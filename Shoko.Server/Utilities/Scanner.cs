using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using Shoko.Commons.Extensions;
using Shoko.Commons.Notification;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Server.Databases;
using Shoko.Server.FileHelper;
using Shoko.Server.Models.Internal;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.Utilities;

public class Scanner : INotifyPropertyChangedExt
{
    private BackgroundWorker workerIntegrityScanner = new();

    public Scanner()
    {
        workerIntegrityScanner.WorkerReportsProgress = true;
        workerIntegrityScanner.WorkerSupportsCancellation = true;
        workerIntegrityScanner.DoWork += WorkerIntegrityScanner_DoWork;
    }

    public static Scanner Instance { get; set; } = new();

    public event PropertyChangedEventHandler PropertyChanged;

    public void NotifyPropertyChanged(string propname)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
    }

    private int queueCount = 0;

    public int QueueCount
    {
        get => queueCount;
        set => this.SetField(() => queueCount, value);
    }

    public void Init()
    {
        Utils.MainThreadDispatch(() => { RepoFactory.Scan.GetAll().ForEach(a => Scans.Add(a)); });
        var runscan = Scans.FirstOrDefault(a => a.Status == ScanStatus.Running);
        if (runscan != null)
        {
            ActiveScan = runscan;
            StartScan();
        }
    }

    public void StartScan()
    {
        if (ActiveScan == null)
        {
            return;
        }

        RunScan = ActiveScan;
        cancelIntegrityCheck = false;
        workerIntegrityScanner.RunWorkerAsync();
    }

    public void ClearScan()
    {
        if (ActiveScan == null)
        {
            return;
        }

        if (workerIntegrityScanner.IsBusy && RunScan == ActiveScan)
        {
            CancelScan();
        }

        RepoFactory.ScanFile.Delete(RepoFactory.ScanFile.GetByScanID(ActiveScan.Id));
        RepoFactory.Scan.Delete(ActiveScan);
        Utils.MainThreadDispatch(() => { Scans.Remove(ActiveScan); });
        ActiveScan = null;
    }


    public void CancelScan()
    {
        if (ActiveScan == null)
        {
            return;
        }

        if (workerIntegrityScanner.IsBusy)
        {
            cancelIntegrityCheck = true;
            while (workerIntegrityScanner.IsBusy)
            {
                Utils.DoEvents();
                Thread.Sleep(100);
            }

            cancelIntegrityCheck = false;
        }
    }

    public bool Finished => (ActiveScan != null && ActiveScan.Status == ScanStatus.Finish) ||
                            ActiveScan == null;

    public string QueueState => ActiveScan != null ? ActiveScan.StatusText : string.Empty;
    public bool QueuePaused => ActiveScan != null && ActiveScan.Status == ScanStatus.Standby;
    public bool QueueRunning => ActiveScan != null && ActiveScan.Status == ScanStatus.Running;
    public bool Exists => ActiveScan != null;

    private Scan activeScan;

    public Scan ActiveScan
    {
        get => activeScan;
        set
        {
            if (value != activeScan)
            {
                activeScan = value;
                Refresh();
                Utils.MainThreadDispatch(() =>
                {
                    ActiveErrorFiles.Clear();
                    if (value != null)
                    {
                        RepoFactory.ScanFile.GetWithError(value.Id).ForEach(a => ActiveErrorFiles.Add(a));
                    }
                });
            }
        }
    }

    public void Refresh()
    {
        this.OnPropertyChanged(() => Exists, () => Finished, () => QueueState, () => QueuePaused,
            () => QueueRunning);
        if (activeScan != null)
        {
            QueueCount = RepoFactory.ScanFile.GetWaitingCount(activeScan.Id);
        }
    }

    public ObservableCollection<Scan> Scans { get; set; } = new();

    public ObservableCollection<ScanFile> ActiveErrorFiles { get; set; } = new();

    public bool HasFiles => Finished && ActiveErrorFiles.Count > 0;

    public void AddErrorScan(ScanFile file)
    {
        Utils.MainThreadDispatch(() =>
        {
            if (ActiveScan != null && ActiveScan.Id == file.ScanId)
            {
                ActiveErrorFiles.Add(file);
            }
        });
    }

    public void DeleteAllErroredFiles()
    {
        if (ActiveScan == null)
        {
            return;
        }

        var files = ActiveErrorFiles.ToList();
        ActiveErrorFiles.Clear();
        var episodesToUpdate = new HashSet<Shoko_Episode>();
        var seriesToUpdate = new HashSet<ShokoSeries>();
        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            files.ForEach(file =>
            {
                var place = file.GetVideoLocation();
                place.RemoveAndDeleteFileWithOpenTransaction(session, seriesToUpdate);
            });
            // update everything we modified
            foreach (var ser in seriesToUpdate)
            {
                ser?.QueueUpdateStats();
            }
        }

        RepoFactory.ScanFile.Delete(files);
    }

    private bool cancelIntegrityCheck;

    internal Scan RunScan;

    public static int OnHashProgress(string fileName, int percentComplete)
    {
        return 1; //continue hashing (return 0 to abort)
    }

    private void WorkerIntegrityScanner_DoWork(object sender, DoWorkEventArgs e)
    {
        if (RunScan != null && RunScan.Status != ScanStatus.Finish)
        {
            var paused = ShokoService.CmdProcessorHasher.Paused;
            ShokoService.CmdProcessorHasher.Paused = true;
            var s = RunScan;
            s.Status = ScanStatus.Running;
            RepoFactory.Scan.Save(s);
            Refresh();
            var files = RepoFactory.ScanFile.GetWaiting(s.Id);
            var cnt = 0;
            foreach (var sf in files)
            {
                try
                {
                    if (!File.Exists(sf.AbsolutePath))
                    {
                        sf.Status = ScanFileStatus.ErrorFileNotFound;
                    }
                    else
                    {
                        var f = new FileInfo(sf.AbsolutePath);
                        if (sf.FileSize != f.Length)
                        {
                            sf.Status = ScanFileStatus.ErrorInvalidSize;
                        }
                        else
                        {
                            ShokoService.CmdProcessorHasher.QueueState = new QueueStateStruct
                            {
                                message = "Hashing File: {0}",
                                queueState = QueueStateEnum.HashingFile,
                                extraParams = new[] { sf.AbsolutePath }
                            };
                            var hashes =
                                FileHashHelper.GetHashInfo(sf.AbsolutePath, true, OnHashProgress, false, false, false);
                            if (string.IsNullOrEmpty(hashes.ED2K))
                            {
                                sf.Status = ScanFileStatus.ErrorMissingHash;
                            }
                            else
                            {
                                sf.CheckedED2K = hashes.ED2K;
                                if (sf.IsFaulty)
                                {
                                    sf.Status = ScanFileStatus.ErrorInvalidHash;
                                }
                                else
                                {
                                    sf.Status = ScanFileStatus.ProcessedOK;
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    sf.Status = ScanFileStatus.ErrorIOError;
                }

                cnt++;
                sf.CheckedAt = DateTime.Now;
                RepoFactory.ScanFile.Save(sf);
                if (sf.Status > ScanFileStatus.ProcessedOK)
                {
                    AddErrorScan(sf);
                }

                Refresh();

                if (cancelIntegrityCheck)
                {
                    break;
                }
            }

            if (files.Any(a => a.Status == ScanFileStatus.Waiting))
            {
                s.Status = ScanStatus.Standby;
            }
            else
            {
                s.Status = ScanStatus.Finish;
            }

            RepoFactory.Scan.Save(s);
            Refresh();
            RunScan = null;
            ShokoService.CmdProcessorHasher.Paused = paused;
        }
    }
}
