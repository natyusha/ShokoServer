using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.AniDB;
using Shoko.Plugin.Abstractions.Models.Shoko;
using Shoko.Server.API.Converters;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.Internal;
using Shoko.Server.Repositories;

using DataSource = Shoko.Server.API.v3.Models.Common.DataSource;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

public class File
{
    /// <summary>
    /// The ID of the File. You'll need this to play it.
    /// </summary>
    public int ID { get; set; }

    /// <summary>
    /// The v1 cross-references where every episode this file belongs to is
    /// added in a reverse tree where we go from the series then episodes.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<SeriesCrossReference>? SeriesIDs { get; set; }

    /// <summary>
    /// The v2 cross-references where everything is flat. Also contains more
    /// cross-reference related metadata such as the ordering, percentage,
    /// release group (if any) and source.
    /// </summary>
    /// <value></value>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<FileCrossReference>? CrossReferences { get; set; }

    /// <summary>
    /// The Filesize in bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// If this file is marked as a file variation.
    /// </summary>
    public bool IsVariation { get; set; }

    /// <summary>
    /// The calculated hashes of the file
    /// </summary>
    /// <returns></returns>
    public IHashes Hashes { get; set; }

    /// <summary>
    /// All of the Locations that this file exists in
    /// </summary>
    public IReadOnlyList<Location> Locations { get; set; }

    /// <summary>
    /// Try to fit this file's resolution to something like 1080p, 480p, etc
    /// </summary>
    public string Resolution { get; set; }

    /// <summary>
    /// The duration of the file.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Where to resume the next playback.
    /// </summary>
    public TimeSpan? ResumePosition { get; set; }

    /// <summary>
    /// The last watched date for the current user. Is null if unwatched
    /// </summary>
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime? Watched { get; set; }

    /// <summary>
    /// When the file was last imported. Usually is a file only imported once,
    /// but there may be exceptions.
    /// </summary>
    public DateTime? Imported { get; set; }

    /// <summary>
    /// The file creation date of this file
    /// </summary>
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime Created { get; set; }

    /// <summary>
    /// When the file was last updated (e.g. the hashes were added/updated).
    /// </summary>
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime Updated { get; set; }

    /// <summary>
    /// The <see cref="File.AniDB"/>, if <see cref="DataSource.AniDB"/> is
    /// included in the data to add.
    /// </summary>
    [JsonProperty("AniDB", NullValueHandling = NullValueHandling.Ignore)]
    public AniDB? AniDBFile { get; set; }

    /// <summary>
    /// The <see cref="MediaInfo"/>, if to-be included in the response data.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public MediaInfo? MediaInfo { get; set; }

    public File(HttpContext context, IShokoVideo file, IncludeVideoXRefs withXRefs = IncludeVideoXRefs.False, HashSet<DataSource>? includeDataFrom = null, bool includeMediaInfo = false) :
        // TODO: Replace this cast once user data is added to the abstraction.
        this((file as Shoko_Video)!.GetUserRecord(context?.GetUser()?.Id ?? 0), file, withXRefs, includeDataFrom, includeMediaInfo)
    { }

    public File(Shoko_Video_User? userRecord, IShokoVideo file, IncludeVideoXRefs withXRefs = IncludeVideoXRefs.False, HashSet<DataSource>? includeDataFrom = null, bool includeMediaInfo = false)
    {
        var hashes = file.Hashes;
        ID = file.Id;
        Size = file.Size;
        IsVariation = file.IsVariation;
        Hashes = hashes;
        Resolution = file.Resolution;
        Locations = file.AllLocations
            .Select(a => new Location(a))
            .ToList();
        Duration = file.Duration;
        ResumePosition = userRecord?.ResumePosition;
        Watched = userRecord?.LastWatchedAt;
        Imported = file.LastImportedAt;
        Created = file.CreatedAt;
        Updated = file.LastUpdatedAt;
        switch (withXRefs)
        {
            case IncludeVideoXRefs.V1:
                {
                    var episodes = file.AllEpisodes;
                    if (episodes.Count == 0)
                        break;
                    var seriesDict = file.AllSeries.ToDictionary(series => series.Id);
                    SeriesIDs = episodes
                        .GroupBy(episode => episode.SeriesId, episode => new EpisodeIDs(episode))
                        .Select(tuples =>
                        {
                            if (seriesDict.TryGetValue(tuples.Key, out var series))
                            {
                                return new SeriesCrossReference
                                {
                                    SeriesID = new(series),
                                    EpisodeIDs = tuples.ToList(),
                                };
                            }

                            return new SeriesCrossReference
                            {
                                EpisodeIDs = tuples.ToList(),
                            };
                        })
                        .ToList();
                    break;
                }
            case IncludeVideoXRefs.V2:
                CrossReferences = file.AllCrossReferences
                    .Select(xref => new FileCrossReference(xref))
                    .ToList();
                break;
        }

        if (includeDataFrom?.Contains(DataSource.AniDB) ?? false)
        {
            var anidbFile = file.AnidbFile;
            if (anidbFile != null)
                this.AniDBFile = new File.AniDB(anidbFile);
        }

        if (includeMediaInfo)
        {
            var mediaContainer = file?.Media;
            if (mediaContainer == null)
                throw new Exception("Unable to find media container for File");
            MediaInfo = new MediaInfo(mediaContainer);
        }
    }

    public class Location
    {
        /// <summary>
        /// Shoko file location id.
        /// </summary>
        public int ID { get; }

        /// <summary>
        /// Shoko file id.
        /// </summary>
        public int FileID { get; }

        /// <summary>
        /// The Import Folder that this file resides in.
        /// </summary>
        public int ImportFolderID { get; }

        /// <summary>
        /// The relative path from the import folder's path on the server.
        /// </summary>
        public string RelativePath { get; }

        /// <summary>
        /// Can the server access the file right now
        /// </summary>
        [JsonRequired]
        public bool IsAccessible { get; }

        public Location(IShokoVideoLocation location)
        {
            ID = location.Id;
            FileID = location.VideoId;
            ImportFolderID = location.ImportFolderId;
            RelativePath = location.RelativePath;
            IsAccessible = location.IsAccessible;
        }
    }

    /// <summary>
    /// AniDB_File info
    /// </summary>
    public class AniDB
    {
        /// <summary>
        /// The AniDB File ID
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Blu-ray, DVD, LD, TV, etc
        /// </summary>
        public FileSource Source { get; set; }

        /// <summary>
        /// The Release Group. This is usually set, but sometimes is set as "raw/unknown"
        /// </summary>
        public ReleaseGroup? ReleaseGroup { get; set; }

        /// <summary>
        /// The file's release date. This is probably not filled in
        /// </summary>
        [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
        public DateTime? ReleaseDate { get; set; }

        /// <summary>
        /// The file's version, Usually 1, sometimes more when there are edits released later
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Is the file marked as deprecated. Generally, yes if there's a V2, and this isn't it
        /// </summary>
        public bool IsDeprecated { get; set; }

        /// <summary>
        /// Mostly applicable to hentai, but on occasion a TV release is censored enough to earn this.
        /// </summary>
        public bool? IsCensored { get; set; }

        /// <summary>
        /// Does the file have chapters. This may be wrong, since it was only
        /// added in AVDump2 (a more recent version at that).
        /// </summary>
        public bool IsChaptered { get; set; }

        /// <summary>
        /// The original FileName. Useful for when you obtained from a shady source or when you renamed it without thinking. 
        /// </summary>
        public string OriginalFileName { get; set; }

        /// <summary>
        /// The reported FileSize. If you got this far and it doesn't match, something very odd has occurred
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// The reported duration of the file
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Any comments that were added to the file, such as something wrong with it.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Audio languages.
        /// </summary>
        public IReadOnlyList<TextLanguage> AudioLanguages { get; set; }

        /// <summary>
        /// Subtitle languages.
        /// </summary>
        public IReadOnlyList<TextLanguage> SubLanguages { get; set; }

        /// <summary>
        /// When we last got data on this file
        /// </summary>
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime Updated { get; set; }

        public AniDB(IAniDBFile anidb)
        {
            var releaseGroup = anidb.ReleaseGroup;
            var mediaInfo = anidb.Media;
            ID = anidb.Id;
            Source = anidb.Source;
            ReleaseGroup = releaseGroup != null ? new ReleaseGroup(releaseGroup) : null;
            ReleaseDate = anidb.ReleasedAt;
            Version = anidb.FileVersion;
            IsDeprecated = anidb.IsDeprecated;
            IsCensored = anidb.IsCensored;
            IsChaptered = anidb.IsChaptered;
            OriginalFileName = anidb.OriginalFileName;
            FileSize = anidb.FileSize;
            Description = anidb.Comment;
            Updated = anidb.LastUpdatedAt;
            AudioLanguages = mediaInfo?.AudioLanguages ?? new List<TextLanguage>();
            SubLanguages = mediaInfo?.SubtitleLanguages ?? new List<TextLanguage>();
        }
    }

    public class SeriesCrossReference
    {
        /// <summary>
        /// The Series IDs
        /// </summary>
        public SeriesIDs? SeriesID { get; set; }

        /// <summary>
        /// The Episode IDs
        /// </summary>
        public List<EpisodeIDs>? EpisodeIDs { get; set; }
    }

    public class FileCrossReference
    {
        /// <summary>
        /// File/episode cross-reference id.
        /// </summary>
        public int ID { get; }

        /// <summary>
        /// The shoko video id.
        /// </summary>
        public int FileID { get; }

        /// <summary>
        /// The shoko episode id.
        /// </summary>
        public int EpisodeID { get; }

        /// <summary>
        /// The anidb episode id.
        /// </summary>
        public int AniDBEpisodeID { get; }

        /// <summary>
        /// The shoko series id.
        /// </summary>
        public int SeriesID { get; }

        /// <summary>
        /// The anidb anime id.
        /// </summary>
        public int AniDBAnimeID { get; }

        /// <summary>
        /// The ordering index for this cross-reference relative to the other
        /// cross-references linked to the file.
        /// </summary>
        /// <value></value>
        public int Order { get; }

        /// <summary>
        /// If the file is linked to multiple episodes, then this percentage
        /// tells us how muc
        /// </summary>
        public decimal Percentage { get; }

        /// <summary>
        /// The release group assosiated with this cross-reference.
        /// </summary>
        /// <value></value>
        public ReleaseGroup? ReleaseGroup { get; }

        /// <summary>
        /// The source of the cross-reference.
        /// </summary>
        public DataSource Source { get; }

        public FileCrossReference(IShokoVideoCrossReference xref)
        {
            var releaseGroup = xref.ReleaseGroup;

            ID = xref.Id;
            FileID = xref.VideoId;
            EpisodeID = xref.EpisodeId;
            AniDBEpisodeID = xref.AnidbEpisodeId;
            SeriesID = xref.SeriesId;
            AniDBAnimeID = xref.AnidbAnimeId;
            Order = xref.Order;
            Percentage = xref.Percentage;
            ReleaseGroup = releaseGroup is not null ? new ReleaseGroup(releaseGroup) : null;
            Source = (DataSource)xref.DataSource;
        }
    }

    /// <summary>
    /// User stats for the file.
    /// </summary>
    public class FileUserStats
    {
        public FileUserStats()
        {
            ResumePosition = TimeSpan.Zero;
            WatchedCount = 0;
            LastWatchedAt = null;
            LastUpdatedAt = DateTime.Now;
        }

        public FileUserStats(Shoko_Video_User userStats)
        {
            ResumePosition = userStats.ResumePosition;
            WatchedCount = userStats.WatchedCount;
            LastWatchedAt = userStats.LastWatchedAt;
            LastUpdatedAt = userStats.LastUpdatedAt;
        }

        public FileUserStats MergeWithExisting(Shoko_Video_User existing, Shoko_Video file)
        {
            // Sync the watch date and aggregate the data up to the episode if needed.
            file.ToggleWatchedStatus(LastWatchedAt.HasValue, true, LastWatchedAt, true, existing.UserId, true, true, LastUpdatedAt);

            // Update the rest of the data. The watch count have been bumped when toggling the watch state, so set it to it's intended value.
            existing.WatchedCount = WatchedCount;
            existing.ResumePosition = ResumePosition;
            RepoFactory.Shoko_Video_User.Save(existing);

            // Return a new representation
            return new FileUserStats(existing);
        }

        /// <summary>
        /// Where to resume the next playback.
        /// </summary>
        public TimeSpan? ResumePosition { get; set; }

        /// <summary>
        /// Total number of times the file have been watched.
        /// </summary>
        public int WatchedCount { get; set; }

        /// <summary>
        /// When the file was last watched. Will be null if the full is
        /// currently marked as unwatched.
        /// </summary>
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime? LastWatchedAt { get; set; }

        /// <summary>
        /// When the entry was last updated.
        /// </summary>
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime LastUpdatedAt { get; set; }
    }

    /// <summary>
    /// Input models.
    /// </summary>
    public class Input
    {
        /// <summary>
        /// Link a file to multiple episodes.
        /// </summary>
        public class LinkEpisodesBody
        {
            /// <summary>
            /// An array of episode identifiers to link to the file.
            /// </summary>
            /// <value></value>
            [Required]
            public int[] episodeIDs { get; set; } = new int[] { };
        }

        /// <summary>
        /// Link a file to multiple episodes.
        /// </summary>
        public class LinkMultipleFilesBody
        {
            /// <summary>
            /// An array of file identifiers to link in batch.
            /// </summary>
            /// <value></value>
            [Required]
            public int[] fileIDs { get; set; } = new int[] { };

            /// <summary>
            /// The episode identifier.
            /// </summary>
            /// <value></value>
            [Required]
            public int episodeID { get; set; }
        }

        /// <summary>
        /// Link a file to an episode range in a series.
        /// </summary>
        public class LinkSeriesBody
        {
            /// <summary>
            /// The series identifier.
            /// </summary>
            /// <value></value>
            [Required]
            public int seriesID { get; set; }

            /// <summary>
            /// The start of the range of episodes to link to the file. Append a type prefix to use another episode type.
            /// </summary>
            /// <value></value>
            [Required]
            public string rangeStart { get; set; } = string.Empty;

            /// <summary>
            /// The end of the range of episodes to link to the file. The prefix used should be the same as in <see cref="rangeStart"/>.
            /// </summary>
            /// <value></value>
            [Required]
            public string rangeEnd { get; set; } = string.Empty;
        }

        /// <summary>
        /// Link multiple files to an episode range in a series.
        /// </summary>
        public class LinkSeriesMultipleBody
        {
            /// <summary>
            /// An array of file identifiers to link in batch.
            /// </summary>
            /// <value></value>
            [Required]
            public int[] fileIDs { get; set; } = new int[] { };

            /// <summary>
            /// The series identifier.
            /// </summary>
            /// <value></value>
            [Required]
            public int seriesID { get; set; }

            /// <summary>
            /// The start of the range of episodes to link to the file. Append a type prefix to use another episode type.
            /// </summary>
            /// <value></value>
            [Required]
            public string rangeStart { get; set; } = string.Empty;

            /// <summary>
            /// If true then files will be linked to a single episode instead of a range spanning the amount of files to add.
            /// </summary>
            /// <value></value>
            [DefaultValue(false)]
            public bool singleEpisode { get; set; }
        }

        /// <summary>
        /// Unlink the spesified episodes from a file.
        /// </summary>
        public class UnlinkEpisodesBody
        {
            /// <summary>
            /// An array of episode identifiers to unlink from the file.
            /// </summary>
            /// <value></value>
            [Required]
            public int[] episodeIDs { get; set; } = new int[] { };
        }

        /// <summary>
        /// Unlink multiple files in batch.
        /// </summary>
        public class UnlinkMultipleBody
        {
            /// <summary>
            /// An array of file identifiers to unlink in batch.
            /// </summary>
            /// <value></value>
            [Required]
            public int[] fileIDs { get; set; } = new int[] { };
        }
    }
    public enum FileSortCriteria
    {
        None = 0,
        ImportFolderName = 1,
        ImportFolderID = 2,
        AbsolutePath = 3,
        RelativePath = 4,
        FileSize = 5,
        DuplicateCount = 6,
        CreatedAt = 7,
        ImportedAt = 8,
        ViewedAt = 9,
        WatchedAt = 10,
        ED2K = 11,
        MD5 = 12,
        SHA1 = 13,
        CRC32 = 14,
        FileName = 15,
        FileID = 16,
    }

#pragma warning disable 8603
    private static Func<(IShokoVideo Video, IShokoVideoLocation Location, IReadOnlyList<IShokoVideoLocation> Locations, Shoko_Video_User? UserRecord), object?> GetOrderFunction(FileSortCriteria criteria, bool isInverted) =>
        criteria switch
        {
            FileSortCriteria.ImportFolderName => (tuple) => tuple.Location?.ImportFolder?.Name ?? string.Empty,
            FileSortCriteria.ImportFolderID => (tuple) => tuple.Location?.ImportFolderId,
            FileSortCriteria.AbsolutePath => (tuple) => tuple.Location?.AbsolutePath,
            FileSortCriteria.RelativePath => (tuple) => tuple.Location?.RelativePath,
            FileSortCriteria.FileSize => (tuple) => tuple.Video.Size,
            FileSortCriteria.FileName => (tuple) => tuple.Location?.FileName,
            FileSortCriteria.FileID => (tuple) => tuple.Video.Id,
            FileSortCriteria.DuplicateCount => (tuple) => tuple.Locations.Count,
            FileSortCriteria.CreatedAt => (tuple) => tuple.Video.CreatedAt,
            FileSortCriteria.ImportedAt => isInverted ? (tuple) => tuple.Video.LastImportedAt ?? DateTime.MinValue : (tuple) => tuple.Video.LastImportedAt ?? DateTime.MaxValue,
            FileSortCriteria.ViewedAt => isInverted ? (tuple) => tuple.UserRecord?.LastUpdatedAt ?? DateTime.MinValue : (tuple) => tuple.UserRecord?.LastUpdatedAt ?? DateTime.MaxValue,
            FileSortCriteria.WatchedAt => isInverted ? (tuple) => tuple.UserRecord?.LastWatchedAt ?? DateTime.MinValue : (tuple) => tuple.UserRecord?.LastWatchedAt ?? DateTime.MaxValue,
            FileSortCriteria.ED2K => (tuple) => tuple.Video.Hashes.ED2K,
            FileSortCriteria.MD5 => (tuple) => tuple.Video.Hashes.MD5,
            FileSortCriteria.SHA1 => (tuple) => tuple.Video.Hashes.SHA1,
            FileSortCriteria.CRC32 => (tuple) => tuple.Video.Hashes.CRC32,
            _ => null,
        };
#pragma warning restore 8603

    public static IEnumerable<(IShokoVideo, IShokoVideoLocation, IReadOnlyList<IShokoVideoLocation>, Shoko_Video_User?)> OrderBy(IEnumerable<(IShokoVideo, IShokoVideoLocation, IReadOnlyList<IShokoVideoLocation>, Shoko_Video_User?)> enumerable, List<string> sortCriterias)
    {
        bool first = true;
        return sortCriterias.Aggregate(enumerable, (current, rawSortCriteria) =>
        {
            // Any unrecognised criterias are ignored.
            var (sortCriteria, isInverted) = ParseSortCriteria(rawSortCriteria);
            var orderFunc = GetOrderFunction(sortCriteria, isInverted);
            if (orderFunc == null)
                return current;

            // First criteria in the list.
            if (first)
            {
                first = false;
                return isInverted ? enumerable.OrderByDescending(orderFunc) : enumerable.OrderBy(orderFunc);
            }

            // All other criterias in the list.
            var ordered = (current as IOrderedEnumerable<(IShokoVideo, IShokoVideoLocation, IReadOnlyList<IShokoVideoLocation>, Shoko_Video_User?)>)!;
            return isInverted ? ordered.ThenByDescending(orderFunc) : ordered.ThenBy(orderFunc);
        });
    }

    private static (FileSortCriteria criteria, bool isInverted) ParseSortCriteria(string input)
    {
        var isInverted = false;
        if (input[0] == '-')
        {
            isInverted = true;
            input = input[1..];
        }

        if (!Enum.TryParse<FileSortCriteria>(input, ignoreCase: true, out var sortCriteria))
            sortCriteria = FileSortCriteria.None;

        return (sortCriteria, isInverted);
    }
}

