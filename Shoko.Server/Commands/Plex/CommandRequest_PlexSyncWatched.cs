using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Models;
using Shoko.Server.Plex;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands.Plex;

[Command(CommandRequestType.Plex_Sync)]
internal class CommandRequest_PlexSyncWatched : CommandRequestImplementation
{
    private readonly ISettingsProvider _settingsProvider;
    public SVR_JMMUser User;

    protected override void Process()
    {
        var userPlexSettings = User.Plex;
        var plexHelper = PlexHelper.GetForUser(User);

        // Check if the user is authenticated.
        if (!plexHelper.IsAuthenticated)
        {
            Logger.LogWarning("Unable to sync videos for {Username} with plex. User is not authenticated against plex. (User={UserID})", User.Username, User.JMMUserID);
            return;
        }

        // Check if the user have a selected server.
        var plexUser = plexHelper.PlexUser;
        var plexServer = plexHelper.SelectedServer;
        if (plexServer == null)
        {
            if (string.IsNullOrEmpty(userPlexSettings.SelectedServer))
                Logger.LogWarning("Unable to sync videos for {Username} with plex. User have not selected an server to sync. (User={UserID})", User.Username, User.JMMUserID);
            else
                Logger.LogWarning("Unable to sync videos for {Username} with plex. User have not selected an server to sync. (User={UserID})", User.Username, User.JMMUserID);
            return;
        }

        // Check the selected libraries from the selected server.
        var plexLibraries = plexHelper.GetSelectedDirectories();
        if (plexLibraries.Length == 0)
        {
            if (userPlexSettings.SelectedLibraries.Count == 0)
                Logger.LogInformation("Unable to sync videos for {Username} with plex. User has not selected any libraries to sync. (User={UserID})", User.Username, User.JMMUserID);
            else
                Logger.LogInformation("Unable to sync videos for {Username} with plex. Unable to load selected libraries to sync. (User={UserID})", User.Username, User.JMMUserID);
            return;
        }

        // Warn the user if some of the selected libraries have become unavailable.
        var unavailableLibraries = userPlexSettings.SelectedLibraries.Count - plexLibraries.Length;
        if (unavailableLibraries > 0)
            Logger.LogWarning("Unable to find {UnavailableCount} libraries for {Username} in server {ServerName}. (User={UserID})", unavailableLibraries, User.Username, plexServer.Name, User.JMMUserID);

        // Start the sync.
        Logger.LogInformation("Syncing {LibraryCount} libraries between plex user {PlexUser} and shoko user {ShokoUser} in plex server {ServerName}. (PlexUser={PlexID},User={ShokoID})", plexLibraries.Length, plexUser.Username, User.Username, plexUser.Id, User.JMMUserID);
        foreach (var plexLibrary in plexLibraries)
        {
            // Warn against potentially misconfigured library settings.
            if (plexLibrary.Scanner != "Shoko")
                Logger.LogWarning("Library {LibraryName} is not set to use the shoko scanner. If this is not by mistake then you can look away from this error. (Library={LibraryID},User={UserID})", plexLibrary.Title, plexLibrary.Key, User.JMMUserID);
            if (plexLibrary.Agent != "Shoko")
                Logger.LogWarning("Library {LibraryName} is not set to use the shoko agent. If this is not by mistake then you can look away from this error. (Library={LibraryID},User={UserID})", plexLibrary.Title, plexLibrary.Key, User.JMMUserID);

            var currentCount = 0L;
            var plexShows = plexLibrary.GetShows();
            var totalCount = plexShows.Select(show => show.LeafCount).Aggregate(0L, (a, t) => a + t);
            Logger.LogInformation("Syncing watched videos for {Username} in library {LibraryName}. (Library={LibraryID},User={UserID})", totalCount, User.Username, User.JMMUserID);
            foreach (var plexShow in plexShows)
            {
                if (++currentCount % 10 == 0)
                    Logger.LogInformation("Syncing watched videos for {Username} in library {LibraryName}. {Current}/{Total} (Library={LibraryID},User={UserID})", User.Username, plexLibrary.Title, currentCount, totalCount, plexLibrary.Key, User.JMMUserID);

                var plexEpisodes = plexShow.GetEpisodes();
                foreach (var plexEpisode in plexEpisodes)
                {
                    // Get the anime episode _and_ the video local entry for the given path.
                    var fileName = plexEpisode.FileName;
                    var (episode, file) = GetAnimeEpisodeAndVideoLocalByFileName(fileName);
                    if (episode == null || file == null)
                    {
                        Logger.LogDebug("Skipped {FileName}. No valid episode or special.", fileName);
                        continue;
                    }

                    // Compare and sync the watch state between shoko and plex.
                    var fileUR = file.GetUserRecord(User.JMMUserID);
                    var episodeUR = episode.GetUserRecord(User.JMMUserID);
                    DateTime? lastWatchedInShoko = fileUR?.WatchedDate ?? episodeUR?.WatchedDate;
                    DateTime? lastWatchedInPlex = plexEpisode.ViewCount > 1 && plexEpisode.LastViewedAt != null ? FromUnixTime(plexEpisode.LastViewedAt.Value) : null;
                    bool isWatchedInShoko = lastWatchedInShoko is not null;
                    bool isWatchedInPlex = lastWatchedInPlex is not null;
                    bool inSync = isWatchedInPlex && isWatchedInShoko ? lastWatchedInShoko == lastWatchedInPlex : isWatchedInPlex == isWatchedInShoko;
                    Logger.LogDebug("Syncing {FileName} against Plex. (Library={LibraryID},User={UserID},Episode={EpisodeID},File={FileID},Plex={WatchedInPlex},Shoko={WatchedInShoko},InSync={IsInSync})", fileName, plexLibrary.Key, User.JMMUserID, episode.AnimeEpisodeID, file.VideoLocalID, isWatchedInPlex, isWatchedInShoko, inSync);
                    if (isWatchedInPlex && isWatchedInShoko)
                        // there is no way to sync plex to an exact date, so we can't do the opposite, afaik.
                        if (lastWatchedInPlex.Value > lastWatchedInShoko.Value)
                            file.ToggleWatchedStatus(true, true, lastWatchedInPlex, true, User.JMMUserID, true, true);
                    else if (isWatchedInShoko && !isWatchedInPlex)
                        plexEpisode.Scrobble();
                    else if (isWatchedInPlex && !isWatchedInShoko)
                        file.ToggleWatchedStatus(true, true, lastWatchedInPlex, true, User.JMMUserID, true, true);
                }
            }
        }
    }
    
    

    public override void GenerateCommandID()
    {
        CommandID = $"SyncPlex_{User.JMMUserID}";
    }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority7;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Syncing Plex for user: {0}",
        queueState = QueueStateEnum.SyncPlex,
        extraParams = new[] { User.Username }
    };

    public override bool LoadFromDBCommand(CommandRequest cq)
    {
        CommandID = cq.CommandID;
        CommandRequestID = cq.CommandRequestID;
        Priority = cq.Priority;
        CommandDetails = cq.CommandDetails;
        DateTimeUpdated = cq.DateTimeUpdated;
        User = RepoFactory.JMMUser.GetByID(Convert.ToInt32(cq.CommandDetails));
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
            CommandDetails = User.JMMUserID.ToString(CultureInfo.InvariantCulture),
            DateTimeUpdated = DateTime.Now
        };
        return cq;
    }

    private DateTime FromUnixTime(long unixTime)
    {
        return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddSeconds(unixTime);
    }

    private (SVR_AnimeEpisode, SVR_VideoLocal) GetAnimeEpisodeAndVideoLocalByFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return (null, null);

        return RepoFactory.VideoLocalPlace.GetAll()
            .Where(v => fileName.Equals(v?.FilePath?.Split(Path.DirectorySeparatorChar).LastOrDefault(), StringComparison.InvariantCultureIgnoreCase))
            .Select(a => RepoFactory.VideoLocal.GetByID(a.VideoLocalID))
            .Where(a => a != null)
            .SelectMany(file => RepoFactory.AnimeEpisode.GetByHash(file.Hash).Select(episode => (episode, file)))
            .Where(t => t.episode! != null)
            .FirstOrDefault(a => a.episode.AniDB_Episode.EpisodeType == (int)EpisodeType.Episode || a.episode.AniDB_Episode.EpisodeType == (int)EpisodeType.Special);
    }

    public CommandRequest_PlexSyncWatched(ILoggerFactory loggerFactory, ISettingsProvider settingsProvider) : base(loggerFactory)
    {
        _settingsProvider = settingsProvider;
    }

    protected CommandRequest_PlexSyncWatched()
    {
    }
}
