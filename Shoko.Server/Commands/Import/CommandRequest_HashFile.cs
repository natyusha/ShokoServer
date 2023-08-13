using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.FileHelper;
using Shoko.Server.Models;
using Shoko.Server.Models.Internal;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.HashFile)]
public class CommandRequest_HashFile : CommandRequestImplementation
{
    private readonly ICommandRequestFactory _commandFactory;
    private readonly ISettingsProvider _settingsProvider;
    public string FileName { get; set; }
    public bool ForceHash { get; set; }

    public bool SkipMyList { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority4;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Checking File for Hashes: {0}",
        queueState = QueueStateEnum.CheckingFile,
        extraParams = new[] { FileName }
    };

    public QueueStateStruct PrettyDescriptionHashing => new()
    {
        message = "Hashing File: {0}", queueState = QueueStateEnum.HashingFile, extraParams = new[] { FileName }
    };

    protected override void Process()
    {
        Logger.LogTrace("Checking File For Hashes: {Filename}", FileName);

        try
        {
            ProcessFile_LocalInfo();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing file: {Filename}", FileName);
        }
    }

    //Added size return, since symbolic links return 0, we use this function also to return the size of the file.
    private long CanAccessFile(string fileName, bool writeAccess, ref Exception e)
    {
        var accessType = writeAccess ? FileAccess.ReadWrite : FileAccess.Read;
        try
        {
            using (var fs = File.Open(fileName, FileMode.Open, accessType, FileShare.ReadWrite))
            {
                var size = fs.Seek(0, SeekOrigin.End);
                return size;
            }
        }
        catch (IOException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            // This shouldn't cause a recursion, as it'll throw if failing
            Logger.LogTrace("File {FileName} is Read-Only, unmarking", fileName);
            try
            {
                var info = new FileInfo(fileName);
                if (info.IsReadOnly)
                {
                    info.IsReadOnly = false;
                }

                // check to see if it stuck. On linux, we can't just winapi hack our way out, so don't recurse in that case, anyway
                if (!new FileInfo(fileName).IsReadOnly && !Utils.IsRunningOnLinuxOrMac())
                {
                    return CanAccessFile(fileName, writeAccess, ref e);
                }
            }
            catch
            {
                // ignore, we tried
            }

            e = ex;
            return 0;
        }
    }

    private void ProcessFile_LocalInfo()
    {
        // hash and read media info for file
        int nshareID;

        var (folder, filePath) = RepoFactory.ImportFolder.GetFromAbsolutePath(FileName);
        if (folder == null || string.IsNullOrEmpty(filePath))
        {
            Logger.LogError("Unable to locate Import Folder for {FileName}", FileName);
            return;
        }

        long filesize = 0;
        Exception e = null;

        if (!File.Exists(FileName))
        {
            Logger.LogError("File does not exist: {Filename}", FileName);
            return;
        }

        var settings = _settingsProvider.GetSettings();
        if (settings.Import.FileLockChecking)
        {
            var numAttempts = 0;
            var writeAccess = folder.IsDropSource == 1;

            // At least 1s between to ensure that size has the chance to change
            var waitTime = settings.Import.FileLockWaitTimeMS;
            if (waitTime < 1000)
            {
                waitTime = settings.Import.FileLockWaitTimeMS = 4000;
                _settingsProvider.SaveSettings();
            }

            // We do checks in the file watcher, but we want to make sure we can still access the file
            // Wait 1 minute before giving up on trying to access the file
            while ((filesize = CanAccessFile(FileName, writeAccess, ref e)) == 0 && numAttempts < 60)
            {
                numAttempts++;
                Thread.Sleep(waitTime);
                Logger.LogTrace("Failed to access, (or filesize is 0) Attempt # {NumAttempts}, {FileName}",
                    numAttempts, FileName);
            }

            // if we failed to access the file, get ouuta here
            if (numAttempts >= 60 || filesize == 0)
            {
                Logger.LogError("Could not access file: {Filename}", FileName);
                return;
            }
        }

        if (!File.Exists(FileName))
        {
            Logger.LogError("Could not access file: {Filename}", FileName);
            return;
        }

        nshareID = folder.Id;


        // check if we have already processed this file
        var vlocalplace = RepoFactory.Shoko_Video_Location.GetByFilePathAndImportFolderID(filePath, nshareID);
        Shoko_Video vlocal = null;
        var filename = Path.GetFileName(filePath);

        if (vlocalplace != null)
        {
            vlocal = vlocalplace.Video;
            if (vlocal != null)
            {
                Logger.LogTrace("VideoLocal record found in database: {Filename}", FileName);

                // This will only happen with DB corruption, so just clean up the mess.
                if (vlocalplace.FullServerPath == null)
                {
                    if (vlocal.Places.Count == 1)
                    {
                        RepoFactory.Shoko_Video.Delete(vlocal);
                        vlocal = null;
                    }

                    RepoFactory.Shoko_Video_Location.Delete(vlocalplace);
                    vlocalplace = null;
                }

                if (vlocal != null && ForceHash)
                {
                    vlocal.FileSize = filesize;
                    vlocal.DateTimeUpdated = DateTime.Now;
                }
            }
        }

        if (vlocal == null)
        {
            Logger.LogTrace("No existing VideoLocal, creating temporary record");
            vlocal = new Shoko_Video
            {
                LastUpdatedAt = DateTime.Now,
                CreatedAt = DateTimeUpdated,
            };
        }

        if (vlocalplace == null)
        {
            Logger.LogTrace("No existing VideoLocal_Place, creating a new record");
            vlocalplace = new Shoko_Video_Location
            {
                RelativePath = filePath,
                ImportFolderId = nshareID,
            };
            // Make sure we have an ID
            RepoFactory.Shoko_Video_Location.Save(vlocalplace);
        }

        // check if we need to get a hash this file
        if (string.IsNullOrEmpty(vlocal.Hash) || ForceHash)
        {
            Logger.LogTrace("No existing hash in VideoLocal, checking XRefs");
            if (!ForceHash)
            {
                // try getting the hash from the CrossRef
                if (!TrySetHashFromXrefs(filename, vlocal))
                    TrySetHashFromFileNameHash(filename, vlocal);    
            }

            if (string.IsNullOrEmpty(vlocal.Hash) || string.IsNullOrEmpty(vlocal.CRC32) || string.IsNullOrEmpty(vlocal.MD5) || string.IsNullOrEmpty(vlocal.SHA1))
                FillHashesAgainstVideoLocalRepo(vlocal);

            FillMissingHashes(vlocal, ForceHash);
            // We should have a hash by now
            // before we save it, lets make sure there is not any other record with this hash (possible duplicate file)

            var tlocal = RepoFactory.Shoko_Video.GetByED2K(vlocal.Hash);
            var duplicate = false;
            var changed = false;

            if (tlocal != null)
            {
                Logger.LogTrace("Found existing VideoLocal with hash, merging info from it");
                // Aid with hashing cloud. Merge hashes and save, regardless of duplicate file
                changed = tlocal.MergeInfoFrom(vlocal);
                vlocal = tlocal;

                var preps = vlocal.Places.Where(a => !vlocalplace.FullServerPath.Equals(a.FullServerPath)).ToList();
                foreach (var prep in preps)
                {
                    if (prep == null)
                    {
                        continue;
                    }

                    // clean up, if there is a 'duplicate file' that is invalid, remove it.
                    if (prep.FullServerPath == null)
                    {
                        RepoFactory.Shoko_Video_Location.Delete(prep);
                    }
                    else
                    {
                        if (!File.Exists(prep.FullServerPath))
                        {
                            RepoFactory.Shoko_Video_Location.Delete(prep);
                        }
                    }
                }

                var dupPlace = vlocal.Places.FirstOrDefault(a => !vlocalplace.FullServerPath.Equals(a.FullServerPath));

                if (dupPlace != null)
                {
                    Logger.LogWarning("Found Duplicate File");
                    Logger.LogWarning("---------------------------------------------");
                    Logger.LogWarning("New File: {FullServerPath}", vlocalplace.FullServerPath);
                    Logger.LogWarning("Existing File: {FullServerPath}", dupPlace.FullServerPath);
                    Logger.LogWarning("---------------------------------------------");

                    if (settings.Import.AutomaticallyDeleteDuplicatesOnImport)
                    {
                        vlocalplace.RemoveRecordAndDeletePhysicalFile();
                        return;
                    }

                    // check if we have a record of this in the database, if not create one
                    var dupFiles = RepoFactory.DuplicateFile.GetByFilePathsAndImportFolder(
                        vlocalplace.FilePath,
                        dupPlace.FilePath,
                        vlocalplace.ImportFolderID, dupPlace.ImportFolderID);
                    if (dupFiles.Count == 0)
                    {
                        dupFiles = RepoFactory.DuplicateFile.GetByFilePathsAndImportFolder(dupPlace.FilePath,
                            vlocalplace.FilePath, dupPlace.ImportFolderID, vlocalplace.ImportFolderID);
                    }

                    if (dupFiles.Count == 0)
                    {
                        var dup = new DuplicateFile
                        {
                            DateTimeUpdated = DateTime.Now,
                            FilePathFile1 = vlocalplace.FilePath,
                            FilePathFile2 = dupPlace.FilePath,
                            ImportFolderIDFile1 = vlocalplace.ImportFolderID,
                            ImportFolderIDFile2 = dupPlace.ImportFolderID,
                            Hash = vlocal.Hash
                        };
                        RepoFactory.DuplicateFile.Save(dup);
                    }

                    //Notify duplicate, don't delete
                    duplicate = true;
                }
            }

            if (!duplicate || changed)
            {
                RepoFactory.Shoko_Video.Save(vlocal, true);
            }

            vlocalplace.VideoLocalID = vlocal.VideoLocalID;
            RepoFactory.Shoko_Video_Location.Save(vlocalplace);

            if (duplicate)
            {
                var crProcfile3 = _commandFactory.Create<CommandRequest_ProcessFile>(
                    c =>
                    {
                        c.VideoLocalID = vlocal.VideoLocalID;
                        c.ForceAniDB = false;
                    }
                );
                crProcfile3.Save();
                return;
            }

            // also save the filename to hash record
            // replace the existing records just in case it was corrupt
            var fnhashes2 = RepoFactory.CR_FileName_ED2K.GetByFileNameAndSize(filename, vlocal.FileSize);
            if (fnhashes2 is { Count: > 1 })
            {
                // if we have more than one record it probably means there is some sort of corruption
                // lets delete the local records
                RepoFactory.CR_FileName_ED2K.Delete(fnhashes2);
            }

            var fnhash = fnhashes2 is { Count: 1 } ? fnhashes2[0] : new FileNameHash();

            fnhash.FileName = filename;
            fnhash.FileSize = vlocal.FileSize;
            fnhash.Hash = vlocal.Hash;
            fnhash.DateTimeUpdated = DateTime.Now;
            RepoFactory.CR_FileName_ED2K.Save(fnhash);
        }
        else
        {
            FillMissingHashes(vlocal, ForceHash);
        }


        if ((vlocal.Media?.GeneralStream?.Duration ?? 0) == 0 || vlocal.MediaVersion < Shoko_Video.MEDIA_VERSION)
        {
            if (vlocalplace.RefreshMediaInfo())
            {
                RepoFactory.Shoko_Video.Save(vlocalplace.VideoLocal, true);
            }
        }

        ShokoEventHandler.Instance.OnFileHashed(folder, vlocalplace);

        // now add a command to process the file
        var crProcFile = _commandFactory.Create<CommandRequest_ProcessFile>(c =>
        {
            c.VideoLocalID = vlocal.VideoLocalID;
            c.ForceAniDB = false;
            c.SkipMyList = SkipMyList;
        });
        crProcFile.Save();
    }

    private bool TrySetHashFromXrefs(string filename, Shoko_Video vlocal)
    {
        var crossRefs =
            RepoFactory.CR_Video_Episode.GetByFileNameAndSize(filename, vlocal.Size);
        if (!crossRefs.Any()) return false;

        vlocal.ED2K = crossRefs[0].ED2K;
        Logger.LogTrace("Got hash from xrefs: {Filename} ({ED2K})", FileName, crossRefs[0].ED2K);
        return true;
    }

    private bool TrySetHashFromFileNameHash(string filename, Shoko_Video vlocal)
    {
        var fnhashes = RepoFactory.CR_FileName_ED2K.GetByFileNameAndSize(filename, vlocal.Size);
        if (fnhashes is { Count: > 1 })
        {
            // if we have more than one record it probably means there is some sort of corruption
            // lets delete the local records
            foreach (var fnh in fnhashes)
            {
                RepoFactory.CR_FileName_ED2K.Delete(fnh.FileNameHashID);
            }
        }

        // reinit this to check if we erased them
        fnhashes = RepoFactory.CR_FileName_ED2K.GetByFileNameAndSize(filename, vlocal.Size);

        if (fnhashes is not { Count: 1 }) return false;

        Logger.LogTrace("Got hash from LOCAL cache: {Filename} ({ED2K})", FileName, fnhashes[0].Hash);
        vlocal.ED2K = fnhashes[0].Hash;
        return true;

    }

    private void FillMissingHashes(Shoko_Video vlocal, bool force)
    {
        var hasherSettings = _settingsProvider.GetSettings().Import.Hasher;
        var needEd2k = string.IsNullOrEmpty(vlocal.Hash);
        var needCRC32 = string.IsNullOrEmpty(vlocal.CRC32) && hasherSettings.CRC || hasherSettings.ForceGeneratesAllHashes && force;
        var needMD5 = string.IsNullOrEmpty(vlocal.MD5) && hasherSettings.MD5 || hasherSettings.ForceGeneratesAllHashes && force;
        var needSHA1 = string.IsNullOrEmpty(vlocal.SHA1) && hasherSettings.SHA1 || hasherSettings.ForceGeneratesAllHashes && force;
        if (needCRC32 || needMD5 || needSHA1) FillHashesAgainstVideoLocalRepo(vlocal);

        needCRC32 = string.IsNullOrEmpty(vlocal.CRC32) && hasherSettings.CRC || hasherSettings.ForceGeneratesAllHashes && force;
        needMD5 = string.IsNullOrEmpty(vlocal.MD5) && hasherSettings.MD5 || hasherSettings.ForceGeneratesAllHashes && force;
        needSHA1 = string.IsNullOrEmpty(vlocal.SHA1) && hasherSettings.SHA1 || hasherSettings.ForceGeneratesAllHashes && force;
        if (!needEd2k && !needCRC32 && !needMD5 && !needSHA1) return;

        ShokoService.CmdProcessorHasher.QueueState = PrettyDescriptionHashing;
        var start = DateTime.Now;
        var tp = new List<string>();
        if (needSHA1) tp.Add("SHA1");
        if (needMD5) tp.Add("MD5");
        if (needCRC32) tp.Add("CRC32");

        Logger.LogTrace("Calculating missing {Filename} hashes for: {Types}", FileName, string.Join(",", tp));
        // update the VideoLocal record with the Hash, since cloud support we calculate everything
        var hashes = FileHashHelper.GetHashInfo(FileName.Replace("/", $"{Path.DirectorySeparatorChar}"), true,
            ShokoServer.OnHashProgress,
            needCRC32, needMD5, needSHA1);
        var ts = DateTime.Now - start;
        Logger.LogTrace("Hashed file in {TotalSeconds:#0.0} seconds --- {Filename} ({Size})", ts.TotalSeconds,
            FileName, Utils.FormatByteSize(vlocal.FileSize));

        if (string.IsNullOrEmpty(vlocal.Hash)) vlocal.Hash = hashes.ED2K?.ToUpperInvariant();
        if (needSHA1) vlocal.SHA1 = hashes.SHA1?.ToUpperInvariant();
        if (needMD5) vlocal.MD5 = hashes.MD5?.ToUpperInvariant();
        if (needCRC32) vlocal.CRC32 = hashes.CRC32?.ToUpperInvariant();
    }

    private static void FillHashesAgainstVideoLocalRepo(Shoko_Video v)
    {
        if (!string.IsNullOrEmpty(v.ED2KHash))
        {
            var n = RepoFactory.Shoko_Video.GetByHash(v.ED2KHash);
            if (n != null)
            {
                if (!string.IsNullOrEmpty(n.CRC32))
                {
                    v.CRC32 = n.CRC32.ToUpperInvariant();
                }

                if (!string.IsNullOrEmpty(n.MD5))
                {
                    v.MD5 = n.MD5.ToUpperInvariant();
                }

                if (!string.IsNullOrEmpty(n.SHA1))
                {
                    v.SHA1 = n.SHA1.ToUpperInvariant();
                }

                return;
            }
        }

        if (!string.IsNullOrEmpty(v.SHA1))
        {
            var n = RepoFactory.Shoko_Video.GetBySHA1(v.SHA1);
            if (n != null)
            {
                if (!string.IsNullOrEmpty(n.CRC32))
                {
                    v.CRC32 = n.CRC32.ToUpperInvariant();
                }

                if (!string.IsNullOrEmpty(n.MD5))
                {
                    v.MD5 = n.MD5.ToUpperInvariant();
                }

                if (!string.IsNullOrEmpty(v.ED2KHash))
                {
                    v.ED2KHash = n.ED2K.ToUpperInvariant();
                }

                return;
            }
        }

        if (!string.IsNullOrEmpty(v.MD5))
        {
            var n = RepoFactory.Shoko_Video.GetByMD5(v.MD5);
            if (n != null)
            {
                if (!string.IsNullOrEmpty(n.CRC32))
                {
                    v.CRC32 = n.CRC32.ToUpperInvariant();
                }

                if (!string.IsNullOrEmpty(n.SHA1))
                {
                    v.SHA1 = n.SHA1.ToUpperInvariant();
                }

                if (!string.IsNullOrEmpty(v.ED2K))
                {
                    v.ED2K = n.ED2K.ToUpperInvariant();
                }
            }
        }
    }

    /// <summary>
    /// This should generate a unique key for a command
    /// It will be used to check whether the command has already been queued before adding it
    /// </summary>
    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_HashFile_{FileName}";
    }

    public override bool LoadFromDBCommand(CommandRequest cq)
    {
        CommandID = cq.CommandID;
        CommandRequestID = cq.CommandRequestID;
        Priority = cq.Priority;
        CommandDetails = cq.CommandDetails;
        DateTimeUpdated = cq.DateTimeUpdated;

        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        FileName = TryGetProperty(docCreator, "CommandRequest_HashFile", "FileName");
        ForceHash = bool.Parse(TryGetProperty(docCreator, "CommandRequest_HashFile", "ForceHash"));
        SkipMyList = bool.Parse(TryGetProperty(docCreator, "CommandRequest_HashFile", "SkipMyList"));

        return FileName.Trim().Length > 0;
    }

    public override CommandRequest ToDatabaseObject()
    {
        GenerateCommandID();

        var cq = new CommandRequest
        {
            CommandID = CommandID,
            CommandType = CommandType,
            Priority = Priority,
            CommandDetails = ToXML(),
            DateTimeUpdated = DateTime.Now
        };
        return cq;
    }

    public CommandRequest_HashFile(ILoggerFactory loggerFactory, ICommandRequestFactory commandFactory, ISettingsProvider settingsProvider) :
        base(loggerFactory)
    {
        _commandFactory = commandFactory;
        _settingsProvider = settingsProvider;
    }

    protected CommandRequest_HashFile()
    {
    }
}
