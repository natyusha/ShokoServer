using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.Implementations;
using Shoko.Plugin.Abstractions.Models.Shoko;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReferences;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Models.Internal;

public class Shoko_Episode : IShokoEpisode
{
    #region Database Columns

    /// <summary>
    /// Local id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Local <see cref="ShokoSeries"/> id.
    /// </summary>
    public int SeriesId { get; set; }

    /// <summary>
    /// The universally unique anidb episode id.
    /// </summary>
    /// <remarks>
    /// Also see <seealso cref="AniDB"/> for a local representation
    /// of the anidb episode data.
    /// </remarks>
    public int AnidbEpisodeId { get; set; }

    /// <summary>
    /// Hidden episodes will not show up in the UI unless explictly
    /// requested, and will also not count towards the unwatched count for
    /// the series.
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// Timestamp for when the entry was first created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp for when the entry was last updated.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    public IRating? UserRating
    {
        get
        {
            var vote = RepoFactory.AniDB_Vote.GetByEntityAndType(Id, AniDBVoteType.Episode);
            if (vote == null)
                return null;

            return new RatingImpl(DataSource.AniDB, vote.VoteValue / 100, 100, 1, vote.VoteType.ToString());
        }
    }

    #region Titles

    public ITitle GetPreferredTitle()
    {
        // Try finding the preferred title regardless of language.
        var preferredTitle = RepoFactory.ShokoEpisodePreferredTitle.GetByAnidbAnimeIdAndLanguage(AnidbEpisodeId, null);
        if (preferredTitle != null)
            return preferredTitle.Title;

        // Try finding one of the preferred languages.
        foreach (var language in Languages.PreferredEpisodeNamingLanguages)
        {
            // Try finding the preferred title for the given language.
            preferredTitle = RepoFactory.ShokoEpisodePreferredTitle.GetByAnidbAnimeIdAndLanguage(AnidbEpisodeId, language);
            if (preferredTitle != null)
                return preferredTitle.Title;

            // Try finding the anidb title for the given language.
            var anidbTitle = RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(AnidbEpisodeId, language.Language).FirstOrDefault();
            if (anidbTitle != null)
            {
                // Mark it as preferred now.
                anidbTitle.IsPreferred = true;
                return anidbTitle;
            }
        }

        // Get the main title, and also mark it as the preferred title.
        var mainTitle = GetMainTitle();
        switch (mainTitle)
        {
            case AniDB_Episode_Title anidbTitle:
                anidbTitle.IsPreferred = true;
                break;
            case ShokoEpisode_Title shokoTitle:
                shokoTitle.IsPreferred = true;
                break;
            case TitleImpl impl:
                impl.IsPreferred = true;
                break;
        }
        return mainTitle;
    }

    public ITitle GetMainTitle() =>
        AniDB.GetMainTitle();

    public IReadOnlyList<ITitle> GetTitles(IEnumerable<TextLanguage>? languages = null)
    {
        var list = new List<ITitle>();

        list.AddRange(GetUserDefinedTitles());
        var anidbEpisode = AniDB;
        if (anidbEpisode != null)
            list.AddRange(anidbEpisode.GetTitles());
        foreach (var tmdbEpisode in GetTMDBEpisodes())
            list.AddRange(tmdbEpisode.GetTitles());
        foreach (var tvdbEpisode in GetTvDBEpisodes())
            list.AddRange(tvdbEpisode.GetTitles());

        list.Sort();

        if (languages == null)
            return list;
    
        if (!(languages is ISet<TextLanguage> languageSet))
            languageSet = languages.ToHashSet();
        if (languageSet.Count == 0)
            return new ITitle[0];

        return list
            .Where(title => languageSet.Contains(title.Language))
            .ToList();
    }

    public IReadOnlyList<ShokoEpisode_Title> GetUserDefinedTitles()
    {
        return new List<ShokoEpisode_Title>();
    }

    public ShokoEpisode_Title? AddUserDefinedTitle(int userId, string title, TextLanguage language = TextLanguage.English, TitleType type = TitleType.Synonym)
    {
        return new();
    }

    public bool RemoveUserDefinedTitle(ShokoEpisode_Title title)
    {
        return false;
    }

    #endregion

    #region Shoko

    public ShokoSeries Series =>
        RepoFactory.Shoko_Series.GetByID(SeriesId)!;

    public IReadOnlyList<CR_Video_Episode> CrossReferences =>
        RepoFactory.CR_Video_Episode.GetByAniDBEpisodeId(AnidbEpisodeId);

    public IReadOnlyList<Shoko_Video> Videos =>
        GetVideos();

    public IReadOnlyList<Shoko_Video> GetVideos(CrossRefSource? xrefSource = null) =>
        RepoFactory.Shoko_Video.GetByAnidbEpisodeId(AnidbEpisodeId, xrefSource);

    #endregion

    #region 3rd-Party Providers

    #region AniDB

    public AniDB_Episode AniDB =>
        RepoFactory.AniDB_Episode.GetByAnidbEpisodeId(AnidbEpisodeId)!;

    public IReadOnlyList<AniDB_Episode_Title> GetAniDBTitles() =>
        RepoFactory.AniDB_Episode_Title.GetByEpisodeID(AnidbEpisodeId);

    #endregion

    #region TvDB

    public IReadOnlyList<CR_AniDB_TvDB_Episode> GetTvDBCrossReferences() =>
        RepoFactory.CR_AniDB_TvDB_Episode.GetByAniDBEpisodeID(AnidbEpisodeId);

    public IReadOnlyList<TvDB_Episode> GetTvDBEpisodes() =>
        GetTvDBCrossReferences()
            .Select(xref => xref.TvDBEpisode)
            .Where(episode => episode != null)
            .ToList();

    public TvDB_Episode TvDBEpisode
    {
        get
        {
            // Try Overrides first, then regular
            return RepoFactory.CR_AniDB_TvDB_Episode_Override.GetByAniDBEpisodeID(AnidbEpisodeId)
                .Select(a => RepoFactory.TvDB_Episode.GetByTvDBID(a.TvDBEpisodeID)).Where(a => a != null)
                .OrderBy(a => a.SeasonNumber).ThenBy(a => a.EpisodeNumber).FirstOrDefault() ?? RepoFactory.CR_AniDB_TvDB_Episode.GetByAniDBEpisodeID(AnidbEpisodeId)
                .Select(a => RepoFactory.TvDB_Episode.GetByTvDBID(a.TvdbEpisodeId)).Where(a => a != null)
                .OrderBy(a => a.SeasonNumber).ThenBy(a => a.EpisodeNumber).FirstOrDefault();
        }
    }

    public List<TvDB_Episode> TvDBEpisodes
    {
        get
        {
            // Try Overrides first, then regular
            var overrides = RepoFactory.CR_AniDB_TvDB_Episode_Override.GetByAniDBEpisodeID(AnidbEpisodeId)
                .Select(a => RepoFactory.TvDB_Episode.GetByTvDBID(a.TvDBEpisodeID)).Where(a => a != null)
                .OrderBy(a => a.SeasonNumber).ThenBy(a => a.EpisodeNumber).ToList();
            return overrides.Count > 0
                ? overrides
                : RepoFactory.CR_AniDB_TvDB_Episode.GetByAniDBEpisodeID(AnidbEpisodeId)
                    .Select(a => RepoFactory.TvDB_Episode.GetByTvDBID(a.TvdbEpisodeId)).Where(a => a != null)
                    .OrderBy(a => a.SeasonNumber).ThenBy(a => a.EpisodeNumber).ToList();
        }
    }

    #endregion

    #region TMDB

    public IReadOnlyList<CR_AniDB_TMDB_Episode> GetTMDBCrossReferences() =>
        RepoFactory.CR_AniDB_TMDB_Episode.GetByAnidbEpisodeId(AnidbEpisodeId);

    public IReadOnlyList<TMDB_Episode> GetTMDBEpisodes() =>
        GetTMDBCrossReferences()
            .Select(xref => xref.TMDBEpisode)
            .Where(episode => episode != null)
            .ToList();

    public IReadOnlyList<TMDB_EpisodeTitle> GetTMDBTitles() =>
        GetTMDBCrossReferences()
            .SelectMany(xref => RepoFactory.TMDB_EpisodeTitle.GetByEpisodeId(xref.TmdbEpisodeId))
            .ToList();

    #endregion

    #region Trakt

    public IReadOnlyList<CR_AniDB_Trakt_Episode> GetTraktCrossReferences() =>
        RepoFactory.CR_AniDB_Trakt_Episode.GetByAnidbEpisodeId(AnidbEpisodeId);

    public IReadOnlyList<Trakt_Episode> GetTraktEpisodes() =>
        GetTraktCrossReferences()
            .Select(xref => xref.TraktEpisode)
            .Where(episode => episode != null)
            .ToList();

    public IReadOnlyList<Trakt_EpisodeTitle> GetTraktTitles() =>
        GetTraktCrossReferences()
            .SelectMany(xref => RepoFactory.Trakt_EpisodeTitle.GetByEpisodeId(xref.TmdbEpisodeId))
            .ToList();

    #endregion

    #endregion

    #region Watch Status

    public void SaveWatchedStatus(bool watched, int userID, DateTime? watchedDate, bool updateWatchedDate)
    {
        var userRecord = GetUserRecord(userID);

        if (watched)
        {
            // lets check if an update is actually required
            if (userRecord?.LastWatchedAt != null && watchedDate.HasValue &&
                userRecord.LastWatchedAt.Equals(watchedDate.Value) ||
                (userRecord?.LastWatchedAt == null && !watchedDate.HasValue))
                return;

            if (userRecord == null)
                userRecord = new(userID, Id, SeriesId);

            userRecord.WatchedCount++;
            if (userRecord.LastWatchedAt.HasValue && updateWatchedDate || !userRecord.LastWatchedAt.HasValue)
                userRecord.LastWatchedAt = watchedDate ?? DateTime.Now;

            RepoFactory.Shoko_Episode_User.Save(userRecord);
        }
        else if (userRecord != null && updateWatchedDate)
        {
            userRecord.LastWatchedAt = null;
            RepoFactory.Shoko_Episode_User.Save(userRecord);
        }
    }

    public void ToggleWatchedStatus(bool watched, bool updateOnline, DateTime? watchedDate, int userID, bool syncTrakt) =>
        ToggleWatchedStatus(watched, updateOnline, watchedDate, true, userID, syncTrakt);

    public void ToggleWatchedStatus(bool watched, bool updateOnline, DateTime? watchedDate, bool updateStats, int userID, bool syncTrakt)
    {
        foreach (var video in Videos)
        {
            video.ToggleWatchedStatus(watched, updateOnline, watchedDate, updateStats, userID, syncTrakt, true);
            video.SetResumePosition(0, userID);
        }
    }

    #endregion

    #region User

    #region User Record

    public ShokoEpisode_User? GetUserRecord(int userId) =>
        RepoFactory.Shoko_Episode_User.GetByUserAndEpisodeIds(userId, Id);

    #endregion

    #region User V1 Contracts

    // get all the cross refs
    public IReadOnlyList<CL_VideoDetailed> GetVideoDetailedContracts(int userId) =>
        Videos
            .Select(v => v.ToClientDetailed(userId))
            .ToList();

    public CL_AnimeEpisode_User GetUserContract(int userID)
    {
        var anidbEpisode = AniDB;
        var seriesUserRecord = RepoFactory.Shoko_Series_User.GetByUserAndSeriesIds(userID, SeriesId);
        var episodeUserRecord = GetUserRecord(userID);
        var contract = new CL_AnimeEpisode_User
        {
            AniDB_EpisodeID = AnidbEpisodeId,
            AnimeEpisodeID = Id,
            AnimeSeriesID = SeriesId,
            DateTimeCreated = CreatedAt,
            DateTimeUpdated = LastUpdatedAt,
#pragma warning disable 0618
            PlayedCount = episodeUserRecord?.PlayedCount ?? 0,
            StoppedCount = episodeUserRecord?.StoppedCount ?? 0,
#pragma warning restore 0618
            WatchedCount = episodeUserRecord?.WatchedCount ?? 0,
            WatchedDate = episodeUserRecord?.LastWatchedAt,
            AniDB_EnglishName = RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(AnidbEpisodeId, TextLanguage.English)
                .FirstOrDefault()?.Value,
            AniDB_RomajiName = RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(AnidbEpisodeId, TextLanguage.Romaji)
                .FirstOrDefault()?.Value,
            AniDB_AirDate = anidbEpisode.AirDate,
            AniDB_LengthSeconds = anidbEpisode.RawDuration,
            AniDB_Rating = anidbEpisode.RawRating.ToString(),
            AniDB_Votes = anidbEpisode.Votes.ToString(),
            EpisodeNumber = anidbEpisode.Number,
            Description = anidbEpisode.Overview,
            EpisodeType = (int) anidbEpisode.Type,
            UnwatchedEpCountSeries = seriesUserRecord?.UnwatchedEpisodeCount ?? 0,
            LocalFileCount = Videos.Count,
        };
        return contract;
    }


    #endregion

    #endregion
    
    #region Remove Records

    public void RemoveVideoLocals(bool deleteFiles)
    {
        foreach (var location in Videos.SelectMany(video => video.GetLocations(false)))
        {
            if (deleteFiles)
                location.RemoveRecordAndDeletePhysicalFile();
            else
                location.RemoveRecord();
        }
    }

    #endregion

    #region IShokoEpisode

    int IShokoEpisode.AnidbEpisodeId =>
        AnidbEpisodeId;

    ITitle ITitleContainer.PreferredTitle =>
        GetPreferredTitle();

    ITitle ITitleContainer.MainTitle =>
        GetMainTitle();

    IReadOnlyList<ITitle> ITitleContainer.Titles =>
        GetTitles();

    #endregion


    protected bool Equals(Shoko_Episode other)
    {
        return Id == other.Id && SeriesId == other.SeriesId &&
               AnidbEpisodeId == other.AnidbEpisodeId &&
               LastUpdatedAt.Equals(other.LastUpdatedAt) &&
               CreatedAt.Equals(other.CreatedAt);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((Shoko_Episode)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Id;
            hashCode = (hashCode * 397) ^ SeriesId;
            hashCode = (hashCode * 397) ^ AnidbEpisodeId;
            hashCode = (hashCode * 397) ^ LastUpdatedAt.GetHashCode();
            hashCode = (hashCode * 397) ^ CreatedAt.GetHashCode();
            return hashCode;
        }
    }
}
