using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models;
using Shoko.Server.Models.Internal;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Models.Provider;

namespace Shoko.Server;

public class ShokoEventHandler : IShokoEventHandler
{
    public event EventHandler<FileDeletedEventArgs> FileDeleted;

    public event EventHandler<FileDetectedEventArgs> FileDetected;

    public event EventHandler<FileHashedEventArgs> FileHashed;

    public event EventHandler<FileNotMatchedEventArgs> FileNotMatched;

    public event EventHandler<FileMatchedEventArgs> FileMatched;

    public event EventHandler<FileRenamedEventArgs> FileRenamed;

    public event EventHandler<FileMovedEventArgs> FileMoved;

    public event EventHandler<AniDBBannedEventArgs> AniDBBanned;

    public event EventHandler<ShowUpdatedEventArgs> ShowUpdated;

    public event EventHandler<SeasonUpdatedEventArgs> SeasonUpdated;

    public event EventHandler<EpisodeUpdatedEventArgs> EpisodeUpdated;

    public event EventHandler<MovieUpdatedEventArgs> MovieUpdated;

    public event EventHandler<SettingsSavedEventArgs> SettingsSaved;

    public event EventHandler Start;

    public event EventHandler<CancelEventArgs> Shutdown;

    private static ShokoEventHandler _instance;

    public static ShokoEventHandler Instance
    {
        get
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = new ShokoEventHandler();
            return _instance;
        }
    }

    public void OnFileDetected(ImportFolder folder, FileInfo file)
    {
        var path = file.FullName.Replace(folder.Path, "");
        if (!path.StartsWith("/"))
        {
            path = "/" + path;
        }

        FileDetected?.Invoke(null, new FileDetectedEventArgs(file, path, folder));
    }

    public void OnFileHashed(ImportFolder folder, Shoko_Video_Location vlp)
    {
        FileHashed?.Invoke(null, new FileHashedEventArgs(vlp, vlp.RelativePath, folder));
    }

    public void OnFileDeleted(ImportFolder folder, Shoko_Video_Location vlp, Shoko_Video video)
    {
        FileDeleted?.Invoke(null, new FileDeletedEventArgs(vlp, vlp.RelativePath, folder));
    }

    public void OnFileMatched(Shoko_Video_Location vlp)
    {
        FileMatched?.Invoke(null, new FileMatchedEventArgs(vlp));
    }

    public void OnFileNotMatched(Shoko_Video_Location vlp, int autoMatchAttempts, bool hasXRefs, bool isUDPBanned)
    {
        FileNotMatched?.Invoke(null, new FileNotMatchedEventArgs(vlp, autoMatchAttempts, hasXRefs, isUDPBanned));
    }

    public void OnFileMoved(ImportFolder oldFolder, ImportFolder newFolder, string oldPath, string newPath, Shoko_Video_Location vlp)
    {
        FileMoved?.Invoke(null, new FileMovedEventArgs(vlp, newPath, newFolder, oldPath, oldFolder));
    }

    public void OnFileRenamed(ImportFolder folder, string oldName, string newName, Shoko_Video_Location vlp)
    {
        FileRenamed?.Invoke(null, new FileRenamedEventArgs(vlp, folder, newName, oldName));
    }

    public void OnAniDBBanned(AniDBBanType type, DateTime time, DateTime resumeTime)
    {
        AniDBBanned?.Invoke(null, new AniDBBannedEventArgs(type, time, resumeTime));
    }

    public void OnShowUpdated(IShowMetadata show)
    {
        ShowUpdated?.Invoke(null, new ShowUpdatedEventArgs(show));
    }

    public void OnSeasonUpdated(ISeasonMetadata show)
    {
        SeasonUpdated?.Invoke(null, new SeasonUpdatedEventArgs(show));
    }

    public void OnEpisodeUpdated(IEpisodeMetadata show)
    {
        EpisodeUpdated?.Invoke(null, new EpisodeUpdatedEventArgs(show));
    }

    public void OnMovieUpdated(IMovieMetadata show)
    {
        MovieUpdated?.Invoke(null, new MovieUpdatedEventArgs(show));
    }

    public void OnSettingsSaved()
    {
        SettingsSaved?.Invoke(null, new SettingsSavedEventArgs());
    }

    public void OnStart()
    {
        Start?.Invoke(null, EventArgs.Empty);
    }

    public bool OnShutdown()
    {
        var args = new CancelEventArgs();
        Shutdown?.Invoke(null, args);
        return !args.Cancel;
    }
}
