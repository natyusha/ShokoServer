using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Commands;
using Shoko.Server.Models.Internal;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using CommandRequestPriority = Shoko.Server.Server.Enums.CommandRequestPriority;
using File = Shoko.Server.API.v3.Models.Shoko.File;
using FileSortCriteria = Shoko.Server.API.v3.Models.Shoko.File.FileSortCriteria;
using Path = System.IO.Path;
using MediaInfo = Shoko.Server.API.v3.Models.Shoko.MediaInfo;
using DataSource = Shoko.Server.API.v3.Models.Common.DataSource;
using AbstractDataSource = Shoko.Plugin.Abstractions.Enums.DataSource;
using Shoko.Plugin.Abstractions.Models.Shoko;

namespace Shoko.Server.API.v3.Controllers;

[ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
[Authorize]
public class FileController : BaseController
{
    private const string FileUserStatsNotFoundWithFileID = "No FileUserStats entry for the given fileID for the current user";

    private const string FileNoPath = "Unable to get file path";

    private const string AnidbNotFoundForFileID = "No File.Anidb entry for the given fileID";

    internal const string FileNotFoundWithFileID = "No File entry for the given fileID";

    internal const string FileForbiddenForUser = "Accessing File is not allowed for the current user";

    private readonly TraktTVHelper _traktHelper;

    private readonly ICommandRequestFactory _commandFactory;

    public FileController(TraktTVHelper traktHelper, ICommandRequestFactory commandFactory, ISettingsProvider settingsProvider) : base(settingsProvider)
    {
        _traktHelper = traktHelper;
        _commandFactory = commandFactory;
    }

    /// <summary>
    /// Get or search through the files accessible to the current user.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeMissing">Include missing files among the results.</param>
    /// <param name="includeIgnored">Include ignored files among the results.</param>
    /// <param name="includeVariations">Include files marked as a variation among the results.</param>
    /// <param name="includeDuplicates">Include files with multiple locations (and thus have duplicates) among the results.</param>
    /// <param name="includeUnrecognized">Include unrecognized files among the results.</param>
    /// <param name="includeLinked">Include manually linked files among the results.</param>
    /// <param name="includeViewed">Include previously viewed files among the results.</param>
    /// <param name="includeWatched">Include previously watched files among the results</param>
    /// <param name="sortOrder">Sort ordering. Attach '-' at the start to reverse the order of the criteria.</param>
    /// <param name="includeXRefs">Include series and episode cross-references.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="search">An optional search query to filter files based on their absolute paths.</param>
    /// <param name="fuzzy">Indicates that fuzzy-matching should be used for the search query.</param>
    /// <returns>A sliced part of the results for the current query.</returns>
    [HttpGet]
    public ActionResult<ListResult<File>> GetFiles(
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] IncludeOnlyFilter includeMissing = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter includeIgnored = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeVariations = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter includeDuplicates = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter includeUnrecognized = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter includeLinked = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter includeViewed = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter includeWatched = IncludeOnlyFilter.True,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string> sortOrder = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] bool includeMediaInfo = false,
        [FromQuery] IncludeVideoXRefs includeXRefs = IncludeVideoXRefs.False,
        [FromQuery] string search = null,
        [FromQuery] bool fuzzy = true)
    {
        // Filtering.
        var user = User;
        var includeLocations = includeDuplicates != IncludeOnlyFilter.True ||
            !string.IsNullOrEmpty(search) ||
            (sortOrder?.Any(criteria => criteria.Contains(FileSortCriteria.DuplicateCount.ToString())) ?? false);
        var includeUserRecord = includeViewed != IncludeOnlyFilter.True ||
            includeWatched != IncludeOnlyFilter.True ||
            (sortOrder?.Any(criteria => criteria.Contains(FileSortCriteria.ViewedAt.ToString()) || criteria.Contains(FileSortCriteria.WatchedAt.ToString())) ?? false);
        var enumerable = RepoFactory.Shoko_Video.GetAll()
            .Select(video => (
                Video: video as IShokoVideo,
                BestLocation: video.GetPreferredLocation(includeMissing != IncludeOnlyFilter.True) as IShokoVideoLocation,
                Locations: includeLocations ? video.Locations as IReadOnlyList<IShokoVideoLocation> : null,
                UserRecord: includeUserRecord ? video.GetUserRecord(user.Id) : null
            ))
            .Where(tuple =>
            {
                var (video, bestLocation, locations, userRecord) = tuple;
                var xrefs = video.AllCrossReferences;
                var isAnimeAllowed = xrefs
                    .Select(xref => xref.AnidbAnimeId)
                    .Distinct()
                    .Select(anidbID => RepoFactory.AniDB_Anime.GetByAnidbAnimeId(anidbID))
                    .Where(anime => anime != null)
                    .All(user.AllowedAnime);
                if (!isAnimeAllowed)
                    return false;

                if (includeMissing != IncludeOnlyFilter.True)
                {
                    var shouldHideMissing = includeMissing == IncludeOnlyFilter.False;
                    var fileIsMissing = bestLocation == null;
                    if (shouldHideMissing == fileIsMissing)
                        return false;
                }

                if (includeIgnored != IncludeOnlyFilter.True)
                {
                    var shouldHideIgnored = includeIgnored == IncludeOnlyFilter.False;
                    if (shouldHideIgnored == video.IsIgnored)
                        return false;
                }

                if (includeVariations != IncludeOnlyFilter.True)
                {
                    var shouldHideVariation = includeVariations == IncludeOnlyFilter.False;
                    if (shouldHideVariation == video.IsVariation)
                        return false;
                }

                if (includeDuplicates != IncludeOnlyFilter.True)
                {
                    var shouldHideDuplicate = includeDuplicates == IncludeOnlyFilter.False;
                    var hasDuplicates = locations.Count > 1;
                    if (shouldHideDuplicate == hasDuplicates)
                        return false;
                }

                if (includeUnrecognized != IncludeOnlyFilter.True)
                {
                    var shouldHideUnrecognized = includeUnrecognized == IncludeOnlyFilter.False;
                    var fileIsUnrecognized = xrefs.Count == 0;
                    if (shouldHideUnrecognized == fileIsUnrecognized)
                        return false;
                }

                if (includeLinked != IncludeOnlyFilter.True)
                {
                    var shouldHideLinked = includeLinked == IncludeOnlyFilter.False;
                    var fileIsLinked = xrefs.Count > 0 && xrefs.Any(xref => xref.DataSource != AbstractDataSource.AniDB);
                    if (shouldHideLinked == fileIsLinked)
                        return false;
                }

                if (includeViewed != IncludeOnlyFilter.True)
                {
                    var shouldHideViewed = includeViewed == IncludeOnlyFilter.False;
                    var fileIsViewed = userRecord != null;
                    if (shouldHideViewed == fileIsViewed)
                        return false;
                }

                if (includeWatched != IncludeOnlyFilter.True)
                {
                    var shouldHideWatched = includeWatched == IncludeOnlyFilter.False;
                    var fileIsWatched = userRecord?.LastWatchedAt != null;
                    if (shouldHideWatched == fileIsWatched)
                        return false;
                }

                return true;
            });

        // Search.
        if (!string.IsNullOrEmpty(search))
            enumerable = enumerable
                .Search(search, tuple => tuple.Locations.Select(location => location.AbsolutePath).Where(path => path != null), fuzzy)
                .Select(result => result.Result);

        // Sorting.
        if (sortOrder != null && sortOrder.Count > 0)
            enumerable = Models.Shoko.File.OrderBy(enumerable, sortOrder);
        else if (string.IsNullOrEmpty(search))
            enumerable = Models.Shoko.File.OrderBy(enumerable, new()
            {
                // First sort by import folder from A-Z.
                FileSortCriteria.ImportFolderName.ToString(),
                // Then by the relative path inside the import folder, from A-Z.
                FileSortCriteria.RelativePath.ToString(),
            });

        // Skip and limit.
        return enumerable
            .ToListResult(tuple => new File(tuple.UserRecord, tuple.Video, includeXRefs, includeDataFrom, includeMediaInfo), page, pageSize);
    }

    /// <summary>
    /// Get File Details
    /// </summary>
    /// <param name="fileID">Shoko VideoLocalID</param>
    /// <param name="includeXRefs">Set to true to include series and episode cross-references.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <returns></returns>
    [HttpGet("{fileID}")]
    public ActionResult<File> GetFile([FromRoute] int fileID, [FromQuery] IncludeVideoXRefs includeXRefs = IncludeVideoXRefs.False,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] bool includeMediaInfo = false)
    {
        var file = RepoFactory.Shoko_Video.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        return new File(HttpContext, file, includeXRefs, includeDataFrom, includeMediaInfo);
    }

    /// <summary>
    /// Delete a file.
    /// </summary>
    /// <param name="fileID">The VideoLocal_Place ID. This cares about which location we are deleting from.</param>
    /// <param name="removeFiles">Remove all physical file locations.</param>
    /// <param name="removeFolder">This causes the empty folder removal to skipped if set to false.
    /// This significantly speeds up batch deleting if you are deleting many files in the same folder.
    /// It may be specified in the query.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("{fileID}")]
    public ActionResult DeleteFile([FromRoute] int fileID, [FromQuery] bool removeFiles = true, [FromQuery] bool removeFolder = true)
    {
        var file = RepoFactory.Shoko_Video.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        foreach (var location in file.Locations)
            if (removeFiles)
                location.RemoveRecordAndDeletePhysicalFile(removeFolder);
            else
                location.RemoveRecord();
        return Ok();
    }

    /// <summary>
    /// Get the <see cref="File.AniDB"/> using the <paramref name="fileID"/>.
    /// </summary>
    /// <remarks>
    /// This isn't a list because AniDB only has one File mapping even if there are multiple episodes.
    /// </remarks>
    /// <param name="fileID">Shoko File ID</param>
    /// <returns></returns>
    [HttpGet("{fileID}/AniDB")]
    public ActionResult<File.AniDB> GetFileAnidbByFileID([FromRoute] int fileID)
    {
        var file = RepoFactory.Shoko_Video.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var anidb = file.AniDB;
        if (anidb == null)
            return NotFound(AnidbNotFoundForFileID);

        return new File.AniDB(anidb);
    }

    /// <summary>
    /// Get the <see cref="File.AniDB"/> using the <paramref name="anidbFileID"/>.
    /// </summary>
    /// <remarks>
    /// This isn't a list because AniDB only has one File mapping even if there are multiple episodes.
    /// </remarks>
    /// <param name="anidbFileID">AniDB File ID</param>
    /// <returns></returns>
    [HttpGet("AniDB/{anidbFileID}")]
    public ActionResult<File.AniDB> GetFileAnidbByAnidbFileID([FromRoute] int anidbFileID)
    {
        var anidb = RepoFactory.AniDB_File.GetByFileID(anidbFileID);
        if (anidb == null)
            return NotFound(AnidbNotFoundForFileID);

        return new File.AniDB(anidb);
    }

    /// <summary>
    /// Get the <see cref="File.AniDB"/>for file using the <paramref name="anidbFileID"/>.
    /// </summary>
    /// <remarks>
    /// This isn't a list because AniDB only has one File mapping even if there are multiple episodes.
    /// </remarks>
    /// <param name="anidbFileID">AniDB File ID</param>
    /// <param name="includeXRefs">Set to true to include series and episode cross-references.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <returns></returns>
    [HttpGet("AniDB/{anidbFileID}/File")]
    public ActionResult<File> GetFileByAnidbFileID([FromRoute] int anidbFileID, [FromQuery] IncludeVideoXRefs includeXRefs = IncludeVideoXRefs.False,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] bool includeMediaInfo = false)
    {
        var anidb = RepoFactory.AniDB_File.GetByFileID(anidbFileID);
        if (anidb == null)
            return NotFound(FileNotFoundWithFileID);

        var file = RepoFactory.Shoko_Video.GetByED2K(anidb.ED2K);
        if (file == null)
            return NotFound(AnidbNotFoundForFileID);

        return new File(HttpContext, file, includeXRefs, includeDataFrom, includeMediaInfo);
    }

    /// <summary>
    /// Returns a file stream for the specified file ID.
    /// </summary>
    /// <param name="fileID">Shoko ID</param>
    /// <returns>A file stream for the specified file.</returns>
    [HttpGet("{fileID}/Stream")]
    public ActionResult GetFileStream([FromRoute] int fileID)
    {
        var file = RepoFactory.Shoko_Video.GetByID(fileID) as IShokoVideo;
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var bestLocation = file.PreferredLocation;

        var fileInfo = bestLocation.GetFileInfo();
        if (fileInfo == null)
            return InternalError("Unable to find physical file for reading the stream data.");

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(fileInfo.FullName, out var contentType))
            contentType = "application/octet-stream";

        return PhysicalFile(fileInfo.FullName, contentType, enableRangeProcessing: true);
    }

    /// <summary>
    /// Get the MediaInfo model for file with VideoLocal ID
    /// </summary>
    /// <param name="fileID">Shoko ID</param>
    /// <returns></returns>
    [HttpGet("{fileID}/MediaInfo")]
    public ActionResult<MediaInfo> GetFileMediaInfo([FromRoute] int fileID)
    {
        var file = RepoFactory.Shoko_Video.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var mediaContainer = file?.Media;
        if (mediaContainer == null)
            return InternalError("Unable to find media container for File");

        return new MediaInfo(mediaContainer);
    }

    /// <summary>
    /// Return the user stats for the file with the given <paramref name="fileID"/>.
    /// </summary>
    /// <param name="fileID">Shoko file ID</param>
    /// <returns>The user stats if found.</returns>
    [HttpGet("{fileID}/UserStats")]
    public ActionResult<File.FileUserStats> GetFileUserStats([FromRoute] int fileID)
    {
        var file = RepoFactory.Shoko_Video.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var user = HttpContext.GetUser(); 
        var userStats = file.GetUserRecord(user.Id);

        if (userStats == null)
            return NotFound(FileUserStatsNotFoundWithFileID);

        return new File.FileUserStats(userStats);
    }

    /// <summary>
    /// Put a <see cref="File.FileUserStats"/> object down for the <see cref="File"/> with the given <paramref name="fileID"/>.
    /// </summary>
    /// <param name="fileID">Shoko file ID</param>
    /// <param name="fileUserStats">The new and/or update file stats to put for the file.</param>
    /// <returns>The new and/or updated user stats.</returns>
    [HttpPut("{fileID}/UserStats")]
    public ActionResult<File.FileUserStats> PutFileUserStats([FromRoute] int fileID, [FromBody] File.FileUserStats fileUserStats)
    {
        // Make sure the file exists.
        var file = RepoFactory.Shoko_Video.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        // Get the user data.
        var user = HttpContext.GetUser(); 
        var userStats = file.GetOrCreateUserRecord(user.Id);

        // Merge with the existing entry and return an updated version of the stats.
        return fileUserStats.MergeWithExisting(userStats, file);
    }

    /// <summary>
    /// Mark a file as watched or unwatched.
    /// </summary>
    /// <param name="fileID">VideoLocal ID. Watched Status is kept per file, no matter how many copies or where they are.</param>
    /// <param name="watched">Is it watched?</param>
    /// <returns></returns>
    [HttpPost("{fileID}/Watched/{watched?}")]
    public ActionResult SetWatchedStatusOnFile([FromRoute] int fileID, [FromRoute] bool watched = true)
    {
        var file = RepoFactory.Shoko_Video.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        file.ToggleWatchedStatus(watched, User.Id);

        return Ok();
    }

    /// <summary>
    /// Update either watch status, resume position, or both.
    /// </summary>
    /// <param name="fileID">VideoLocal ID. Watch status and resume position is kept per file, regardless of how many duplicates the file has.</param>
    /// <param name="eventName">The name of the event that triggered the scrobble.</param>
    /// <param name="episodeID">The episode id to scrobble to trakt.</param>
    /// <param name="watched">True if file should be marked as watched, false if file should be unmarked, or null if it shall not be updated.</param>
    /// <param name="resumePosition">Number of ticks into the video to resume from, or null if it shall not be updated.</param>
    /// <returns></returns>
    [HttpPatch("{fileID}/Scrobble")]
    public ActionResult ScrobbleFileAndEpisode([FromRoute] int fileID, [FromQuery(Name = "event")] string eventName = null, [FromQuery] int? episodeID = null, [FromQuery] bool? watched = null, [FromQuery] long? resumePosition = null)
    {

        var file = RepoFactory.Shoko_Video.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        // Handle legacy scrobble events.
        if (string.IsNullOrEmpty(eventName))
        {
            return ScrobbleStatusOnFile(file, watched, resumePosition);
        }

        var episode = episodeID.HasValue ? RepoFactory.Shoko_Episode.GetByID(episodeID.Value) : file.GetEpisodes()?.FirstOrDefault();
        if (episode == null)
            return BadRequest("Could not get Episode with ID: " + episodeID);

        var playbackPositionTicks = resumePosition ?? 0;
        if (playbackPositionTicks >= file.Duration.TotalMilliseconds)
        {
            watched = true;
            playbackPositionTicks = 0;
        }

        switch (eventName)
        {
            // The playback was started.
            case "play":
            // The playback was resumed after a pause.
            case "resume":
                ScrobbleToTrakt(file, episode, playbackPositionTicks, ScrobblePlayingStatus.Start);
                break;
            // The playback was paused.
            case "pause":
                ScrobbleToTrakt(file, episode, playbackPositionTicks, ScrobblePlayingStatus.Pause);
                break;
            // The playback was ended.
            case "stop":
                ScrobbleToTrakt(file, episode, playbackPositionTicks, ScrobblePlayingStatus.Stop);
                break;
            // The playback is still active, but the playback position changed.
            case "scrobble":
                break;
            // A user interaction caused the watch state to change.
            case "user-interaction":
                break;
        }

        if (watched.HasValue)
            file.ToggleWatchedStatus(watched.Value, User.Id);
        file.SetResumePosition(playbackPositionTicks, User.Id);

        return NoContent();
    }

    [NonAction]
    private void ScrobbleToTrakt(Shoko_Video file, Shoko_Episode episode, long position, ScrobblePlayingStatus status)
    {
        if (User.IsTraktUser)
            return;

        float percentage = 100 * (position / (long) file.Duration.TotalMilliseconds);
        ScrobblePlayingType scrobbleType = episode.Series?.GetAnime().AnimeType == AnimeType.Movie
            ? ScrobblePlayingType.movie
            : ScrobblePlayingType.episode;

        _traktHelper.Scrobble(scrobbleType, episode.Id.ToString(), status, percentage);
    }

    [NonAction]
    private ActionResult ScrobbleStatusOnFile(Shoko_Video file, bool? watched, long? resumePosition)
    {
        if (!(watched ?? false) && resumePosition != null)
        {
            var safeRP = resumePosition ?? 0;
            if (safeRP < 0) safeRP = 0;

            if (safeRP >= file.Duration.TotalMilliseconds)
                watched = true;
            else
                file.SetResumePosition(safeRP, User.Id);
        }

        if (watched != null)
        {
            var safeWatched = watched ?? false;
            file.ToggleWatchedStatus(safeWatched, User.Id);
            if (safeWatched)
                file.SetResumePosition(0, User.Id);

        }

        return Ok();
    }

    /// <summary>
    /// Mark or unmark a file as ignored.
    /// </summary>
    /// <param name="fileID">VideoLocal ID</param>
    /// <param name="value">Thew new ignore value.</param>
    /// <returns></returns>
    [HttpPut("{fileID}/Ignore")]
    public ActionResult IgnoreFile([FromRoute] int fileID, [FromQuery] bool value = true)
    {
        var file = RepoFactory.Shoko_Video.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        file.IsIgnored = value;
        RepoFactory.Shoko_Video.Save(file, false);

        return Ok();
    }

    /// <summary>
    /// Run a file through AVDump and return the result.
    /// </summary>
    /// <param name="fileID">VideoLocal ID</param>
    /// <returns></returns>
    [HttpPost("{fileID}/AVDump")]
    public ActionResult<AVDumpResult> AvDumpFile([FromRoute] int fileID)
    {
        var file = RepoFactory.Shoko_Video.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var settings = SettingsProvider.GetSettings();
        if (string.IsNullOrWhiteSpace(settings.AniDb.AVDumpKey))
            return BadRequest("Missing AVDump API key");

        var filePath = file.GetPreferredLocation(true)?.AbsolutePath;
        if (string.IsNullOrEmpty(filePath))
            return BadRequest(FileNoPath);

        var result = AVDumpHelper.DumpFile(filePath).Replace("\r", "");

        return new AVDumpResult()
        {
            FullOutput = result,
            Ed2k = result.Split('\n').FirstOrDefault(s => s.Trim().Contains("ed2k://")),
        };
    }

    /// <summary>
    /// Rescan a file on AniDB.
    /// </summary>
    /// <param name="fileID">VideoLocal ID</param>
    /// <param name="priority">Increase the priority to the max for the queued command.</param>
    /// <returns></returns>
    [HttpPost("{fileID}/Rescan")]
    public ActionResult RescanFile([FromRoute] int fileID, [FromQuery] bool priority = false)
    {
        var file = RepoFactory.Shoko_Video.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var filePath = file.GetPreferredLocation(true)?.AbsolutePath;
        if (string.IsNullOrEmpty(filePath))
            return BadRequest(FileNoPath);

        var command = _commandFactory.Create<CommandRequest_ProcessFile>(
            c =>
            {
                c.VideoLocalID = file.Id;
                c.ForceAniDB = true;
            }
        );
        if (priority) command.Priority = (int) CommandRequestPriority.Priority1;
        command.Save();
        return Ok();
    }

    /// <summary>
    /// Rehash a file.
    /// </summary>
    /// <param name="fileID">VideoLocal ID</param>
    /// <returns></returns>
    [HttpPost("{fileID}/Rehash")]
    public ActionResult RehashFile([FromRoute] int fileID)
    {
        var file = RepoFactory.Shoko_Video.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var filePath = file.GetPreferredLocation(true)?.AbsolutePath;
        if (string.IsNullOrEmpty(filePath))
            return BadRequest(FileNoPath);

        var command = _commandFactory.Create<CommandRequest_HashFile>(
            c =>
            {
                c.FileName = filePath;
                c.ForceHash = true;
            }
        );
        command.Save();

        return Ok();
    }

    /// <summary>
    /// Link one or more episodes to the same file.
    /// </summary>
    /// <param name="fileID">The file id.</param>
    /// <param name="body">The body.</param>
    /// <returns></returns>
    [HttpPost("{fileID}/Link")]
    public ActionResult LinkSingleEpisodeToFile([FromRoute] int fileID, [FromBody] File.Input.LinkEpisodesBody body)
    {
        var file = RepoFactory.Shoko_Video.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        if (RemoveXRefsForFile(file))
            return BadRequest($"Cannot remove associations created from AniDB data for file '{file.Id}'");

        foreach (var episodeID in body.episodeIDs)
        {
            var episode = RepoFactory.Shoko_Episode.GetByID(episodeID);
            if (episode == null)
                return BadRequest("Could not find episode entry");

            var command = _commandFactory.Create<CommandRequest_LinkFileManually>(
                c =>
                {
                    c.VideoId = fileID;
                    c.EpisodeId = episode.Id;
                }
            );
            command.Save();
        }

        return Ok();
    }

    /// <summary>
    /// Link one or more episodes from a series to the same file.
    /// </summary>
    /// <param name="fileID">The file id.</param>
    /// <param name="body">The body.</param>
    /// <returns></returns>
    [HttpPost("{fileID}/LinkFromSeries")]
    public ActionResult LinkMultipleEpisodesToFile([FromRoute] int fileID, [FromBody] File.Input.LinkSeriesBody body)
    {
        var file = RepoFactory.Shoko_Video.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var series = RepoFactory.Shoko_Series.GetByID(body.seriesID);
        if (series == null)
            return BadRequest("Unable to find series entry");

        var episodeType = EpisodeType.Normal;
        var (rangeStart, startType, startErrorMessage) = Helpers.ModelHelper.GetEpisodeNumberAndTypeFromInput(body.rangeStart);
        if (!string.IsNullOrEmpty(startErrorMessage))
            return BadRequest(string.Format(startErrorMessage, "rangeStart"));

        var (rangeEnd, endType, endErrorMessage) = Helpers.ModelHelper.GetEpisodeNumberAndTypeFromInput(body.rangeEnd);
        if (!string.IsNullOrEmpty(endErrorMessage))
            return BadRequest(string.Format(endErrorMessage, "rangeEnd"));

        if (startType != endType)
            return BadRequest("Unable to use different episode types in the `rangeStart` and `rangeEnd`.");

        // Set the episode type if it was included in the input.
        if (startType.HasValue) episodeType = startType.Value;

        // Validate the range.
        var totalEpisodes = Helpers.ModelHelper.GetTotalEpisodesForType(series.GetEpisodes(), episodeType);
        if (rangeStart < 1)
            return BadRequest("`rangeStart` cannot be lower than 1");
        if (rangeStart > totalEpisodes)
            return BadRequest("`rangeStart` cannot be higher than the total number of episodes for the selected type.");
        if (rangeEnd < rangeStart)
            return BadRequest("`rangeEnd`cannot be lower than `rangeStart`.");
        if (rangeEnd > totalEpisodes)
            return BadRequest("`rangeEnd` cannot be higher than the total number of episodes for the selected type.");

        if (RemoveXRefsForFile(file))
            return BadRequest($"Cannot remove associations created from AniDB data for file '{file.Id}'");

        for (int episodeNumber = rangeStart; episodeNumber <= rangeEnd; episodeNumber++)
        {
            var anidbEpisode = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeTypeNumber(series.AniDB_ID, episodeType, episodeNumber)[0];
            if (anidbEpisode == null)
                return InternalError("Could not find the AniDB entry for episode");

            var episode = RepoFactory.Shoko_Episode.GetByAnidbEpisodeId(anidbEpisode.EpisodeId);
            if (episode == null)
                return InternalError("Could not find episode entry");

            var command = _commandFactory.Create<CommandRequest_LinkFileManually>(
                c =>
                {
                    c.VideoId = fileID;
                    c.EpisodeId = episode.Id;
                }
            );
            command.Save();
        }

        return Ok();
    }

    /// <summary>
    /// Unlink all the episodes if no body is given, or only the spesified episodes from the file.
    /// </summary>
    /// <param name="fileID">The file id.</param>
    /// <param name="body">Optional. The body.</param>
    /// <returns></returns>
    [HttpDelete("{fileID}/Link")]
    public ActionResult UnlinkMultipleEpisodesFromFile([FromRoute] int fileID, [FromBody] File.Input.UnlinkEpisodesBody body)
    {
        var file = RepoFactory.Shoko_Video.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var all = body == null;
        var episodeIdSet = body?.episodeIDs?.ToHashSet() ?? new();
        var seriesIDs = new HashSet<int>();
        foreach (var episode in file.GetEpisodes())
        {
            if (!all && !episodeIdSet.Contains(episode.Id)) continue;

            seriesIDs.Add(episode.SeriesId);
            var xref = RepoFactory.CR_Video_Episode.GetByED2KAndAnidbEpisodeId(file.ED2K, episode.AnidbEpisodeId);
            if (xref != null)
            {
                if (xref.CrossReferenceSource == CrossRefSource.AniDB)
                    return BadRequest($"Cannot remove associations created from AniDB data for file '{file.Id}'");

                RepoFactory.CR_Video_Episode.Delete(xref);
            }
        }

        if (file.LastImportedAt.HasValue)
        {
            // Reset the import date.
            file.LastImportedAt = null;
            RepoFactory.Shoko_Video.Save(file);
        }

        foreach (var seriesID in seriesIDs)
        {
            var series = RepoFactory.Shoko_Series.GetByID(seriesID);
            if (series != null)
                series.QueueUpdateStats();
        }

        return Ok();
    }

    /// <summary>
    /// Link multiple files to one or more episodes in a series.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns></returns>
    [HttpPost("LinkFromSeries")]
    public ActionResult LinkMultipleFiles([FromBody] File.Input.LinkSeriesMultipleBody body)
    {
        if (body.fileIDs.Length == 0)
            return BadRequest("`fileIDs` must contain at least one element.");

        // Validate all the file ids.
        var files = new List<Shoko_Video>(body.fileIDs.Length);
        for (int index = 0, fileID = body.fileIDs[0]; index < body.fileIDs.Length; fileID = body.fileIDs[++index])
        {
            var file = RepoFactory.Shoko_Video.GetByID(fileID);
            if (file == null)
                return BadRequest($"Unable to find file entry for `fileIDs[{index}]`.");

            files[index] = file;
        }

        var series = RepoFactory.Shoko_Series.GetByID(body.seriesID);
        if (series == null) return BadRequest("Unable to find series entry");

        var episodeType = EpisodeType.Normal;
        var (rangeStart, startType, startErrorMessage) = Helpers.ModelHelper.GetEpisodeNumberAndTypeFromInput(body.rangeStart);
        if (!string.IsNullOrEmpty(startErrorMessage))
        {
            return BadRequest(string.Format(startErrorMessage, "rangeStart"));
        }

        // Set the episode type if it was included in the input.
        if (startType.HasValue) episodeType = startType.Value;

        // Validate the range.
        var rangeEnd = rangeStart + files.Count - 1;
        var totalEpisodes = Helpers.ModelHelper.GetTotalEpisodesForType(series.GetEpisodes(), episodeType);
        if (rangeStart < 1)
        {
            return BadRequest("`rangeStart` cannot be lower than 1");
        }
        if (rangeStart > totalEpisodes)
        {
            return BadRequest("`rangeStart` cannot be higher than the total number of episodes for the selected type.");
        }
        if (rangeEnd < rangeStart)
        {
            return BadRequest("`rangeEnd`cannot be lower than `rangeStart`.");
        }
        if (rangeEnd > totalEpisodes)
        {
            return BadRequest("`rangeEnd` cannot be higher than the total number of episodes for the selected type.");
        }

        var fileCount = 1;
        var singleEpisode = body.singleEpisode;
        var episodeNumber = rangeStart;
        foreach (var file in files)
        {
            var anidbEpisode = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeTypeNumber(series.AniDB_ID, episodeType, episodeNumber)[0];
            if (anidbEpisode == null)
                return InternalError("Could not find the AniDB entry for episode");

            var episode = RepoFactory.Shoko_Episode.GetByAnidbEpisodeId(anidbEpisode.EpisodeId);
            if (episode == null)
                return InternalError("Could not find episode entry");

            if (RemoveXRefsForFile(file))
                return BadRequest($"Cannot remove associations created from AniDB data for file '{file.Id}'");

            var command = _commandFactory.Create<CommandRequest_LinkFileManually>(
                c =>
                {
                    c.VideoId = file.Id;
                    c.EpisodeId = episode.Id;
                }
            );
            if (singleEpisode)
                command.Percentage = (int)Math.Round((double)(fileCount / files.Count * 100));
            else
                episodeNumber++;

            fileCount++;
            command.Save();
        }

        return Ok();
    }

    /// <summary>
    /// Link multiple files to a single episode.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns></returns>
    [HttpPost("Link")]
    public ActionResult LinkMultipleFiles([FromBody] File.Input.LinkMultipleFilesBody body)
    {
        if (body.fileIDs.Length == 0)
            return BadRequest("`fileIDs` must contain at least one element.");

        // Validate all the file ids.
        var files = new List<Shoko_Video>();
        foreach (var fileID in body.fileIDs)
        {
            var file = RepoFactory.Shoko_Video.GetByID(fileID);
            if (file == null)
                return BadRequest($"Unable to find file entry for File ID {fileID}.");

            files.Add(file);
        }

        var episode = RepoFactory.Shoko_Episode.GetByID(body.episodeID);
        if (episode == null) return BadRequest("Unable to find episode entry");
        var anidbEpisode = episode.AniDB;
        if (anidbEpisode == null)
            return InternalError("Could not find the AniDB entry for episode");

        var fileCount = 1;
        foreach (var file in files)
        {
            if (RemoveXRefsForFile(file))
                return BadRequest($"Cannot remove associations created from AniDB data for file '{file.Id}'");

            var command = _commandFactory.Create<CommandRequest_LinkFileManually>(
                c =>
                {
                    c.VideoId = file.Id;
                    c.EpisodeId = episode.Id;
                }
            );
            command.Percentage = (int)Math.Round((double)(fileCount / files.Count * 100));

            fileCount++;
            command.Save();
        }

        return Ok();
    }

    [NonAction]
    private bool RemoveXRefsForFile(Shoko_Video file)
    {
        foreach (var xref in RepoFactory.CR_Video_Episode.GetByED2K(file.ED2K))
        {
            if (xref.CrossReferenceSource == CrossRefSource.AniDB)
                return true;

            RepoFactory.CR_Video_Episode.Delete(xref);
        }

        if (file.LastImportedAt.HasValue)
        {
            // Reset the import date.
            file.LastImportedAt = null;
            RepoFactory.Shoko_Video.Save(file);
        }

        return false;
    }

    /// <summary>
    /// Search for a file by path or name. Internally, it will convert forward
    /// slash (/) and backwards slash (\) to the system directory separator
    /// before matching.
    /// </summary>
    /// <param name="path">The path to search for.</param>
    /// <param name="includeXRefs">Set to true to include series and episode cross-references.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="limit">Limit the number of returned results.</param>
    /// <returns>A list of all files with a file location that ends with the given path.</returns>
    [HttpGet("PathEndsWith")]
    public ActionResult<List<File>> PathEndsWithQuery([FromQuery] string path, [FromQuery] IncludeVideoXRefs includeXRefs = IncludeVideoXRefs.V1,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [Range(0, 100)] int limit = 0)
        => PathEndsWithInternal(path, includeXRefs, includeDataFrom, limit);

    /// <summary>
    /// Search for a file by path or name. Internally, it will convert forward
    /// slash (/) and backwards slash (\) to the system directory separator
    /// before matching.
    /// </summary>
    /// <param name="path">The path to search for. URL encoded.</param>
    /// <param name="includeXRefs">Set to true to include series and episode cross-references.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="limit">Limit the number of returned results.</param>
    /// <returns>A list of all files with a file location that ends with the given path.</returns>
    [HttpGet("PathEndsWith/{*path}")]
    public ActionResult<List<File>> PathEndsWithPath([FromRoute] string path, [FromQuery] IncludeVideoXRefs includeXRefs = IncludeVideoXRefs.V1,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [Range(0, 100)] int limit = 0)
        => PathEndsWithInternal(Uri.UnescapeDataString(path), includeXRefs, includeDataFrom, limit);

    /// <summary>
    /// Search for a file by path or name. Internally, it will convert forward
    /// slash (/) and backwards slash (\) to the system directory separator
    /// before matching.
    /// </summary>
    /// <param name="path">The path to search for.</param>
    /// <param name="includeXRefs">Set to true to include series and episode cross-references.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="limit">Limit the number of returned results.</param>
    /// <returns>A list of all files with a file location that ends with the given path.</returns>
    internal ActionResult<List<File>> PathEndsWithInternal(string path, IncludeVideoXRefs includeXRefs,
        HashSet<DataSource> includeDataFrom, int limit = 0)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new List<File>();

        var user = User;
        var query = path
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var results = RepoFactory.Shoko_Video_Location.GetAll()
            .AsParallel()
            .Where(location => location.AbsolutePath?.EndsWith(query, StringComparison.OrdinalIgnoreCase) ?? false)
            .Select(location => location.Video)
            .Where(file =>
            {
                if (file == null)
                    return false;

                var xrefs = file.GetCrossReferences(true);
                var series = xrefs.Count > 0 ? xrefs[0].Series : null;
                return series == null || user.AllowedSeries(series);
            })
            .DistinctBy(file => file.Id);

        if (limit <= 0)
            return results
                .Select(a => new File(HttpContext, a, includeXRefs, includeDataFrom))
                .ToList();

        return results
            .Take(limit)
            .Select(a => new File(HttpContext, a, includeXRefs, includeDataFrom))
            .ToList();
    }

    /// <summary>
    /// Search for a file by path or name via regex. Internally, it will convert \/ to the system directory separator and match against the string
    /// </summary>
    /// <param name="path">a path to search for. URL Encoded</param>
    /// <returns></returns>
    [HttpGet("PathRegex/{*path}")]
    public ActionResult<List<File>> RegexSearchByPath([FromRoute] string path)
    {
        var query = path;
        if (query.Contains("%") || query.Contains("+")) query = Uri.UnescapeDataString(query);
        if (query.Contains("%")) query = Uri.UnescapeDataString(query);
        if (Path.DirectorySeparatorChar == '\\') query = query.Replace("\\/", "\\\\");
        Regex regex;

        try
        {
            regex = new Regex(query, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        }
        catch (RegexParseException e)
        {
            return BadRequest(e.Message);
        }

        var results = RepoFactory.Shoko_Video_Location.GetAll().AsParallel()
            .Where(a => regex.IsMatch(a.AbsolutePath)).Select(a => a.Video)
            .Distinct()
            .Where(a =>
            {
                var ser = a?.GetSeries().FirstOrDefault();
                return ser == null || User.AllowedSeries(ser);
            }).Select(a => new File(HttpContext, a, IncludeVideoXRefs.V1)).ToList();
        return results;
    }

    /// <summary>
    /// Search for a file by path or name via regex. Internally, it will convert \/ to the system directory separator and match against the string
    /// </summary>
    /// <param name="path">a path to search for. URL Encoded</param>
    /// <returns></returns>
    [HttpGet("FilenameRegex/{*path}")]
    public ActionResult<List<File>> RegexSearchByFileName([FromRoute] string path)
    {
        var query = path;
        if (query.Contains("%") || query.Contains("+")) query = Uri.UnescapeDataString(query);
        if (query.Contains("%")) query = Uri.UnescapeDataString(query);
        if (Path.DirectorySeparatorChar == '\\') query = query.Replace("\\/", "\\\\");
        Regex regex;

        try
        {
            regex = new Regex(query, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        }
        catch (RegexParseException e)
        {
            return BadRequest(e.Message);
        }

        var results = RepoFactory.Shoko_Video_Location.GetAll().AsParallel()
            .Where(a => regex.IsMatch(a.FileName)).Select(a => a.Video)
            .Distinct()
            .Where(a =>
            {
                var ser = a?.GetSeries().FirstOrDefault();
                return ser == null || User.AllowedSeries(ser);
            }).Select(a => new File(HttpContext, a, IncludeVideoXRefs.V1)).ToList();
        return results;
    }

    /// <summary>
    /// Get recently added files.
    /// </summary>
    /// <returns></returns>
    [HttpGet("Recent/{limit:int?}")]
    [Obsolete("Use the universal file endpoint instead.")]
    public ActionResult<ListResult<File>> GetRecentFilesObselete([FromRoute] [Range(0, 1000)] int limit = 100)
        => GetRecentFiles(limit);

    /// <summary>
    /// Get recently added files.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeXRefs">Set to false to exclude series and episode cross-references.</param>
    /// <returns></returns>
    [Obsolete("Use the universal file endpoint instead.")]
    [HttpGet("Recent")]
    public ActionResult<ListResult<File>> GetRecentFiles([FromQuery] [Range(0, 1000)] int pageSize = 100, [FromQuery] [Range(1, int.MaxValue)] int page = 1, [FromQuery] IncludeVideoXRefs includeXRefs = IncludeVideoXRefs.V1)
    {
        return RepoFactory.Shoko_Video.GetMostRecentlyAdded(-1, 0, User.Id)
            .ToListResult(file => new File(HttpContext, file, includeXRefs), page, pageSize);
    }

    /// <summary>
    /// Get ignored files.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [Obsolete("Use the universal file endpoint instead.")]
    [HttpGet("Ignored")]
    public ActionResult<ListResult<File>> GetIgnoredFiles([FromQuery] [Range(0, 1000)] int pageSize = 100, [FromQuery] [Range(1, int.MaxValue)] int page = 1)
    {
        return RepoFactory.Shoko_Video.GetIgnoredVideos()
            .ToListResult(file => new File(HttpContext, file), page, pageSize);
    }

    /// <summary>
    /// Get files with more than one location.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeXRefs">Set to true to include series and episode cross-references.</param>
    /// <returns></returns>
    [Obsolete("Use the universal file endpoint instead.")]
    [HttpGet("Duplicates")]
    public ActionResult<ListResult<File>> GetExactDuplicateFiles([FromQuery] [Range(0, 1000)] int pageSize = 100, [FromQuery] [Range(1, int.MaxValue)] int page = 1, [FromQuery] IncludeVideoXRefs includeXRefs = IncludeVideoXRefs.False)
    {
        return RepoFactory.Shoko_Video.GetExactDuplicateVideos()
            .ToListResult(file => new File(HttpContext, file, includeXRefs), page, pageSize);
    }

    /// <summary>
    /// Get files with no cross-reference.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeXRefs">Set to false to exclude series and episode cross-references.</param>
    /// <returns></returns>
    [Obsolete("Use the universal file endpoint instead.")]
    [HttpGet("Linked")]
    public ActionResult<ListResult<File>> GetManuellyLinkedFiles([FromQuery] [Range(0, 1000)] int pageSize = 100, [FromQuery] [Range(1, int.MaxValue)] int page = 1, [FromQuery] IncludeVideoXRefs includeXRefs = IncludeVideoXRefs.True)
    {
        return RepoFactory.Shoko_Video.GetManuallyLinkedVideos()
            .ToListResult(file => new File(HttpContext, file, includeXRefs), page, pageSize);
    }

    /// <summary>
    /// Get all files with missing cross-references data.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeXRefs">Set to false to exclude series and episode cross-references.</param>
    /// <returns></returns>
    [HttpGet("MissingCrossReferenceData")]
    public ActionResult<ListResult<File>> GetFilesWithMissingCrossReferenceData([FromQuery] [Range(0, 1000)] int pageSize = 100, [FromQuery] [Range(1, int.MaxValue)] int page = 1, [FromQuery] bool includeXRefs = true)
    {
        return RepoFactory.Shoko_Video.GetVideosWithMissingCrossReferenceData()
            .ToListResult(
                file => new File(HttpContext, file)
                {
                    SeriesIDs = includeXRefs ? file.GetCrossReferences(false)
                        .GroupBy(xref => xref.AnidbAnimeId, xref => new Models.Shoko.EpisodeIDs(xref))
                        .Select(tuples => new File.SeriesCrossReference { SeriesID = new() { AniDB = tuples.Key }, EpisodeIDs = tuples.ToList() })
                        .ToList() : null
                },
                page,
                pageSize
            );
    }

    /// <summary>
    /// Get unrecognized files.
    /// Use pageSize and page (index 0) in the query to enable pagination.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [Obsolete("Use the universal file endpoint instead.")]
    [HttpGet("Unrecognized")]
    public ActionResult<ListResult<File>> GetUnrecognizedFiles([FromQuery] [Range(0, 1000)] int pageSize = 100, [FromQuery] [Range(1, int.MaxValue)] int page = 1)
    {
        return RepoFactory.Shoko_Video.GetVideosWithoutEpisode()
            .ToListResult(file => new File(HttpContext, file), page, pageSize);
    }
}
