using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Models;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReferences;
using Shoko.Server.Models.Internal;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Server.Enums;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.ProcessFile)]
public class CommandRequest_ProcessFile : CommandRequestImplementation
{
    private readonly ICommandRequestFactory _commandFactory;

    private readonly IServerSettings _settings;

    private readonly IUDPConnectionHandler _udpConnectionHandler;

    public int VideoLocalID { get; set; }

    public bool ForceAniDB { get; set; }

    public bool SkipMyList { get; set; }

    private Shoko_Video Video;

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority3;

    public override QueueStateStruct PrettyDescription
    {
        get
        {
            if (Video != null)
            {
                return new QueueStateStruct
                {
                    message = "Getting file info from UDP API: {0}",
                    queueState = QueueStateEnum.FileInfo,
                    extraParams = new[] { Video.FileName }
                };
            }

            return new QueueStateStruct
            {
                message = "Getting file info from UDP API: {0}",
                queueState = QueueStateEnum.FileInfo,
                extraParams = new[] { VideoLocalID.ToString() }
            };
        }
    }

    protected override void Process()
    {
        Logger.LogTrace("Processing File: {VideoLocalID}", VideoLocalID);

        try
        {
            // Check if the video local (file) is available.
            if (Video == null)
            {
                Video = RepoFactory.Shoko_Video.GetByID(VideoLocalID);
                if (Video == null)
                    return;
            }

            // Store a hash-set of the old cross-references for comparison later.
            var oldXRefs = Video.GetCrossReferences(false)
                .Select(xref => xref.AnidbEpisodeId)
                .ToHashSet();

            // Process and get the AniDB file entry.
            var aniFile = ProcessFile_AniDB(Video);

            // Rename and/or move the physical file(s) if needed.
            Video.Locations.ForEach(a => { a.RenameAndMoveAsRequired(); });

            // Check if an AniDB file is now available and if the cross-references changed.
            var newXRefs = Video.GetCrossReferences(false)
                .Select(xref => xref.AnidbEpisodeId)
                .ToHashSet();
            var xRefsMatch = newXRefs.SetEquals(oldXRefs);
            if (aniFile != null && newXRefs.Count > 0 && !xRefsMatch)
            {
                // Set/update the import date
                Video.LastImportedAt = DateTime.Now;
                RepoFactory.Shoko_Video.Save(Video);

                // Dispatch the on file matched event.
                ShokoEventHandler.Instance.OnFileMatched(Video.GetPreferredLocation());
            }
            // Fire the file not matched event if we didn't update the cross-references.
            else
            {
                var autoMatchAttempts = RepoFactory.AniDB_File_Update.GetByFileSizeAndHash(Video.Size, Video.ED2K).Count;
                var hasXRefs = newXRefs.Count > 0 && xRefsMatch;
                var isUDPBanned = _udpConnectionHandler.IsBanned;
                ShokoEventHandler.Instance.OnFileNotMatched(Video.GetPreferredLocation(), autoMatchAttempts, hasXRefs, isUDPBanned);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing CommandRequest_ProcessFile: {VideoLocalID}", VideoLocalID);
        }
    }

    private AniDB_File ProcessFile_AniDB(Shoko_Video vidLocal)
    {
        Logger.LogTrace("Checking for AniDB_File record for: {VidLocalHash} --- {VidLocalFileName}", vidLocal.ED2K,
            vidLocal.FileName);
        // check if we already have this AniDB_File info in the database

        var animeIDs = new Dictionary<int, bool>();

        var aniFile = GetLocalAniDBFile(vidLocal);
        if (aniFile?.FileSize != Video.Size)
            aniFile ??= TryGetAniDBFileFromAniDB(vidLocal, animeIDs);
        if (aniFile == null) return null;

        PopulateAnimeForFile(vidLocal, aniFile.CrossReferences, animeIDs);

        // We do this inside, as the info will not be available as needed otherwise
        var videoLocals =
            aniFile.EpisodeIDs?.SelectMany(a => RepoFactory.Shoko_Video.GetByAnidbEpisodeId(a))
                .Where(b => b != null)
                .ToList();
        if (videoLocals == null) return null;

        // Get status from existing eps/files if needed
        GetWatchedStateIfNeeded(vidLocal, videoLocals);

        // update stats for groups and series. The series are not saved until here, so it's absolutely necessary!!
        animeIDs.Keys.ForEach(AniDB_Anime.UpdateStatsByAnimeID);

        if (_settings.FileQualityFilterEnabled)
        {
            videoLocals.Sort(FileQualityFilter.CompareTo);
            var keep = videoLocals
                .Take(_settings.FileQualityPreferences.MaxNumberOfFilesToKeep)
                .ToList();
            foreach (var vl2 in keep)
            {
                videoLocals.Remove(vl2);
            }

            if (!_settings.FileQualityPreferences.AllowDeletionOfImportedFiles &&
                videoLocals.Contains(vidLocal))
            {
                videoLocals.Remove(vidLocal);
            }

            videoLocals = videoLocals.Where(FileQualityFilter.ShouldRemoveVideo).ToList();

            videoLocals.ForEach(a => a.Places.ForEach(b => b.RemoveRecordAndDeletePhysicalFile()));
        }
        
        // we have an AniDB File, so check the release group info
        if (aniFile.GroupID != 0)
        {
            var releaseGroup = RepoFactory.AniDB_ReleaseGroup.GetByGroupID(aniFile.GroupID);
            if (releaseGroup == null)
            {
                // may as well download it immediately. We can change it later if it becomes an issue
                // this will only happen if it's null, and most people grab mostly the same release groups
                var groupCommand =
                    _commandFactory.Create<CommandRequest_GetReleaseGroup>(c => c.GroupID = aniFile.GroupID);
                groupCommand.ProcessCommand();
            }
        }

        // Add this file to the users list
        if (_settings.AniDb.MyList_AddFiles && !SkipMyList && vidLocal.AniDBMyListId <= 0)
        {
            _commandFactory.Create<CommandRequest_AddFileToMyList>(c =>
            {
                c.ED2K = vidLocal.ED2K;
                c.ReadStates = true;
            }).Save();
        }

        return aniFile;
    }

    private void GetWatchedStateIfNeeded(Shoko_Video vidLocal, List<Shoko_Video> videoLocals)
    {
        if (!_settings.Import.UseExistingFileWatchedStatus) return;

        // Copy over watched states
        foreach (var user in RepoFactory.Shoko_User.GetAll())
        {
            var watchedVideo = videoLocals.FirstOrDefault(a =>
                a?.GetUserRecord(user.Id)?.LastWatchedAt != null);
            // No files that are watched
            if (watchedVideo == null)
            {
                continue;
            }

            var watchedRecord = watchedVideo.GetUserRecord(user.Id);
            var userRecord = vidLocal.GetOrCreateUserRecord(user.Id);

            userRecord.LastWatchedAt = watchedRecord.LastWatchedAt;
            userRecord.WatchedCount = watchedRecord.WatchedCount;
            userRecord.ResumePosition = watchedRecord.ResumePosition;

            userRecord.LastUpdatedAt = DateTime.Now;
            RepoFactory.Shoko_Video_User.Save(userRecord);
        }
    }

    private AniDB_File GetLocalAniDBFile(Shoko_Video vidLocal)
    {
        AniDB_File aniFile = null;
        if (!ForceAniDB)
        {
            aniFile = RepoFactory.AniDB_File.GetByHashAndFileSize(vidLocal.ED2K, Video.Size);

            if (aniFile == null)
            {
                Logger.LogTrace("AniDB_File record not found");
            }
        }

        // If cross refs were wiped, but the AniDB_File was not, we unfortunately need to requery the info
        var crossRefs = vidLocal.GetCrossReferences(false);
        if (crossRefs.Count == 0)
        {
            aniFile = null;
        }

        return aniFile;
    }

    private void PopulateAnimeForFile(Shoko_Video vidLocal, IReadOnlyList<CR_Video_Episode> xrefs, Dictionary<int, bool> animeIDs)
    {
        // check if we have the episode info
        // if we don't, we will need to re-download the anime info (which also has episode info)
        if (xrefs.Count == 0)
        {
            // if we have the AniDB file, but no cross refs it means something has been broken
            Logger.LogDebug("Could not find any cross ref records for: {Ed2KHash}", vidLocal.ED2K);
        }
        else
        {
            foreach (var xref in xrefs)
            {
                if (animeIDs.TryGetValue(xref.AnidbAnimeId, out var current))
                    animeIDs[xref.AnidbAnimeId] = current || xref.Episode == null;
                else
                    animeIDs.Add(xref.AnidbAnimeId, xref.Episode == null);
            }
        }

        foreach (var kV in animeIDs)
        {
            var animeID = kV.Key;
            var missingEpisodes = kV.Value;
            // get from DB
            var anime = RepoFactory.AniDB_Anime.GetByAnidbAnimeId(animeID);
            var update = RepoFactory.AniDB_Anime_Update.GetByAnimeID(animeID);
            var animeRecentlyUpdated = false;

            if (anime != null && update != null)
            {
                var ts = DateTime.Now - update.UpdatedAt;
                if (ts.TotalHours < _settings.AniDb.MinimumHoursToRedownloadAnimeInfo)
                {
                    animeRecentlyUpdated = true;
                }
            }
            else
            {
                missingEpisodes = true;
            }

            // even if we are missing episode info, don't get data  more than once every `x` hours
            // this is to prevent banning
            if (missingEpisodes && !animeRecentlyUpdated)
            {
                Logger.LogDebug("Getting Anime record from AniDB....");
                // this should detect and handle a ban, which will leave Result null, and defer
                var animeCommand = _commandFactory.Create<CommandRequest_GetAnimeHTTP>(c =>
                {
                    c.AnimeID = animeID;
                    c.ForceRefresh = true;
                    c.DownloadRelations = _settings.AutoGroupSeries || _settings.AniDb.DownloadRelatedAnime;
                    c.CreateSeriesEntry = true;
                });

                animeCommand.ProcessCommand();
                anime = animeCommand.Result;
            }

            // create the group/series/episode records if needed
            if (anime == null)
            {
                Logger.LogWarning($"Unable to create AniDB_Anime for file: {vidLocal.FileName}");
                Logger.LogWarning("Queuing GET for AniDB_Anime: {AnimeID}", animeID);
                var animeCommand = _commandFactory.Create<CommandRequest_GetAnimeHTTP>(
                    c =>
                    {
                        c.AnimeID = animeID;
                        c.ForceRefresh = true;
                        c.DownloadRelations = _settings.AutoGroupSeries || _settings.AniDb.DownloadRelatedAnime;
                        c.CreateSeriesEntry = true;
                    }
                );
                animeCommand.Save();
                return;
            }

            Logger.LogDebug("Creating groups, series and episodes....");
            // check if there is an AnimeSeries Record associated with this AnimeID
            var ser = RepoFactory.Shoko_Series.GetByAnidbAnimeId(animeID);

            if (ser == null)
            {
                // We will put UpdatedAt in the CreateAnimeSeriesAndGroup method, to ensure it exists at first write
                ser = anime.CreateAnimeSeriesAndGroup();
                ser.CreateAnimeEpisodes(anime);
            }
            else
            {
                var ts = DateTime.Now - ser.UpdatedAt;

                // don't even check episodes if we've done it recently...
                if (ts.TotalHours > 6)
                {
                    if (ser.NeedsEpisodeUpdate())
                    {
                        Logger.LogInformation(
                            "Series {Title} needs episodes regenerated (an episode was added or deleted from AniDB)",
                            anime.MainTitle
                        );
                        ser.CreateAnimeEpisodes(anime);
                        ser.UpdatedAt = DateTime.Now;
                    }
                }
            }

            // check if we have any group status data for this associated anime
            // if not we will download it now
            if (RepoFactory.AniDB_Anime_ReleaseGroup_Status.GetByAnimeID(anime.AnimeId).Count == 0)
            {
                _commandFactory.Create<CommandRequest_GetReleaseGroupStatus>(c => c.AnimeID = anime.AnimeId).Save();
            }

            // Only save the date, we'll update GroupFilters and stats in one pass
            // don't bother saving the series here, it'll happen in AniDB_Anime.UpdateStatsByAnimeID()
            // just don't do anything that needs this changed data before then
            ser.EpisodeAddedDate = DateTime.Now;

            foreach (var grp in ser.AllGroupsAbove)
            {
                grp.EpisodeAddedDate = DateTime.Now;
                RepoFactory.Shoko_Group.Save(grp, false, false, false);
            }
        }
    }

    private SVR_AniDB_File TryGetAniDBFileFromAniDB(Shoko_Video vidLocal, Dictionary<int, bool> animeIDs)
    {
        // check if we already have a record
        var aniFile = RepoFactory.AniDB_File.GetByHashAndFileSize(vidLocal.ED2K, Video.Size);

        if (aniFile == null || aniFile.FileSize != Video.Size)
        {
            ForceAniDB = true;
        }

        if (ForceAniDB)
        {
            // get info from AniDB
            Logger.LogDebug("Getting AniDB_File record from AniDB....");
            try
            {
                var fileCommand = _commandFactory.Create<CommandRequest_GetFile>(c =>
                {
                    c.VideoLocalID = Video.VideoLocalID;
                    c.ForceAniDB = true;
                    c.BubbleExceptions = true;
                });
                fileCommand.ProcessCommand();
                aniFile = fileCommand.Result;
            }
            catch (AniDBBannedException)
            {
                // We're banned, so queue it for later
                Logger.LogError("We are banned. Re-queuing {CommandID} for later", CommandID);
                var fileCommand = _commandFactory.Create<CommandRequest_ProcessFile>(
                    c =>
                    {
                        c.VideoLocalID = Video.VideoLocalID;
                        c.ForceAniDB = true;
                    }
                );
                fileCommand.Save(true);
            }
        }

        if (aniFile == null)
        {
            return null;
        }

        // get Anime IDs from the file for processing, the episodes might not be created yet here
        aniFile.CrossReferences.Select(a => a.AnidbAnimeId).Distinct().ForEach(animeID =>
        {
            if (animeIDs.ContainsKey(animeID))
            {
                animeIDs[animeID] = false;
            }
            else
            {
                animeIDs.Add(animeID, false);
            }
        });

        return aniFile;
    }

    /// <summary>
    /// This should generate a unique key for a command
    /// It will be used to check whether the command has already been queued before adding it
    /// </summary>
    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_ProcessFile_{VideoLocalID}";
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
        VideoLocalID = int.Parse(TryGetProperty(docCreator, "CommandRequest_ProcessFile", "VideoLocalID"));
        ForceAniDB = bool.Parse(TryGetProperty(docCreator, "CommandRequest_ProcessFile", "ForceAniDB"));
        SkipMyList = bool.Parse(TryGetProperty(docCreator, "CommandRequest_ProcessFile", "SkipMyList"));
        Video = RepoFactory.Shoko_Video.GetByID(VideoLocalID);

        return true;
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

    public CommandRequest_ProcessFile(ILoggerFactory loggerFactory, ICommandRequestFactory commandFactory, ISettingsProvider settingsProvider, IUDPConnectionHandler udpConnectionHandler) :
        base(loggerFactory)
    {
        _commandFactory = commandFactory;
        _settings = settingsProvider.GetSettings();
        _udpConnectionHandler = udpConnectionHandler;
    }

    protected CommandRequest_ProcessFile()
    {
    }
}
