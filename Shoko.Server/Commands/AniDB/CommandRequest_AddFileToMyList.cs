using System;
using System.Linq;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Models.Internal;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.User;
using Shoko.Server.Repositories;
using Shoko.Server.Server.Enums;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands.AniDB;

[Serializable]
[Command(CommandRequestType.AniDB_AddFileUDP)]
public class CommandRequest_AddFileToMyList : CommandRequestImplementation
{
    private readonly IRequestFactory _requestFactory;
    private readonly ICommandRequestFactory _commandFactory;
    private readonly ISettingsProvider _settingsProvider;

    public string Hash { get; set; }

    public bool ReadStates { get; set; } = true;

    [NonSerialized]
    private Shoko_Video Video;

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

    public override QueueStateStruct PrettyDescription
    {
        get
        {
            if (Video != null)
            {
                return new QueueStateStruct
                {
                    message = "Adding file to MyList: {0}",
                    queueState = QueueStateEnum.AniDB_MyListAdd,
                    extraParams = new[] { Video.FileName }
                };
            }

            return new QueueStateStruct
            {
                message = "Adding file to MyList: {0}",
                queueState = QueueStateEnum.AniDB_MyListAdd,
                extraParams = new[] { Hash }
            };
        }
    }

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_AddFileToMyList: {FileName} - {Hash} - {ReadStates}",
            Video?.GetPreferredLocation()?.FileName, Hash, ReadStates);

        try
        {
            if (Video == null)
            {
                return;
            }

            var settings = _settingsProvider.GetSettings();

            // when adding a file via the API, newWatchedStatus will return with current watched status on AniDB
            // if the file is already on the user's list

            var isManualLink = Video.GetAniDBFile() == null;

            // mark the video file as watched
            var aniDBUsers = RepoFactory.Shoko_User.GetAniDBUsers();
            var juser = aniDBUsers.FirstOrDefault();
            DateTime? originalWatchedDate = null;
            if (juser != null)
            {
                originalWatchedDate = Video.GetUserRecord(juser.Id)?.WatchedDate?.ToUniversalTime();
            }

            UDPResponse<ResponseMyListFile> response = null;
            // this only gets overwritten if the response is File Already in MyList
            var state = settings.AniDb.MyList_StorageState;

            if (isManualLink)
            {
                var episodes = Video.GetAnimeEpisodes().Select(a => a.AniDB_Episode).ToArray();
                foreach (var episode in episodes)
                {
                    var request = _requestFactory.Create<RequestAddEpisode>(
                        r =>
                        {
                            r.State = state.GetMyList_State();
                            r.IsWatched = originalWatchedDate.HasValue;
                            r.WatchedDate = originalWatchedDate;
                            r.AnimeID = episode.AnimeID;
                            r.EpisodeNumber = episode.EpisodeNumber;
                            r.EpisodeType = (EpisodeType)episode.EpisodeType;
                        }
                    );
                    response = request.Execute();

                    if (response.Code != UDPReturnCode.FILE_ALREADY_IN_MYLIST)
                    {
                        continue;
                    }

                    var updateRequest = _requestFactory.Create<RequestUpdateEpisode>(
                        r =>
                        {
                            r.State = state.GetMyList_State();
                            r.IsWatched = originalWatchedDate.HasValue;
                            r.WatchedDate = originalWatchedDate;
                            r.AnimeID = episode.AnimeID;
                            r.EpisodeNumber = episode.EpisodeNumber;
                            r.EpisodeType = (EpisodeType)episode.EpisodeType;
                        }
                    );
                    updateRequest.Execute();
                }
            }
            else
            {
                var request = _requestFactory.Create<RequestAddFile>(
                    r =>
                    {
                        r.State = state.GetMyList_State();
                        r.IsWatched = originalWatchedDate.HasValue;
                        r.WatchedDate = originalWatchedDate;
                        r.Hash = Video.Hash;
                        r.Size = Video.FileSize;
                    }
                );
                response = request.Execute();

                if (response.Code == UDPReturnCode.FILE_ALREADY_IN_MYLIST)
                {
                    var updateRequest = _requestFactory.Create<RequestUpdateFile>(
                        r =>
                        {
                            r.State = state.GetMyList_State();
                            if (originalWatchedDate.HasValue)
                            {
                                r.IsWatched = originalWatchedDate.HasValue;
                                r.WatchedDate = originalWatchedDate;                                
                            }
                            r.Hash = Video.Hash;
                            r.Size = Video.FileSize;
                        }
                    );
                    updateRequest.Execute();
                }
            }

            // never true for Manual Links, so no worries about the loop overwriting it
            if ((response?.Response?.MyListID ?? 0) != 0)
            {
                Video.MyListID = response.Response.MyListID;
                RepoFactory.Shoko_Video.Save(Video);
            }

            var newWatchedDate = response?.Response?.WatchedDate;
            Logger.LogInformation(
                "Added File to MyList. File: {FileName}  Manual Link: {IsManualLink}  Watched Locally: {Unknown}  Watched AniDB: {ResponseIsWatched}  Local State: {AniDbMyListStorageState}  AniDB State: {State}  ReadStates: {ReadStates}  ReadWatched Setting: {AniDbMyListReadWatched}  ReadUnwatched Setting: {AniDbMyListReadUnwatched}",
                Video.GetPreferredLocation()?.FileName, isManualLink, originalWatchedDate != null,
                response?.Response?.IsWatched, settings.AniDb.MyList_StorageState, state, ReadStates,
                settings.AniDb.MyList_ReadWatched, settings.AniDb.MyList_ReadUnwatched
            );
            if (juser != null)
            {
                var watched = newWatchedDate != null && !DateTime.UnixEpoch.Equals(newWatchedDate);
                var watchedLocally = originalWatchedDate != null;

                if (ReadStates)
                {
                    // handle import watched settings. Don't update AniDB in either case, we'll do that with the storage state
                    if (settings.AniDb.MyList_ReadWatched && watched && !watchedLocally)
                    {
                        Video.ToggleWatchedStatus(true, false, newWatchedDate?.ToLocalTime(), false, juser.Id,
                            false, false);
                    }
                    else if (settings.AniDb.MyList_ReadUnwatched && !watched && watchedLocally)
                    {
                        Video.ToggleWatchedStatus(false, false, null, false, juser.Id,
                            false, false);
                    }
                }
            }

            // if we don't have xrefs, then no series or eps.
            var series = Video.EpisodeCrossRefs.Select(a => a.AnimeID).Distinct().ToArray();
            if (series.Length <= 0)
            {
                return;
            }

            foreach (var id in series)
            {
                var ser = RepoFactory.Shoko_Series.GetByAnimeID(id);
                ser?.QueueUpdateStats();
            }

            // lets also try adding to the users trakt collection
            if (settings.TraktTv.Enabled &&
                !string.IsNullOrEmpty(settings.TraktTv.AuthToken))
            {
                foreach (var aep in Video.GetAnimeEpisodes())
                {
                    var cmdSyncTrakt = _commandFactory.Create<CommandRequest_TraktCollectionEpisode>(
                        c =>
                        {
                            c.AnimeEpisodeID = aep.AnimeEpisodeID;
                            c.Action = (int)TraktSyncAction.Add;
                        }
                    );
                    cmdSyncTrakt.Save();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing CommandRequest_AddFileToMyList: {Hash}", Hash);
        }
    }

    /// <summary>
    /// This should generate a unique key for a command
    /// It will be used to check whether the command has already been queued before adding it
    /// </summary>
    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_AddFileToMyList_{Hash}";
    }

    public override bool LoadFromDBCommand(CommandRequest cq)
    {
        CommandID = cq.CommandID;
        CommandRequestID = cq.CommandRequestID;
        Priority = cq.Priority;
        CommandDetails = cq.CommandDetails;
        DateTimeUpdated = cq.DateTimeUpdated;

        // read xml to get parameters
        if (CommandDetails.Trim().Length > 0)
        {
            var docCreator = new XmlDocument();
            docCreator.LoadXml(CommandDetails);

            // populate the fields
            Hash = TryGetProperty(docCreator, "CommandRequest_AddFileToMyList", "Hash");
            var read = TryGetProperty(docCreator, "CommandRequest_AddFileToMyList", "ReadStates");
            if (!bool.TryParse(read, out var readStates))
            {
                readStates = true;
            }

            ReadStates = readStates;
        }

        if (Hash.Trim().Length <= 0)
        {
            return false;
        }

        Video = RepoFactory.Shoko_Video.GetByED2K(Hash);
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

    public CommandRequest_AddFileToMyList(ILoggerFactory loggerFactory, IRequestFactory requestFactory,
        ICommandRequestFactory commandFactory, ISettingsProvider settingsProvider) : base(loggerFactory)
    {
        _requestFactory = requestFactory;
        _commandFactory = commandFactory;
        _settingsProvider = settingsProvider;
    }

    protected CommandRequest_AddFileToMyList()
    {
    }
}
