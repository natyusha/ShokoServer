using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Models.MediaInfo;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.AniDB;
using Shoko.Plugin.Abstractions.Models.Implementations;
using Shoko.Plugin.Abstractions.Models.Shoko;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.FileHelper.Subtitles;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReferences;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Server.Enums;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Models.Internal;

public class Shoko_Video : IShokoVideo
{
    #region Database Columns

    #region Identifiers

    /// <inheritdoc/>
    public int Id { get; set; }

    /// <summary>
    /// AniDB MyList Id linked to the file.
    /// </summary>
    public int AniDBMyListId { get; set; }

    #endregion

    #region Hashes

    private string _ed2k { get; set; } = string.Empty;

    /// <summary>
    /// ED2K hash. Required by AniDB to match the file, so it will always be
    /// present.
    /// </summary>
    public string ED2K
    {
        get => _ed2k;
        set => _ed2k = value.ToUpperInvariant();
    }

    private string? _crc32 { get; set; }

    /// <summary>
    /// Optional CRC32 hash, to-be used by plugins for whatever reasons. It's
    /// optional since it can be disabled in the settings, and thus may not
    /// always be present.
    /// </summary>
    public string? CRC32
    {
        get => string.IsNullOrEmpty(_crc32) ? null : _crc32;
        set => _crc32 = string.IsNullOrEmpty(value) ? null : value.ToUpperInvariant();
    }

    private string? _md5 { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string? MD5
    {
        get => string.IsNullOrEmpty(_md5) ? null : _md5;
        set => _md5 = string.IsNullOrEmpty(value) ? null : value.ToUpperInvariant();
    }

    private string? _sha1 { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string? SHA1
    {
        get => string.IsNullOrEmpty(_sha1) ? null : _sha1;
        set => _sha1 = string.IsNullOrEmpty(value) ? null : value.ToUpperInvariant();
    }

    #endregion

    #region Other Metadata

    private string? _fileName { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [Obsolete("Use ShokoVideoLocation.FileName instead")]
    public string FileName
    {
        get => _fileName ?? string.Empty;
        set => _fileName = value;
    }

    /// <inheritdoc/>
    public long Size { get; set; }

    /// <inheritdoc/>
    public bool IsIgnored { get; set; }

    /// <inheritdoc/>
    public bool IsVariation { get; set; }

    /// <inheritdoc/>
    public DateTime CreatedAt { get; set; }

    /// <inheritdoc/>
    public DateTime LastUpdatedAt { get; set; }

    /// <inheritdoc/>
    public DateTime? LastImportedAt { get; set; }

    #endregion

    #region Media info

    /// <summary>
    /// We increase this version every time the media blob format changes.
    /// </summary>
    public const int MEDIA_VERSION = 5;

    /// <summary>
    /// The format version for the media blob stored with the record.
    /// </summary>
    public int MediaVersion { get; set; }

    /// <summary>
    /// The media info blob.
    /// </summary>
    /// <value></value>
    public byte[]? MediaBlob { get; set; }

    /// <summary>
    /// The size of the media info blob.
    /// </summary>
    /// <value></value>
    public int MediaSize { get; set; }

    #endregion

    #endregion

    #region Helpers

    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    /// <inheritdoc/>
    public string Resolution
    {
        get
        {
            var stream = Media?.VideoStream;
            if (stream != null)
            {
                var width = stream.Width;
                var height = stream.Height;
                if (height > 0 && width > 0)
                {
                    return MediaInfoUtils.GetStandardResolution(new Tuple<int, int>(width, height));
                }
            }

            return "unknown";
        }
    }

    /// <inheritdoc/>
    public TimeSpan Duration
    {
        get
        {
            var duration = Media?.GeneralStream?.Duration ?? 0;
            var seconds = Math.Truncate(duration);
            var milliseconds = (duration - seconds) * 1000;
            return new TimeSpan(0, 0, 0, (int)seconds, (int)milliseconds);
        }
    }

    public Shoko_Video_Location? GetPreferredLocation(bool resolve = false) =>
        GetLocations(resolve).FirstOrDefault();

    public FileInfo? GetPreferredFileInfo() =>
        GetPreferredLocation(true)?.GetFileInfo();

    public AniDB_File? AniDB =>
        GetAniDB();

    public AniDB_File? GetAniDB() =>
        RepoFactory.AniDB_File.GetByED2K(ED2K);

    public IReadOnlyList<Shoko_Video_Location> Locations =>
        Id <= 0 ? Array.Empty<Shoko_Video_Location>() :
        RepoFactory.Shoko_Video_Location.GetByVideoId(Id);

    public IReadOnlyList<Shoko_Video_Location> GetLocations(bool resolve) =>
        Id <= 0 ? Array.Empty<Shoko_Video_Location>() :
        RepoFactory.Shoko_Video_Location.GetByVideoId(Id, resolve);

    public IReadOnlyList<CR_Video_Episode> GetCrossReferences(bool resolve) =>
        string.IsNullOrEmpty(ED2K) ? Array.Empty<CR_Video_Episode>() :
        RepoFactory.CR_Video_Episode.GetByED2K(ED2K, resolve);

    public IReadOnlyList<Shoko_Episode> GetEpisodes() =>
        GetCrossReferences(true)
            .Select(xref => xref.Episode!)
            .ToList();

    public IReadOnlyList<ShokoSeries> GetSeries() =>
        GetCrossReferences(true)
            .DistinctBy(xref => xref.AnidbAnimeId)
            .Select(xref => xref.Series!)
            .ToList();

    public IReadOnlyList<ShokoGroup> GetGroups() =>
        GetSeries()
            .DistinctBy(series => series.ParentGroupId)
            .Select(series => series.ParentGroup)
            .ToList();

    private MediaContainer? _media { get; set; }

    public virtual MediaContainer? Media
    {
        get
        {
            if (MediaVersion == MEDIA_VERSION && (_media?.GeneralStream?.Duration ?? 0) == 0 && MediaBlob != null && MediaBlob.Length > 0 && MediaSize > 0)
            {
                try
                {
                    _media = MessagePackSerializer.Deserialize<MediaContainer>(MediaBlob, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
                }
                catch (Exception e)
                {
                    logger.Error(e, "Unable to Deserialize MediaContainer as MessagePack: {Ex}", e);
                }
            }
            return _media;
        }
        set
        {
            _media = value;
            MediaBlob = MessagePackSerializer.Serialize(_media, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
            MediaSize = MediaBlob.Length;
            MediaVersion = MEDIA_VERSION;
        }
    }

    public string VideoResolution => Media?.VideoStream == null ? "0x0" : $"{Media.VideoStream.Width}x{Media.VideoStream.Height}";

    public void CollectContractMemory()
    {
        _media = null;
    }

    public Shoko_Video_User? GetUserRecord(int userId)
    {
        return RepoFactory.Shoko_Video_User.GetByUserAndVideoIds(userId, Id);
    }

    public Shoko_Video_User GetOrCreateUserRecord(int userId)
    {
        var userRecord = GetUserRecord(userId);
        if (userRecord != null)
            return userRecord;
        userRecord = new(userId, Id);
        RepoFactory.Shoko_Video_User.Save(userRecord);
        return userRecord;
    }

    public void RefreshMediaInfo(Shoko_Video_Location? location = null)
    {
        location ??= GetPreferredLocation(true);
        var path = location?.AbsolutePath;
        if (location == null || string.IsNullOrEmpty(path))
        {
            logger.Warn("Could not find a valid location for Video: {VideoId}", Id);
            return;
        }

        try
        {
            logger.Trace("Getting media info for: {0}", path);

            if (!location.IsAccessible)
            {
                logger.Error($"File {path} failed to be retrived for MediaInfo");
                return;
            }

            var mediaContainer = Utilities.MediaInfoLib.MediaInfo.GetMediaInfo(path);
            var duration = mediaContainer?.GeneralStream?.Duration ?? 0;
            if (duration == 0)
                mediaContainer = null;

            if (mediaContainer == null)
            {
                logger.Error($"File {path} failed to read MediaInfo");
                return;
            }

            var subs = SubtitleHelper.GetSubtitleStreams(path);
            if (subs.Count > 0)
                mediaContainer.media!.track.AddRange(subs);

            Media = mediaContainer;
            RepoFactory.Shoko_Video.Save(this, true);
        }
        catch (Exception e)
        {
            logger.Error(e, $"Unable to read the media information of file {path}:");
        }
    }

    public void SetResumePosition(long resumeposition, int userID)
    {
        var userRecord = GetOrCreateUserRecord(userID);
        userRecord.RawResumePosition = resumeposition;
        userRecord.LastUpdatedAt = DateTime.Now;
        RepoFactory.Shoko_Video_User.Save(userRecord);
    }

    private void SaveWatchedStatus(bool watched, int userId, DateTime? watchedAt, bool updateWatchedDate, DateTime? lastUpdatedAt = null)
    {
        var userRecord = GetUserRecord(userId);
        if (watched)
        {
            userRecord ??= new(userId, Id);
            userRecord.LastWatchedAt = DateTime.Now;
            userRecord.WatchedCount++;

            if (watchedAt.HasValue && updateWatchedDate)
                userRecord.LastWatchedAt = watchedAt.Value;

            userRecord.LastUpdatedAt = lastUpdatedAt ?? DateTime.Now;
            RepoFactory.Shoko_Video_User.Save(userRecord);
        }
        else
        {
            if (userRecord != null)
            {
                userRecord.LastWatchedAt = null;
                RepoFactory.Shoko_Video_User.Save(userRecord);
            }
        }
    }

    public void ToggleWatchedStatus(bool watched, int userID) =>
        ToggleWatchedStatus(watched, true, watched ? DateTime.Now : null, true, userID, true, true);

    public void ToggleWatchedStatus(bool watched, bool updateOnline, DateTime? watchedDate, bool updateStats, int userID,
        bool syncTrakt, bool updateWatchedDate, DateTime? lastUpdatedAt = null)
    {
        var settings = Utils.SettingsProvider.GetSettings();
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        var user = RepoFactory.Shoko_User.GetByID(userID);
        if (user == null) return;

        var aniDBUsers = RepoFactory.Shoko_User.GetAniDBUsers();

        if (!user.IsAniDBUser)
            SaveWatchedStatus(watched, userID, watchedDate, updateWatchedDate, lastUpdatedAt);
        else
            foreach (var juser in aniDBUsers)
                if (juser.IsAniDBUser)
                    SaveWatchedStatus(watched, juser.Id, watchedDate, updateWatchedDate, lastUpdatedAt);


        // now lets find all the associated AniDB_File record if there is one
        if (user.IsAniDBUser)
        {
            if (updateOnline)
                if ((watched && settings.AniDb.MyList_SetWatched) ||
                    (!watched && settings.AniDb.MyList_SetUnwatched))
                {
                    var cmd = commandFactory.Create<CommandRequest_UpdateMyListFileStatus>(
                        c =>
                        {
                            c.Hash = ED2K;
                            c.Watched = watched;
                            c.UpdateSeriesStats = false;
                            c.WatchedDateAsSecs = (int)TimeSpan.FromTicks(watchedDate?.ToUniversalTime().Ticks ?? 0).TotalSeconds;
                        }
                    );
                    cmd.Save();
                }
        }

        // now find all the episode records associated with this video file
        // but we also need to check if there are any other files attached to
        // this episode with a watched status,


        ShokoSeries ser;
        // get all files associated with this episode
        var xrefs = GetCrossReferences(false);
        var toUpdateSeries = new Dictionary<int, ShokoSeries>();
        if (watched)
        {
            // find the total watched percentage
            // eg one file can have a % = 100
            // or if 2 files make up one episodes they will each have a % = 50

            foreach (var xref1 in xrefs)
            {
                // get the episodes for this file, may be more than one (One Piece x Toriko)
                var ep = xref1.Episode;
                // a show we don't have
                if (ep == null) continue;

                // get all the files for this episode
                var epPercentWatched = 0;
                foreach (var xref2 in ep.CrossReferences)
                {
                    var userRecord = xref2.Video?.GetUserRecord(userID);
                    if (userRecord?.LastWatchedAt != null)
                        epPercentWatched += xref2.Percentage;

                    if (epPercentWatched > 95) break;
                }

                if (epPercentWatched > 95)
                {
                    ser = ep.Series;
                    // a problem
                    if (ser == null) continue;
                    if (!toUpdateSeries.ContainsKey(ser.Id))
                        toUpdateSeries.Add(ser.Id, ser);
                    if (!user.IsAniDBUser)
                        ep.SaveWatchedStatus(true, userID, watchedDate, updateWatchedDate);
                    else
                        foreach (var juser in aniDBUsers)
                            if (juser.IsAniDBUser)
                                ep.SaveWatchedStatus(true, juser.Id, watchedDate, updateWatchedDate);

                    if (syncTrakt && settings.TraktTv.Enabled &&
                        !string.IsNullOrEmpty(settings.TraktTv.AuthToken))
                    {
                        var cmdSyncTrakt = commandFactory.Create<CommandRequest_TraktHistoryEpisode>(
                            c =>
                            {
                                c.AnimeEpisodeID = ep.Id;
                                c.Action = TraktSyncAction.Add;
                            }
                        );
                        cmdSyncTrakt.Save();
                    }
                }
            }
        }
        else
        {
            // if setting a file to unwatched only set the episode unwatched, if ALL the files are unwatched
            foreach (var xref1 in xrefs)
            {
                // get the episodes for this file, may be more than one (One Piece x Toriko)
                var ep = xref1.Episode;
                // a show we don't have
                if (ep == null) continue;

                // get all the files for this episode
                var epPercentWatched = 0;
                foreach (var xref2 in ep.CrossReferences)
                {
                    var vidUser = xref2.Video?.GetUserRecord(userID);
                    if (vidUser?.LastWatchedAt != null)
                        epPercentWatched += xref2.Percentage;

                    if (epPercentWatched > 95) break;
                }

                if (epPercentWatched < 95)
                {
                    if (!user.IsAniDBUser)
                        ep.SaveWatchedStatus(false, userID, watchedDate, true);
                    else
                        foreach (var juser in aniDBUsers)
                            if (juser.IsAniDBUser)
                                ep.SaveWatchedStatus(false, juser.Id, watchedDate, true);

                    ser = ep.Series;
                    // a problem
                    if (ser == null) continue;
                    if (!toUpdateSeries.ContainsKey(ser.Id))
                        toUpdateSeries.Add(ser.Id, ser);

                    if (syncTrakt && settings.TraktTv.Enabled &&
                        !string.IsNullOrEmpty(settings.TraktTv.AuthToken))
                    {
                        var cmdSyncTrakt = commandFactory.Create<CommandRequest_TraktHistoryEpisode>(
                            c =>
                            {
                                c.AnimeEpisodeID = ep.Id;
                                c.Action = TraktSyncAction.Remove;
                            }
                        );
                        cmdSyncTrakt.Save();
                    }
                }
            }
        }

        // update stats for groups and series
        if (toUpdateSeries.Count > 0 && updateStats)
        {
            foreach (var s in toUpdateSeries.Values)
            {
                // update all the groups above this series in the hierarchy
                s.UpdateStats(true, true);
            }

            var groups = toUpdateSeries.Values
                .Select(a => a.TopLevelAnimeGroup)
                .DistinctBy(a => a.Id);
            foreach (var group in groups)
            {
                group.UpdateStatsFromTopLevel(true, true);
            }
        }
    }

# pragma warning disable 0618
    public override string ToString()
    {
        return $"{FileName} --- {ED2K}";
    }

    public string ToStringDetailed()
    {
        var sb = new StringBuilder("");
        sb.Append(Environment.NewLine);
        sb.Append("VideoLocalID: " + Id);

        sb.Append(Environment.NewLine);
        sb.Append("FileName: " + FileName);
        sb.Append(Environment.NewLine);
        sb.Append("Hash: " + ED2K);
        sb.Append(Environment.NewLine);
        sb.Append("FileSize: " + Size);
        sb.Append(Environment.NewLine);
        return sb.ToString();
    }

    // is the videolocal empty. This isn't complete, but without one or more of these the record is useless
    public bool IsEmpty()
    {
        if (!string.IsNullOrEmpty(ED2K)) return false;
        if (!string.IsNullOrEmpty(MD5)) return false;
        if (!string.IsNullOrEmpty(CRC32)) return false;
        if (!string.IsNullOrEmpty(SHA1)) return false;
        if (!string.IsNullOrEmpty(FileName)) return false;
        if (Size > 0) return false;
        return true;
    }
# pragma warning restore 0618

    public bool MergeInfoFrom(Shoko_Video otherVideo)
    {
        var changed = false;
        if (string.IsNullOrEmpty(ED2K) && !string.IsNullOrEmpty(otherVideo.ED2K))
        {
            ED2K = otherVideo.ED2K;
            changed = true;
        }
        if (string.IsNullOrEmpty(CRC32) && !string.IsNullOrEmpty(otherVideo.CRC32))
        {
            CRC32 = otherVideo.CRC32;
            changed = true;
        }
        if (string.IsNullOrEmpty(MD5) && !string.IsNullOrEmpty(otherVideo.MD5))
        {
            MD5 = otherVideo.MD5;
            changed = true;
        }
        if (string.IsNullOrEmpty(SHA1) && !string.IsNullOrEmpty(otherVideo.SHA1))
        {
            SHA1 = otherVideo.SHA1;
            changed = true;
        }
        return changed;
    }

    #endregion

    #region IShokoVideo

    #region Identifiers

    /// <inheritdoc/>
    int? IShokoVideo.AnidbFileId =>
        GetAniDB()?.Id;

    #endregion

    #region Links


    /// <inheritdoc/>
    IShokoVideoLocation? IShokoVideo.PreferredLocation =>
        GetPreferredLocation(true);

    /// <inheritdoc/>
    IReadOnlyList<IShokoVideoLocation> IShokoVideo.AllLocations =>
        GetLocations(true);

    /// <inheritdoc/>
    IReadOnlyList<IShokoVideoCrossReference> IShokoVideo.AllCrossReferences =>
        GetCrossReferences(true);

    /// <inheritdoc/>
    IReadOnlyList<IShokoEpisode> IShokoVideo.AllEpisodes =>
        GetEpisodes();

    /// <inheritdoc/>
    IReadOnlyList<IShokoSeries> IShokoVideo.AllSeries =>
        GetSeries();

    /// <inheritdoc/>
    IReadOnlyList<IShokoGroup> IShokoVideo.AllGroups =>
        GetGroups();

    /// <inheritdoc/>
    IAniDBFile? IShokoVideo.AnidbFile =>
        GetAniDB();

    #endregion

    #region Metadata

    DataSource IShokoVideo.CrossReferenceSources =>
        ((IShokoVideo)this).AllCrossReferences.Aggregate(DataSource.None, (current, xref) => current | xref.DataSource);

    IReadOnlyList<IReleaseGroup> IShokoVideo.AllReleaseGroups =>
        ((IShokoVideo)this).AllCrossReferences
            .Select(xref => xref.ReleaseGroup)
            .OfType<IReleaseGroup>()
            .DistinctBy(group => group.Id)
            .ToList();

    /// <summary>
    /// The relevant hashes for the video file. The CRC hash is the only one that should be used, but other hashes may be used for clever uses of the API.
    /// </summary>
    IHashes IShokoVideo.Hashes =>
        new HashesImpl(ED2K, CRC32, MD5, SHA1);

    /// <summary>
    /// The media information data for the video file. This may be null if we
    /// failed to parse the media info for the file.
    /// </summary>
    IMediaInfo? IShokoVideo.Media =>
        Media;

    DataSource IMetadata.DataSource =>
        DataSource.Shoko;

    #endregion

    #endregion
}

// This is a comparer used to sort the completeness of a videolocal, more complete first.
// Because this is only used for comparing completeness of hashes, it does NOT follow the strict equality rules
public class ShokoVideoComparer : IComparer<Shoko_Video>
{
    public int Compare(Shoko_Video? x, Shoko_Video? y)
    {
        if (x == null) return 1;
        if (y == null) return -1;
        if (string.IsNullOrEmpty(x.ED2K)) return 1;
        if (string.IsNullOrEmpty(y.ED2K)) return -1;
        if (string.IsNullOrEmpty(x.CRC32)) return 1;
        if (string.IsNullOrEmpty(y.CRC32)) return -1;
        if (string.IsNullOrEmpty(x.MD5)) return 1;
        if (string.IsNullOrEmpty(y.MD5)) return -1;
        if (string.IsNullOrEmpty(x.SHA1)) return 1;
        if (string.IsNullOrEmpty(y.SHA1)) return -1;
        return 0;
    }
}
