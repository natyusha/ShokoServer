using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.PlexAndKodi;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.Implementations;
using Shoko.Plugin.Abstractions.Models.Provider;
using Shoko.Plugin.Abstractions.Models.Shoko;
using Shoko.Server.Databases;
using Shoko.Server.LZ4;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReferences;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Models.Trakt;
using Shoko.Server.Models.TvDB;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Models.Internal;

public class ShokoSeries : IShokoSeries, IMemoryCollectable
{
    #region Database Columns

    public int Id { get; set; }

    public int ParentGroupId { get; set; }

    public int AniDB_ID { get; set; }

    public DayOfWeek? AirsOn { get; set; }

    public int MissingEpisodeCount { get; set; }

    public int MissingEpisodeCountGroups { get; set; }

    public int HiddenMissingEpisodeCount { get; set; }

    public int HiddenMissingEpisodeCountGroups { get; set; }

    public int LatestLocalEpisodeNumber { get; set; }

    public string SeriesNameOverride { get; set; }

    public DateTime DateTimeUpdated { get; set; }

    public DateTime DateTimeCreated { get; set; }

    public DateTime? EpisodeAddedDate { get; set; }

    public DateTime? LatestEpisodeAirDate { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DataSource DisableAutoMatchFlags { get; set; } = 0;

    #region Contract

    public const int CONTRACT_VERSION = 9;

    public int ContractSize { get; set; }

    public int ContractVersion { get; set; }

    public byte[]? ContractBlob { get; set; }

    #endregion

    #endregion

    #region Disabled Auto Matching

    public bool IsTvDBAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSource.TvDB);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSource.TvDB;
            else
                DisableAutoMatchFlags &= ~DataSource.TvDB;
        }
    }

    public bool IsTMDBAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSource.TMDB);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSource.TMDB;
            else
                DisableAutoMatchFlags &= ~DataSource.TMDB;
        }
    }

    public bool IsTraktAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSource.Trakt);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSource.Trakt;
            else
                DisableAutoMatchFlags &= ~DataSource.Trakt;
        }
    }

    public bool IsMALAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSource.MAL);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSource.MAL;
            else
                DisableAutoMatchFlags &= ~DataSource.MAL;
        }
    }

    public bool IsAniListAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSource.AniList);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSource.AniList;
            else
                DisableAutoMatchFlags &= ~DataSource.AniList;
        }
    }

    public bool IsAnimeshonAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSource.Animeshon);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSource.Animeshon;
            else
                DisableAutoMatchFlags &= ~DataSource.Animeshon;
        }
    }

    public bool IsKitsuAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSource.Kitsu);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSource.Kitsu;
            else
                DisableAutoMatchFlags &= ~DataSource.Kitsu;
        }
    }

    #endregion

    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    public ITitle GetMainTitle()
    {
        return GetAnime().GetMainTitle();
    }

    public ITitle GetPreferredTitle()
    {
        // Return the override if it's set.
        if (!string.IsNullOrEmpty(SeriesNameOverride))
            return new TitleImpl(DataSource.User, TextLanguage.None, SeriesNameOverride, TitleType.None, false, true);

        // Try to find the TvDB title if we prefer TvDB titles.
        if (Utils.SettingsProvider.GetSettings().SeriesNameSource == DataSource.TvDB)
        {
            var tvdbShows = GetTvdbShows();
            var tvdbShowTitle = tvdbShows
                .FirstOrDefault(show => show.MainTitle.Contains("**DUPLICATE", StringComparison.InvariantCultureIgnoreCase))?.MainTitle;
            if (!string.IsNullOrEmpty(tvdbShowTitle))
                return tvdbShowTitle;
        }

        // Otherwise just return the anidb title.
        return GetAnime().GetMainTitle;
    }

    public IReadOnlyList<ITitle> GetAllTitles()
    {
        var titles = new HashSet<string>();

        // Override
        if (SeriesNameOverride != null)
        {
            titles.Add(SeriesNameOverride);
        }

        // AniDB
        if (GetAnime() != null)
        {
            titles.UnionWith(GetAnime().GetAllTitles());
        }
        else
        {
            logger.Error($"A Series has a null AniDB_Anime. That is bad. The AniDB ID is {AniDB_ID}");
        }

        // TvDB
        var tvdb = GetTvdbShows();
        if (tvdb != null)
        {
            titles.UnionWith(tvdb.Select(a => a?.MainTitle).Where(a => a != null));
        }

        // MovieDB
        var movieDB = GetTMDBMovies();
        if (movieDB != null)
        {
            titles.UnionWith(movieDB.SelectMany(movie => new string[] { movie?.MovieName, movie?.OriginalName }).Where(a => !string.IsNullOrEmpty(a)));
        }

        return titles;
    }

    #region Shoko

    /// <summary>
    /// Get episodes for the series.
    /// </summary>
    /// <param name="orderList">Order the returned list.</param>
    /// <param name="includeHidden">Include ignored episodes in the list.</param>
    /// <returns>A list of episodes for the series.</returns>
    public IReadOnlyList<Shoko_Episode> GetEpisodes(bool orderList = false, bool includeHidden = true)
    {
        if (orderList)
        {
            // TODO: Convert to a LINQ query once we've switched to EF Core.
            return RepoFactory.Shoko_Episode.GetBySeriesID(Id)
                .Where(episode => includeHidden || !episode.IsHidden)
                .Select(episode => (episode, anidbEpisode: episode.AniDB))
                .OrderBy(tuple => tuple.anidbEpisode.Type)
                .ThenBy(tuple => tuple.anidbEpisode.Number)
                .Select(tuple => tuple.episode)
                .ToList();
        }
        if (!includeHidden)
        {
            // TODO: Convert to a LINQ query once we've switched to EF Core.
            return RepoFactory.Shoko_Episode.GetBySeriesID(Id)
                .Where(episode => !episode.IsHidden)
                .ToList();
        }
        return RepoFactory.Shoko_Episode.GetBySeriesID(Id);
    }

    /// <summary>
    /// Get video locals for anime series.
    /// </summary>
    /// <param name="xrefSource">Set to a value to only select video locals from
    /// a select source.</param>
    /// <returns>All or some video locals for the anime series.</returns>
    public IReadOnlyList<Shoko_Video> GetVideos(CrossRefSource? xrefSource = null) =>
        RepoFactory.Shoko_Video.GetByAnidbAnimeId(AniDB_ID, xrefSource);

    #endregion

    #region Providers

    #region TMDB

    public IReadOnlyList<CR_AniDB_TMDB_Movie> GetTmdbMovieCrossReferences() =>
        RepoFactory.CR_AniDB_TMDB_Movie.GetByAnidbAnimeId(AniDB_ID);

    public IReadOnlyList<CR_AniDB_TMDB_Show> GetTmdbShowCrossRefereneces() =>
        RepoFactory.CR_AniDB_TMDB_Show.GetByAnidbAnimeId(AniDB_ID);

    public IReadOnlyList<TMDB_Movie> GetTmdbMovies() =>
        GetTmdbMovieCrossReferences()
            .Select(xref => xref.TMDBMovie!)
            .Where(movie => movie != null)
            .ToList();

    public IReadOnlyList<TMDB_Show> GetTmdbShows() =>
        GetTmdbShowCrossRefereneces()
            .Select(xref => xref.TMDB_Show!)
            .Where(show => show != null)
            .ToList();

    #endregion

    #region TvDB

    public IReadOnlyList<CR_AniDB_TvDB> GetTvdbCrossReferences() =>
        RepoFactory.CR_AniDB_TvDB.GetByAnimeID(AniDB_ID);

    public IReadOnlyList<TvDB_Show> GetTvdbShows() =>
        RepoFactory.CR_AniDB_TvDB.GetByAnimeID(AniDB_ID)
            .Select(xref => xref.TvDBShow!)
            .Where(show => show != null)
            .ToList();

    #endregion

    #region Trakt

    public IReadOnlyList<CR_AniDB_Trakt> GetTraktCrossReferences() =>
        RepoFactory.CR_AniDB_Trakt.GetByAnimeID(AniDB_ID);

    public IReadOnlyList<Trakt_Show> GetTraktShows() =>
        GetTvdbCrossReferences()
            .Select(xref => xref.TraktShow!)
            .Where(show => show != null)
            .ToList();

    #endregion

    #region MAL

    public List<CrossRef_AniDB_MAL> CrossRefMAL => RepoFactory.CR_AniDB_MAL.GetByAnimeID(AniDB_ID);

    #endregion

    #endregion

    #region User

    #region User Record

    public ShokoSeries_User GetUserRecord(int userID) =>
        RepoFactory.Shoko_Series_User.GetByUserAndSeriesIds(userID, Id);

    public ShokoSeries_User GetOrCreateUserRecord(int userID)
    {
        var userRecord = GetUserRecord(userID);
        if (userRecord != null)
            return userRecord;

        userRecord = new ShokoSeries_User(userID, Id);
        RepoFactory.Shoko_Series_User.Save(userRecord);
        return userRecord;
    }

    #endregion

    #region Client V1 Contract

    private CL_AnimeSeries_User? _contract;

    public virtual CL_AnimeSeries_User? Contract
    {
        get
        {
            if (_contract == null && ContractBlob != null && ContractBlob.Length > 0 && ContractSize > 0)
            {
                _contract = CompressionHelper.DeserializeObject<CL_AnimeSeries_User>(ContractBlob, ContractSize);
            }

            return _contract;
        }
        set
        {
            _contract = value;
            ContractBlob = CompressionHelper.SerializeObject(value, out var outsize);
            ContractSize = outsize;
            ContractVersion = CONTRACT_VERSION;
        }
    }

    public void CollectContractMemory()
    {
        _contract = null;
    }

    public CL_AnimeSeries_User? GetUserContract(int userid, HashSet<GroupFilterConditionType>? types = null, bool cloned = true)
    {
        try
        {
            var contract = Contract;
            if (contract == null)
            {
                logger.Trace($"Series with ID [{AniDB_ID}] has a null contract on get. Updating");
                RepoFactory.Shoko_Series.Save(this, false, false, true);
                contract = (CL_AnimeSeries_User)_contract?.Clone();
            }
            if (cloned && contract != null) contract = (CL_AnimeSeries_User)contract.Clone();

            if (contract == null)
            {
                logger.Warn($"Series with ID [{AniDB_ID}] has a null contract even after updating");
                return null;
            }

            var userRecord = GetUserRecord(userid);
            if (userRecord != null)
            {
                contract.UnwatchedEpisodeCount = userRecord.UnwatchedEpisodeCount;
                contract.WatchedEpisodeCount = userRecord.WatchedEpisodeCount;
                contract.WatchedDate = userRecord.WatchedDate;
                contract.PlayedCount = userRecord.PlayedCount;
                contract.WatchedCount = userRecord.WatchedCount;
                contract.StoppedCount = userRecord.StoppedCount;
                contract.AniDBAnime.AniDBAnime.FormattedTitle = GetPreferredTitle();
                return contract;
            }

            if (types != null)
            {
                if (!types.Contains(GroupFilterConditionType.HasUnwatchedEpisodes))
                {
                    types.Add(GroupFilterConditionType.HasUnwatchedEpisodes);
                }

                if (!types.Contains(GroupFilterConditionType.EpisodeWatchedDate))
                {
                    types.Add(GroupFilterConditionType.EpisodeWatchedDate);
                }

                if (!types.Contains(GroupFilterConditionType.HasWatchedEpisodes))
                {
                    types.Add(GroupFilterConditionType.HasWatchedEpisodes);
                }
            }

            if (contract.AniDBAnime?.AniDBAnime != null)
            {
                contract.AniDBAnime.AniDBAnime.FormattedTitle = GetPreferredTitle();
            }

            return contract;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Plex Contract

    public Video GetPlexContract(int userid)
    {
        var ser = GetUserContract(userid);
        var v = GetOrCreateUserRecord(userid).PlexContract;
        v.Title = ser.AniDBAnime.AniDBAnime.FormattedTitle;
        return v;
    }

    #endregion

    #endregion

    #region Counts

    public int GetAnimeEpisodesCountWithVideoLocal()
    {
        return GetEpisodes()
            .Count(a => a.Videos.Any());
    }

    public int GetAnimeEpisodesNormalCountWithVideoLocal()
    {
        return GetEpisodes()
            .Count(episode =>
            {
                var anidb = episode.AniDB;
                if (anidb == null)
                    return false;
                if (!episode.Videos.Any())
                    return false;
                return anidb.Type == EpisodeType.Normal;
            });
    }

    public int GetAnimeEpisodesAndSpecialsCountWithVideoLocal()
    {
        return GetEpisodes()
            .Count(episode =>
            {
                var anidb = episode.AniDB;
                if (anidb == null)
                    return false;
                if (!episode.Videos.Any())
                    return false;
                return anidb.Type == EpisodeType.Normal || anidb.Type == EpisodeType.Special;
            });
    }

    public int GetAnimeNumberOfEpisodeTypes()
    {
        return GetEpisodes()
            .Select(episode => new { Shoko = episode, AniDB = episode.AniDB })
            .Where(episode =>
            {
                var anidb = episode.AniDB;
                if (anidb == null)
                    return false;
                return episode.Shoko.Videos.Count > 0;
            })
            .Select(a => a.AniDB.Type)
            .Distinct()
            .Count();
    }

    #endregion

    public Shoko_Episode GetLastEpisodeWatched(int userID)
    {
        Shoko_Episode watchedep = null;
        ShokoEpisode_User userRecordWatched = null;

        foreach (var ep in GetEpisodes())
        {
            var userRecord = ep.GetUserRecord(userID);
            if (userRecord != null && ep.AniDB_Episode != null && ep.EpisodeTypeEnum == EpisodeType.Episode)
            {
                if (watchedep == null)
                {
                    watchedep = ep;
                    userRecordWatched = userRecord;
                }

                if (userRecord.LastWatchedAt > userRecordWatched.LastWatchedAt)
                {
                    watchedep = ep;
                    userRecordWatched = userRecord;
                }
            }
        }

        return watchedep;
    }

    /// <summary>
    /// Get the most recent activly watched episode for the user.
    /// </summary>
    /// <param name="userID">User ID</param>
    /// <param name="includeSpecials">Include specials when searching.</param>
    /// <returns></returns>
    public Shoko_Episode? GetActiveEpisode(int userID, bool includeSpecials = true)
    {
        // Filter the episodes to only normal or special episodes and order them in rising order.
        var episodes = GetEpisodes()
            .Select(episode => (episode, episode.AniDB_Episode))
            .Where(tuple => tuple.AniDB_Episode.EpisodeType == (int)EpisodeType.Episode ||
                            (includeSpecials && tuple.AniDB_Episode.EpisodeType == (int)EpisodeType.Special))
            .OrderBy(tuple => tuple.AniDB_Episode.EpisodeType)
            .ThenBy(tuple => tuple.AniDB_Episode.EpisodeNumber)
            .Select(tuple => tuple.episode)
            .ToList();
        // Look for active watch sessions and return the episode for the most recent session if found.
        var (episode, _) = episodes
            .SelectMany(episode => episode.GetVideoLocals().Select(file => (episode, file.GetUserRecord(userID))))
            .Where(tuple => tuple.Item2 != null)
            .OrderByDescending(tuple => tuple.Item2.LastUpdated)
            .FirstOrDefault(tuple => tuple.Item2.ResumePosition > 0);
        return episode;
    }

    /// <summary>
    /// Series next-up query options for use with <see cref="GetNextEpisode"/>.
    /// </summary>
    public class NextUpQueryOptions
    {
        /// <summary>
        /// Disable the first episode in the series from showing up.
        /// /// </summary>
        public bool DisableFirstEpisode = false;

        /// <summary>
        /// Include currently watching episodes in the search.
        /// </summary>
        public bool IncludeCurrentlyWatching = false;

        /// <summary>
        /// Include hidden episodes in the search.
        /// </summary>
        public bool IncludeHidden = false;

        /// <summary>
        /// Include missing episodes in the search.
        /// </summary>
        public bool IncludeMissing = false;

        /// <summary>
        /// Include already watched episodes in the search if we determine the
        /// user is "re-watching" the series.
        /// </summary>
        public bool IncludeRewatching = false;

        /// <summary>
        /// Include specials in the search.
        /// </summary>
        public bool IncludeSpecials = true;
    }

    /// <summary>
    /// Get the next episode for the series for a user.
    /// </summary>
    /// <param name="userID">User ID</param>
    /// <param name="options">Next-up query options.</param>
    /// <returns></returns>
    public Shoko_Episode? GetNextEpisode(int userID, NextUpQueryOptions options = null)
    {
        // Initialise the options if they're not provided.
        if (options == null)
            options = new();

        // Filter the episodes to only normal or special episodes and order them
        // in rising order. Also count the number of episodes and specials if
        // we're searching for the next episode for "re-watching" sessions.
        var episodesCount = 0;
        var speicalsCount = 0;
        var episodeList = GetEpisodes(orderList: false, includeHidden: options.IncludeHidden)
            .Select(episode => (episode, episode.AniDB_Episode))
            .Where(tuple =>
            {
                if (tuple.AniDB_Episode.EpisodeType == (int)EpisodeType.Episode)
                {
                    episodesCount++;
                    return true;
                }

                if (options.IncludeSpecials && tuple.AniDB_Episode.EpisodeType == (int)EpisodeType.Special)
                {
                    speicalsCount++;
                    return true;
                }

                return false;
            })
            .OrderBy(tuple => tuple.AniDB_Episode.EpisodeType)
            .ThenBy(tuple => tuple.AniDB_Episode.EpisodeNumber)
            .ToList();

        // Look for active watch sessions and return the episode for the most
        // recent session if found.
        if (options.IncludeCurrentlyWatching)
        {
            var (currentlyWatchingEpisode, _) = episodeList
                .SelectMany(tuple => tuple.episode.GetVideoLocals().Select(file => (episode: tuple.episode, fileUR: file.GetUserRecord(userID))))
                .Where(tuple => tuple.fileUR != null)
                .OrderByDescending(tuple => tuple.fileUR.LastUpdated)
                .FirstOrDefault(tuple => tuple.fileUR.ResumePosition > 0);

            if (currentlyWatchingEpisode != null)
            {
                return currentlyWatchingEpisode;
            }
        }
        // Skip check if there is an active watch session for the series and we
        // don't allow active watch sessions.
        else if (episodeList.Any(tuple =>
                     tuple.episode.GetVideoLocals().Any(file => (file.GetUserRecord(userID)?.ResumePosition ?? 0) > 0)))
        {
            return null;
        }

        // When "re-watching" we look for the next episode after the last
        // watched episode.
        if (options.IncludeRewatching)
        {
            var (lastWatchedEpisode, _) = episodeList
                .SelectMany(tuple => tuple.episode.GetVideoLocals().Select(file => (episode: tuple.episode, fileUR: file.GetUserRecord(userID))))
                .Where(tuple => tuple.fileUR != null && tuple.fileUR.WatchedDate.HasValue)
                .OrderByDescending(tuple => tuple.fileUR.LastUpdated)
                .FirstOrDefault();

            if (lastWatchedEpisode != null) {
                // Return `null` if we're on the last episode in the list, or
                // if we're on the last normal episode and there is no specials
                // after it.
                var nextIndex = episodeList.FindIndex(tuple => tuple.episode == lastWatchedEpisode) + 1;
                if ((nextIndex == episodeList.Count) || (nextIndex == episodesCount) && (!options.IncludeSpecials || speicalsCount == 0))
                    return null;

                var (nextEpisode, _) = episodeList.Skip(nextIndex)
                    .FirstOrDefault(options.IncludeMissing ? _ => true : tuple => tuple.episode.GetVideoLocals().Count > 0);
                return nextEpisode;
            }
        }

        // Find the first episode that's unwatched.
        var (unwatchedEpisode, anidbEpisode) = episodeList
            .Where(tuple =>
            {
                var episodeUserRecord = tuple.episode.GetUserRecord(userID);
                if (episodeUserRecord == null)
                {
                    return true;
                }

                return episodeUserRecord.WatchedCount == 0;
            })
            .FirstOrDefault(options.IncludeMissing ? _ => true : tuple => tuple.episode.GetVideoLocals().Count > 0);

        // Disable first episode from showing up in the search.
        if (options.DisableFirstEpisode && anidbEpisode != null && anidbEpisode.EpisodeType == (int)EpisodeType.Episode && anidbEpisode.EpisodeNumber == 1)
            return null;

        return unwatchedEpisode;
    }

    public AniDB_Anime GetAnime()
    {
        var anidb = RepoFactory.AniDB_Anime.GetByAnidbAnimeId(AniDB_ID);
        if (anidb == null)
            throw new NullReferenceException($"ShokoSeries with Id {AniDB_ID} not found.");

        return anidb;
    }

    public DateTime? AirDate
    {
        get
        {
            var anime = GetAnime();
            if (anime?.AirDate != null)
            {
                return anime.AirDate.Value;
            }

            // This will be slower, but hopefully more accurate
            var ep = GetEpisodes()
                .Select(a => a.AniDB)
                .Where(a => (a.Type == EpisodeType.Normal) && a.RawDuration > 0)
                .Select(a => a.AirDate)
                .Where(a => a != null)
                .OrderBy(a => a)
                .FirstOrDefault();
            if (ep != null)
                return ep.Value;

            return null;
        }
    }

    public DateTime? EndDate
    {
        get
        {
            var anime = GetAnime();
            if (anime?.EndDate != null)
            {
                return anime.EndDate.Value;
            }

            return null;
        }
    }

    /// <summary>
    /// Get the most recent days in the week the show airs on.
    /// </summary>
    /// <param name="animeEpisodes">Optionally pass in the episodes so we don't have to fetch them.</param>
    /// <param name="includeThreshold">Threshold of episodes to include in the calculation.</param>
    /// <returns></returns>
    public List<DayOfWeek> GetAirsOnDaysOfWeek(List<Shoko_Episode> animeEpisodes = null, int includeThreshold = 24)
    {
        // Fetch the anime episodes now if we didn't get them supplied to us.
        if (animeEpisodes == null)
            animeEpisodes = GetEpisodes();

        var now = DateTime.Now;
        var filteredEpisodes = animeEpisodes
            .Select(episode =>
            {
                var aniDB = episode.AniDB_Episode;
                var airDate = aniDB.GetAirDateAsDate();
                return (episode, aniDB, airDate);
            })
            .Where(tuple =>
            {
                // We ignore all other types except the "normal" type.
                if ((EpisodeType)tuple.aniDB.EpisodeType != EpisodeType.Episode)
                    return false;

                // We ignore any unknown air dates and dates in the future.
                if (!tuple.airDate.HasValue || tuple.airDate.Value > now)
                    return false;

                return true;
            })
            .ToList();

        // Threshold used to filter out outliners, e.g. a weekday that only happens
        // once or twice for whatever reason, or when a show gets an early preview,
        // an episode moving, etc..
        var outlierThreshold = Math.Min((int)Math.Ceiling(filteredEpisodes.Count / 12D), 4);
        return filteredEpisodes
            .OrderByDescending(tuple => tuple.aniDB.EpisodeNumber)
            // We check up to the `x` last aired episodes to get a grasp on which days
            // it airs on. This helps reduce variance in days for long-running
            // shows, such as One Piece, etc..
            .Take(includeThreshold)
            .Select(tuple => tuple.airDate.Value.DayOfWeek)
            .GroupBy(weekday => weekday)
            .Where(list => list.Count() > outlierThreshold)
            .Select(list => list.Key)
            .OrderBy(weekday => weekday)
            .ToList();
    }

    public void Populate(AniDB_Anime anime)
    {
        AniDB_ID = anime.AnimeID;
        LatestLocalEpisodeNumber = 0;
        DateTimeUpdated = DateTime.Now;
        DateTimeCreated = DateTime.Now;
        UpdatedAt = DateTime.Now;
        SeriesNameOverride = string.Empty;
    }

    public void CreateAnimeEpisodes(AniDB_Anime anime = null)
    {
        anime ??= GetAnime();
        if (anime == null)
        {
            return;
        }

        var eps = anime.GetAniDBEpisodes();
        // Cleanup deleted episodes
        var epsToRemove = GetEpisodes().Where(a => a.AniDB == null).ToList();
        var filesToUpdate = epsToRemove
            .SelectMany(a => RepoFactory.CR_Video_Episode.GetByAniDBEpisodeId(a.AnidbEpisodeId)).ToList();
        var vlIDsToUpdate = filesToUpdate.Select(a => RepoFactory.Shoko_Video.GetByED2K(a.Hash)?.VideoLocalID)
            .Where(a => a != null).Select(a => a.Value).ToList();
        var requestFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        // remove existing xrefs
        RepoFactory.CR_Video_Episode.Delete(filesToUpdate);
        // queue rescan for the files
        vlIDsToUpdate.Select(id => requestFactory.Create<CommandRequest_ProcessFile>(a => a.VideoLocalID = id))
            .ForEach(a => a.Save());
        RepoFactory.Shoko_Episode.Delete(epsToRemove);

        var one_forth = (int)Math.Round(eps.Count / 4D, 0, MidpointRounding.AwayFromZero);
        var one_half = (int)Math.Round(eps.Count / 2D, 0, MidpointRounding.AwayFromZero);
        var three_forths = (int)Math.Round(eps.Count * 3 / 4D, 0, MidpointRounding.AwayFromZero);

        logger.Trace($"Generating {eps.Count} episodes for {anime.MainTitle}");
        for (var i = 0; i < eps.Count; i++)
        {
            if (i == one_forth)
            {
                logger.Trace($"Generating episodes for {anime.MainTitle}: 25%");
            }

            if (i == one_half)
            {
                logger.Trace($"Generating episodes for {anime.MainTitle}: 50%");
            }

            if (i == three_forths)
            {
                logger.Trace($"Generating episodes for {anime.MainTitle}: 75%");
            }

            if (i == eps.Count - 1)
            {
                logger.Trace($"Generating episodes for {anime.MainTitle}: 100%");
            }

            var ep = eps[i];
            ep.CreateAnimeEpisode(Id);
        }
    }

    public bool NeedsEpisodeUpdate()
    {
        var anime = GetAnime();
        if (anime == null)
        {
            return false;
        }

        return anime.GetAniDBEpisodes()
            .Select(episode => RepoFactory.Shoko_Episode.GetByAnidbEpisodeId(episode.EpisodeID))
            .Any(ep => ep == null || ep.AnidbAnimeId != AniDB_ID) || GetEpisodes()
            .Select(episode => RepoFactory.AniDB_Episode.GetByAnidbEpisodeId(episode.AniDB_EpisodeID))
            .Any(ep => ep == null);
    }

    /// <summary>
    /// Gets the direct parent AnimeGroup this series belongs to
    /// </summary>
    public ShokoGroup ParentGroup => RepoFactory.Shoko_Group.GetByID(ParentGroupId);

    /// <summary>
    /// Gets the very top level AnimeGroup which this series belongs to
    /// </summary>
    public ShokoGroup TopLevelAnimeGroup
    {
        get
        {
            var parentGroup = RepoFactory.Shoko_Group.GetByID(ParentGroupId);

            while (parentGroup?.AnimeGroupParentID != null)
            {
                parentGroup = RepoFactory.Shoko_Group.GetByID(parentGroup.AnimeGroupParentID.Value);
            }

            return parentGroup;
        }
    }

    public List<ShokoGroup> AllGroupsAbove
    {
        get
        {
            var grps = new List<ShokoGroup>();
            try
            {
                int? groupID = ParentGroupId;
                while (groupID.HasValue)
                {
                    var grp = RepoFactory.Shoko_Group.GetByID(groupID.Value);
                    if (grp != null)
                    {
                        grps.Add(grp);
                        groupID = grp.AnimeGroupParentID;
                    }
                    else
                    {
                        groupID = null;
                    }
                }

                return grps;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return grps;
        }
    }

    public void UpdateGroupFilters(HashSet<GroupFilterConditionType> types, Shoko_User user = null)
    {
        IReadOnlyList<Shoko_User> users = new List<Shoko_User> { user };
        if (user == null)
        {
            users = RepoFactory.Shoko_User.GetAll();
        }

        var tosave = new List<ShokoGroup_Filter>();

        var n = new HashSet<GroupFilterConditionType>(types);
        var gfs = RepoFactory.Shoko_Group_Filter.GetWithConditionTypesAndAll(n);
        logger.Trace($"Updating {gfs.Count} Group Filters from Series {GetAnime().MainTitle}");
        foreach (var gf in gfs)
        {
            if (gf.UpdateGroupFilterFromSeries(Contract, null))
            {
                if (!tosave.Contains(gf))
                {
                    tosave.Add(gf);
                }
            }

            foreach (var u in users)
            {
                var cgrp = GetUserContract(u.JMMUserID, n);

                if (gf.UpdateGroupFilterFromSeries(cgrp, u))
                {
                    if (!tosave.Contains(gf))
                    {
                        tosave.Add(gf);
                    }
                }
            }
        }

        RepoFactory.Shoko_Group_Filter.Save(tosave);
    }

    public void DeleteFromFilters()
    {
        foreach (var gf in RepoFactory.Shoko_Group_Filter.GetAll())
        {
            var change = false;
            foreach (var k in gf.SeriesIds.Keys)
            {
                if (gf.SeriesIds[k].Contains(Id))
                {
                    gf.SeriesIds[k].Remove(Id);
                    change = true;
                }
            }

            if (change)
            {
                RepoFactory.Shoko_Group_Filter.Save(gf);
            }
        }
    }

    public static Dictionary<int, HashSet<GroupFilterConditionType>> BatchUpdateContracts(ISessionWrapper session,
        IReadOnlyCollection<ShokoSeries> seriesBatch, bool onlyStats = false)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (seriesBatch == null)
        {
            throw new ArgumentNullException(nameof(seriesBatch));
        }

        var grpFilterCondTypesPerSeries = new Dictionary<int, HashSet<GroupFilterConditionType>>();

        if (seriesBatch.Count == 0)
        {
            return grpFilterCondTypesPerSeries;
        }

        var animeIds = new Lazy<int[]>(() => seriesBatch.Select(s => s.AniDB_ID).ToArray(), false);
        var tvDbByAnime = new Lazy<ILookup<int, Tuple<CR_AniDB_TvDB, TvDB_Show>>>(
            () => RepoFactory.TvDB_Show.GetByAnimeIDs(session, animeIds.Value), false);
        var movieByAnime = new Lazy<Dictionary<int, Tuple<CrossRef_AniDB_Other, MovieDB_Movie>>>(
            () => RepoFactory.TMDB_Movie.GetByAnimeIDs(session, animeIds.Value), false);
        var malXrefByAnime = new Lazy<ILookup<int, CrossRef_AniDB_MAL>>(
            () => RepoFactory.CR_AniDB_MAL.GetByAnimeIDs(session, animeIds.Value), false);
        var defImagesByAnime = new Lazy<Dictionary<int, DefaultAnimeImages>>(
            () => RepoFactory.AniDB_Anime.GetDefaultImagesByAnime(session, animeIds.Value), false);

        foreach (var series in seriesBatch)
        {
            var contract = (CL_AnimeSeries_User)series.Contract?.Clone();
            var seriesOnlyStats = onlyStats;

            if (contract == null)
            {
                contract = new CL_AnimeSeries_User();
                seriesOnlyStats = false;
            }

            contract.AniDB_ID = series.AniDB_ID;
            contract.AnimeGroupID = series.ParentGroupId;
            contract.AnimeSeriesID = series.Id;
            contract.DateTimeUpdated = series.DateTimeUpdated;
            contract.DateTimeCreated = series.DateTimeCreated;
            contract.DefaultAudioLanguage = string.Empty;
            contract.DefaultSubtitleLanguage = string.Empty;
            contract.LatestLocalEpisodeNumber = series.LatestLocalEpisodeNumber;
            contract.LatestEpisodeAirDate = series.LatestEpisodeAirDate;
            contract.AirsOn = series.AirsOn;
            contract.EpisodeAddedDate = series.EpisodeAddedDate;
            contract.MissingEpisodeCount = series.MissingEpisodeCount;
            contract.MissingEpisodeCountGroups = series.MissingEpisodeCountGroups;
            contract.SeriesNameOverride = series.SeriesNameOverride;
            contract.DefaultFolder = string.Empty;
            contract.PlayedCount = 0;
            contract.StoppedCount = 0;
            contract.UnwatchedEpisodeCount = 0;
            contract.WatchedCount = 0;
            contract.WatchedDate = null;
            contract.WatchedEpisodeCount = 0;

            if (!seriesOnlyStats)
            {
                // AniDB contract
                var animeRec = series.GetAnime();

                if (animeRec != null)
                {
                    contract.AniDBAnime = (CL_AniDB_AnimeDetailed)animeRec.Contract.Clone();

                    var aniDbAnime = contract.AniDBAnime.AniDBAnime;

                    if (!defImagesByAnime.Value.TryGetValue(animeRec.AnimeId, out var defImages))
                    {
                        defImages = new DefaultAnimeImages { AnimeID = animeRec.AnimeId };
                    }

                    aniDbAnime.DefaultImagePoster = defImages.GetPosterContractNoBlanks();
                    aniDbAnime.DefaultImageFanart = defImages.GetFanartContractNoBlanks(aniDbAnime);
                    aniDbAnime.DefaultImageWideBanner = defImages.WideBanner?.ToContract();
                }

                // TvDB contracts
                var tvDbCrossRefs = tvDbByAnime.Value[series.AniDB_ID].ToList();

                foreach (var missingTvDbSeries in tvDbCrossRefs.Where(cr => cr.Item2 == null)
                             .Select(cr => cr.Item1))
                {
                    logger.Warn("You are missing database information for TvDB series: {0} - {1}",
                        missingTvDbSeries.TvDBID, missingTvDbSeries.TvDBShow?.MainTitle ?? "Series Not Found");
                }

                contract.CrossRefAniDBTvDBV2 = RepoFactory.CR_AniDB_TvDB.GetV2LinksFromAnime(series.AniDB_ID);
                contract.TvDB_Series = tvDbCrossRefs
                    .Select(s => s.Item2)
                    .ToList();

                // MovieDB contracts

                if (movieByAnime.Value.TryGetValue(series.AniDB_ID, out var movieDbInfo))
                {
                    contract.CrossRefAniDBMovieDB = movieDbInfo.Item1;
                    contract.MovieDB_Movie = movieDbInfo.Item2;
                }
                else
                {
                    contract.CrossRefAniDBMovieDB = null;
                    contract.MovieDB_Movie = null;
                }

                // MAL contracts
                contract.CrossRefAniDBMAL = malXrefByAnime.Value[series.AniDB_ID]
                    .ToList();
            }

            var typesChanged = GetConditionTypesChanged(series.Contract, contract);

            series.Contract = contract;
            grpFilterCondTypesPerSeries.Add(series.AnimeSeriesID, typesChanged);
        }

        return grpFilterCondTypesPerSeries;
    }

    public HashSet<GroupFilterConditionType> UpdateContract(bool onlystats = false)
    {
        var start = DateTime.Now;
        TimeSpan ts;
        var contract = (CL_AnimeSeries_User)Contract?.Clone();
        ts = DateTime.Now - start;
        logger.Trace(
            $"While Updating SERIES {GetAnime()?.MainTitle ?? AniDB_ID.ToString()}, Cloned Series Contract in {ts.TotalMilliseconds}ms");
        if (contract == null)
        {
            contract = new CL_AnimeSeries_User();
            onlystats = false;
        }

        contract.AniDB_ID = AniDB_ID;
        contract.AnimeGroupID = ParentGroupId;
        contract.AnimeSeriesID = Id;
        contract.DateTimeUpdated = DateTimeUpdated;
        contract.DateTimeCreated = DateTimeCreated;
        contract.DefaultAudioLanguage = string.Empty;
        contract.DefaultSubtitleLanguage = string.Empty;
        contract.LatestLocalEpisodeNumber = LatestLocalEpisodeNumber;
        contract.LatestEpisodeAirDate = LatestEpisodeAirDate;
        contract.AirsOn = AirsOn;
        contract.EpisodeAddedDate = EpisodeAddedDate;
        contract.MissingEpisodeCount = MissingEpisodeCount;
        contract.MissingEpisodeCountGroups = MissingEpisodeCountGroups;
        contract.SeriesNameOverride = SeriesNameOverride;
        contract.DefaultFolder = string.Empty;
        contract.PlayedCount = 0;
        contract.StoppedCount = 0;
        contract.UnwatchedEpisodeCount = 0;
        contract.WatchedCount = 0;
        contract.WatchedDate = null;
        contract.WatchedEpisodeCount = 0;
        if (onlystats)
        {
            start = DateTime.Now;
            var types2 = GetConditionTypesChanged(Contract, contract);
            Contract = contract;
            ts = DateTime.Now - start;
            logger.Trace(
                $"While Updating SERIES {GetAnime()?.MainTitle ?? AniDB_ID.ToString()}, Got GroupFilterConditionTypesChanged in {ts.TotalMilliseconds}ms");
            return types2;
        }

        var animeRec = GetAnime();
        var tvDBCrossRefs = GetTvdbCrossReferences();
        var movieDBCrossRef = CrossRefMovieDB.FirstOrDefault();
        MovieDB_Movie movie = null;
        if (movieDBCrossRef != null)
        {
            movie = movieDBCrossRef.GetMovieDB_Movie();
        }

        var sers = new List<TvDB_Show>();
        foreach (var xref in tvDBCrossRefs)
        {
            var tvser = xref.TvDBShow;
            if (tvser != null)
            {
                sers.Add(tvser);
            }
            else
            {
                logger.Warn("You are missing database information for TvDB series: {0}", xref.TvDBID);
            }
        }

        // get AniDB data
        if (animeRec != null)
        {
            start = DateTime.Now;
            if (animeRec.Contract == null)
            {
                RepoFactory.AniDB_Anime.Save(animeRec);
            }

            contract.AniDBAnime = (CL_AniDB_AnimeDetailed)animeRec.Contract.Clone();
            ts = DateTime.Now - start;
            logger.Trace(
                $"While Updating SERIES {GetAnime()?.MainTitle ?? AniDB_ID.ToString()}, Got and Cloned AniDB_Anime Contract in {ts.TotalMilliseconds}ms");
            contract.AniDBAnime.AniDBAnime.DefaultImagePoster = animeRec.GetDefaultPoster()?.ToClient();
            if (contract.AniDBAnime.AniDBAnime.DefaultImagePoster == null)
            {
                var im = animeRec.GetDefaultPosterDetailsNoBlanks();
                if (im != null)
                {
                    contract.AniDBAnime.AniDBAnime.DefaultImagePoster = new CL_AniDB_Anime_DefaultImage
                    {
                        AnimeID = im.ImageID, ImageType = (int)im.ImageType
                    };
                }
            }

            contract.AniDBAnime.AniDBAnime.DefaultImageFanart = animeRec.GetDefaultFanart()?.ToClient();
            if (contract.AniDBAnime.AniDBAnime.DefaultImageFanart == null)
            {
                var im = animeRec.GetDefaultFanartDetailsNoBlanks();
                if (im != null)
                {
                    contract.AniDBAnime.AniDBAnime.DefaultImageFanart = new CL_AniDB_Anime_DefaultImage
                    {
                        AnimeID = im.ImageID, ImageType = (int)im.ImageType
                    };
                }
            }

            contract.AniDBAnime.AniDBAnime.DefaultImageWideBanner = animeRec.GetDefaultWideBanner()?.ToClient();
        }

        contract.CrossRefAniDBTvDBV2 = RepoFactory.CR_AniDB_TvDB.GetV2LinksFromAnime(AniDB_ID);


        contract.TvDB_Series = sers;
        contract.CrossRefAniDBMovieDB = null;
        if (movieDBCrossRef != null)
        {
            contract.CrossRefAniDBMovieDB = movieDBCrossRef;
            contract.MovieDB_Movie = movie;
        }

        contract.CrossRefAniDBMAL = CrossRefMAL?.ToList() ?? new List<CrossRef_AniDB_MAL>();
        start = DateTime.Now;
        var types = GetConditionTypesChanged(Contract, contract);
        ts = DateTime.Now - start;
        logger.Trace(
            $"While Updating SERIES {GetAnime()?.MainTitle ?? AniDB_ID.ToString()}, Got GroupFilterConditionTypesChanged in {ts.TotalMilliseconds}ms");
        Contract = contract;
        return types;
    }


    public static HashSet<GroupFilterConditionType> GetConditionTypesChanged(CL_AnimeSeries_User oldcontract,
        CL_AnimeSeries_User newcontract)
    {
        var h = new HashSet<GroupFilterConditionType>();

        if (oldcontract == null ||
            (oldcontract.AniDBAnime.AniDBAnime.EndDate.HasValue &&
             oldcontract.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now &&
             !(oldcontract.MissingEpisodeCount > 0 ||
               oldcontract.MissingEpisodeCountGroups > 0)) !=
            (newcontract.AniDBAnime.AniDBAnime.EndDate.HasValue &&
             newcontract.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now &&
             !(newcontract.MissingEpisodeCount > 0 || newcontract.MissingEpisodeCountGroups > 0)))
        {
            h.Add(GroupFilterConditionType.CompletedSeries);
        }

        if (oldcontract == null ||
            (oldcontract.MissingEpisodeCount > 0 || oldcontract.MissingEpisodeCountGroups > 0) !=
            (newcontract.MissingEpisodeCount > 0 || newcontract.MissingEpisodeCountGroups > 0))
        {
            h.Add(GroupFilterConditionType.MissingEpisodes);
        }

        if (oldcontract == null ||
            !oldcontract.AniDBAnime.AniDBAnime.GetAllTags()
                .SetEquals(newcontract.AniDBAnime.AniDBAnime.GetAllTags()))
        {
            h.Add(GroupFilterConditionType.Tag);
        }

        if (oldcontract == null ||
            oldcontract.AniDBAnime.AniDBAnime.AirDate != newcontract.AniDBAnime.AniDBAnime.AirDate)
        {
            h.Add(GroupFilterConditionType.AirDate);
        }

        if (oldcontract == null ||
            (oldcontract.CrossRefAniDBTvDBV2 == null || oldcontract.CrossRefAniDBTvDBV2.Count == 0) !=
            (newcontract.CrossRefAniDBTvDBV2 == null || newcontract.CrossRefAniDBTvDBV2.Count == 0))
        {
            h.Add(GroupFilterConditionType.AssignedTvDBInfo);
        }

        if (oldcontract == null ||
            (oldcontract.CrossRefAniDBMAL == null || oldcontract.CrossRefAniDBMAL.Count == 0) !=
            (newcontract.CrossRefAniDBMAL == null || newcontract.CrossRefAniDBMAL.Count == 0))
        {
            h.Add(GroupFilterConditionType.AssignedMALInfo);
        }

        if (oldcontract == null ||
            oldcontract.CrossRefAniDBMovieDB == null != (newcontract.CrossRefAniDBMovieDB == null))
        {
            h.Add(GroupFilterConditionType.AssignedMovieDBInfo);
        }

        if (oldcontract == null ||
            (oldcontract.CrossRefAniDBMovieDB == null &&
             (oldcontract.CrossRefAniDBTvDBV2 == null || oldcontract.CrossRefAniDBTvDBV2.Count == 0) !=
             (newcontract.CrossRefAniDBMovieDB == null &&
              (newcontract.CrossRefAniDBTvDBV2 == null || newcontract.CrossRefAniDBTvDBV2.Count == 0))))
        {
            h.Add(GroupFilterConditionType.AssignedTvDBOrMovieDBInfo);
        }

        if (oldcontract == null ||
            oldcontract.AniDBAnime.AniDBAnime.AnimeType != newcontract.AniDBAnime.AniDBAnime.AnimeType)
        {
            h.Add(GroupFilterConditionType.AnimeType);
        }

        if (oldcontract == null ||
            !oldcontract.AniDBAnime.Stat_AllVideoQuality.SetEquals(newcontract.AniDBAnime.Stat_AllVideoQuality) ||
            !oldcontract.AniDBAnime.Stat_AllVideoQuality_Episodes.SetEquals(
                newcontract.AniDBAnime.Stat_AllVideoQuality_Episodes))
        {
            h.Add(GroupFilterConditionType.VideoQuality);
        }

        if (oldcontract == null ||
            oldcontract.AniDBAnime.AniDBAnime.VoteCount != newcontract.AniDBAnime.AniDBAnime.VoteCount ||
            oldcontract.AniDBAnime.AniDBAnime.TempVoteCount != newcontract.AniDBAnime.AniDBAnime.TempVoteCount ||
            oldcontract.AniDBAnime.AniDBAnime.Rating != newcontract.AniDBAnime.AniDBAnime.Rating ||
            oldcontract.AniDBAnime.AniDBAnime.TempRating != newcontract.AniDBAnime.AniDBAnime.TempRating)
        {
            h.Add(GroupFilterConditionType.AniDBRating);
        }

        if (oldcontract == null || oldcontract.DateTimeCreated != newcontract.DateTimeCreated)
        {
            h.Add(GroupFilterConditionType.SeriesCreatedDate);
        }

        if (oldcontract == null || oldcontract.EpisodeAddedDate != newcontract.EpisodeAddedDate)
        {
            h.Add(GroupFilterConditionType.EpisodeAddedDate);
        }

        if (oldcontract == null ||
            (oldcontract.AniDBAnime.AniDBAnime.EndDate.HasValue &&
             oldcontract.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now) !=
            (newcontract.AniDBAnime.AniDBAnime.EndDate.HasValue &&
             newcontract.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now))
        {
            h.Add(GroupFilterConditionType.FinishedAiring);
        }

        if (oldcontract == null ||
            oldcontract.MissingEpisodeCountGroups > 0 != newcontract.MissingEpisodeCountGroups > 0)
        {
            h.Add(GroupFilterConditionType.MissingEpisodesCollecting);
        }

        if (oldcontract == null ||
            !oldcontract.AniDBAnime.Stat_AudioLanguages.SetEquals(newcontract.AniDBAnime.Stat_AudioLanguages))
        {
            h.Add(GroupFilterConditionType.AudioLanguage);
        }

        if (oldcontract == null ||
            !oldcontract.AniDBAnime.Stat_SubtitleLanguages.SetEquals(newcontract.AniDBAnime.Stat_SubtitleLanguages))
        {
            h.Add(GroupFilterConditionType.SubtitleLanguage);
        }

        if (oldcontract == null ||
            oldcontract.AniDBAnime.AniDBAnime.EpisodeCount != newcontract.AniDBAnime.AniDBAnime.EpisodeCount)
        {
            h.Add(GroupFilterConditionType.EpisodeCount);
        }

        if (oldcontract == null ||
            !oldcontract.AniDBAnime.CustomTags.Select(a => a.TagName)
                .ToHashSet()
                .SetEquals(newcontract.AniDBAnime.CustomTags.Select(a => a.TagName).ToHashSet()))
        {
            h.Add(GroupFilterConditionType.CustomTags);
        }

        if (oldcontract == null || oldcontract.LatestEpisodeAirDate != newcontract.LatestEpisodeAirDate)
        {
            h.Add(GroupFilterConditionType.LatestEpisodeAirDate);
        }

        var oldyear = -1;
        var newyear = -1;
        if (oldcontract?.AniDBAnime?.AniDBAnime?.AirDate != null)
        {
            oldyear = oldcontract.AniDBAnime.AniDBAnime.AirDate.Value.Year;
        }

        if (newcontract?.AniDBAnime?.AniDBAnime?.AirDate != null)
        {
            newyear = newcontract.AniDBAnime.AniDBAnime.AirDate.Value.Year;
        }

        if (oldyear != newyear)
        {
            h.Add(GroupFilterConditionType.Year);
        }

        if (oldcontract?.AniDBAnime?.Stat_AllSeasons == null ||
            !oldcontract.AniDBAnime.Stat_AllSeasons.SetEquals(newcontract.AniDBAnime.Stat_AllSeasons))
        {
            h.Add(GroupFilterConditionType.Season);
        }

        //TODO This three should be moved to AnimeSeries_User in the future...
        if (oldcontract == null ||
            (oldcontract.AniDBAnime.UserVote != null &&
             oldcontract.AniDBAnime.UserVote.VoteType == (int)AniDBVoteType.Anime) !=
            (newcontract.AniDBAnime.UserVote != null &&
             newcontract.AniDBAnime.UserVote.VoteType == (int)AniDBVoteType.Anime))
        {
            h.Add(GroupFilterConditionType.UserVoted);
        }

        if (oldcontract == null ||
            oldcontract.AniDBAnime.UserVote != null != (newcontract.AniDBAnime.UserVote != null))
        {
            h.Add(GroupFilterConditionType.UserVotedAny);
        }

        if (oldcontract == null ||
            (oldcontract.AniDBAnime.UserVote?.VoteValue ?? 0) !=
            (newcontract.AniDBAnime.UserVote?.VoteValue ?? 0))
        {
            h.Add(GroupFilterConditionType.UserRating);
        }

        return h;
    }

    public override string ToString()
    {
        return $"Series: {GetAnime().MainTitle} ({Id})";
        //return string.Empty;
    }

    internal class EpisodeList : List<EpisodeList.StatEpisodes>
    {
        public EpisodeList(AnimeType ept)
        {
            AnimeType = ept;
        }

        private AnimeType AnimeType { get; set; }

        private readonly Regex partmatch = new("part (\\d.*?) of (\\d.*)");

        private readonly Regex remsymbols = new("[^A-Za-z0-9 ]");

        private readonly Regex remmultispace = new("\\s+");

        public void Add(Shoko_Episode ep, bool available)
        {
            var hidden = ep.IsHidden;
            if (AnimeType == AnimeType.OVA || AnimeType == AnimeType.Movie)
            {
                var ename = ep.Title;
                var empty = string.IsNullOrEmpty(ename);
                Match m = null;
                if (!empty)
                {
                    m = partmatch.Match(ename);
                }

                var s = new StatEpisodes.StatEpisode { Available = available, Hidden = hidden };
                if (m?.Success ?? false)
                {
                    int.TryParse(m.Groups[1].Value, out var part_number);
                    int.TryParse(m.Groups[2].Value, out var part_count);
                    var rname = partmatch.Replace(ename, string.Empty);
                    rname = remsymbols.Replace(rname, string.Empty);
                    rname = remmultispace.Replace(rname, " ");


                    s.EpisodeType = StatEpisodes.StatEpisode.EpType.Part;
                    s.PartCount = part_count;
                    s.Match = rname.Trim();
                    if (s.Match == "complete movie" || s.Match == "movie" || s.Match == "ova")
                    {
                        s.Match = string.Empty;
                    }
                }
                else
                {
                    if (empty || ename == "complete movie" || ename == "movie" || ename == "ova")
                    {
                        s.Match = string.Empty;
                    }
                    else
                    {
                        var rname = partmatch.Replace(ep.Title, string.Empty);
                        rname = remsymbols.Replace(rname, string.Empty);
                        rname = remmultispace.Replace(rname, " ");
                        s.Match = rname.Trim();
                    }

                    s.EpisodeType = StatEpisodes.StatEpisode.EpType.Complete;
                    s.PartCount = 0;
                }

                StatEpisodes fnd = null;
                foreach (var k in this)
                {
                    foreach (var ss in k)
                    {
                        if (ss.Match == s.Match)
                        {
                            fnd = k;
                            break;
                        }
                    }

                    if (fnd != null)
                    {
                        break;
                    }
                }

                if (fnd == null)
                {
                    var eps = new StatEpisodes();
                    eps.Add(s);
                    Add(eps);
                }
                else
                {
                    fnd.Add(s);
                }
            }
            else
            {
                var eps = new StatEpisodes();
                var es = new StatEpisodes.StatEpisode
                {
                    Match = string.Empty,
                    EpisodeType = StatEpisodes.StatEpisode.EpType.Complete,
                    PartCount = 0,
                    Available = available,
                    Hidden = hidden,
                };
                eps.Add(es);
                Add(eps);
            }
        }

        public class StatEpisodes : List<StatEpisodes.StatEpisode>
        {
            public class StatEpisode
            {
                public enum EpType
                {
                    Complete,
                    Part
                }

                public string Match;
                public int PartCount;
                public EpType EpisodeType { get; set; }
                public bool Available { get; set; }
                public bool Hidden { get; set; }
            }

            public bool Available
            {
                get
                {
                    var maxcnt = this.Select(k => k.PartCount).Concat(new[] { 0 }).Max();
                    var parts = new int[maxcnt + 1];
                    foreach (var k in this)
                    {
                        switch (k.EpisodeType)
                        {
                            case StatEpisode.EpType.Complete when k.Available:
                                return true;
                            case StatEpisode.EpType.Part when k.Available:
                                parts[k.PartCount]++;
                                if (parts[k.PartCount] == k.PartCount)
                                {
                                    return true;
                                }

                                break;
                        }
                    }

                    return false;
                }
            }

            public bool Hidden
                => this.Any(e => e.Hidden);
        }
    }

    public void MoveSeries(ShokoGroup newGroup)
    {
        var oldGroupID = ParentGroupId;
        // Update the stats for the series and group.
        ParentGroupId = newGroup.Id;
        DateTimeUpdated = DateTime.Now;
        UpdateStats(true, true);
        newGroup.TopLevelAnimeGroup?.UpdateStatsFromTopLevel(true, true);

        var oldGroup = RepoFactory.Shoko_Group.GetByID(oldGroupID);
        if (oldGroup != null)
        {
            // This was the only one series in the group so delete the now orphan group.
            if (oldGroup.GetAllSeries().Count == 0)
            {
                oldGroup.DeleteGroup(false);
            }

            // Update the top group
            var topGroup = oldGroup.TopLevelAnimeGroup;
            if (topGroup.Id != oldGroup.Id)
            {
                topGroup.UpdateStatsFromTopLevel(true, true);
            }
        }
    }

    public void QueueUpdateStats()
    {
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        var cmdRefreshAnime = commandFactory.Create<CommandRequest_RefreshAnime>(c => c.AnimeID = AniDB_ID);
        cmdRefreshAnime.Save();
    }

    public void UpdateStats(bool watchedStats, bool missingEpsStats)
    {
        var start = DateTime.Now;
        var initialStart = DateTime.Now;
        var name = GetAnime()?.MainTitle ?? AniDB_ID.ToString();
        logger.Info(
            $"Starting Updating STATS for SERIES {name} - Watched Stats: {watchedStats}, Missing Episodes: {missingEpsStats}");

        var startEps = DateTime.Now;
        var eps = GetEpisodes().Where(a => a.AniDB_Episode != null).ToList();
        var tsEps = DateTime.Now - startEps;
        logger.Trace($"Got episodes for SERIES {name} in {tsEps.TotalMilliseconds}ms");

        if (watchedStats)
        {
            var vls = RepoFactory.CR_Video_Episode.GetByAnidbAnimeId(AniDB_ID)
                .Where(a => !string.IsNullOrEmpty(a?.Hash)).Select(xref =>
                    (xref.EpisodeID, VideoLocal: RepoFactory.Shoko_Video.GetByED2K(xref.Hash)))
                .Where(a => a.VideoLocal != null).ToLookup(a => a.EpisodeID, b => b.VideoLocal);
            var vlUsers = vls.SelectMany(
                xref =>
                {
                    var users = xref?.SelectMany(a => RepoFactory.Shoko_Video_User.GetByVideoLocalID(a.VideoLocalID));
                    return users?.Select(a => (EpisodeID: xref.Key, VideoLocalUser: a)) ??
                           Array.Empty<(int EpisodeID, Shoko_Video_User VideoLocalUser)>();
                }
            ).Where(a => a.VideoLocalUser != null).ToLookup(a => (a.EpisodeID, UserID: a.VideoLocalUser.JMMUserID),
                b => b.VideoLocalUser);
            var epUsers = eps.SelectMany(
                    ep =>
                    {
                        var users = RepoFactory.Shoko_Episode_User.GetByEpisodeID(ep.AnimeEpisodeID);
                        return users.Select(a => (EpisodeID: ep.AniDB_EpisodeID, AnimeEpisode_User: a));
                    }
                ).Where(a => a.AnimeEpisode_User != null)
                .ToLookup(a => (a.EpisodeID, UserID: a.AnimeEpisode_User.JMMUserID), b => b.AnimeEpisode_User);

            foreach (var juser in RepoFactory.Shoko_User.GetAll())
            {
                var userRecord = GetOrCreateUserRecord(juser.JMMUserID);

                var unwatchedCount = 0;
                var hiddenUnwatchedCount = 0;
                var watchedCount = 0;
                var watchedEpisodeCount = 0;
                DateTime? lastEpisodeUpdate = null;
                DateTime? watchedDate = null;

                var lck = new object();

                eps.AsParallel().Where(ep =>
                    vls.Contains(ep.AniDB_EpisodeID) &&
                    ep.EpisodeTypeEnum is EpisodeType.Episode or EpisodeType.Special).ForAll(
                    ep =>
                    {
                        Shoko_Video_User vlUser = null;
                        if (vlUsers.Contains((ep.AniDB_EpisodeID, juser.JMMUserID)))
                        {
                            vlUser = vlUsers[(ep.AniDB_EpisodeID, juser.JMMUserID)]
                                .OrderByDescending(a => a.LastUpdated)
                                .FirstOrDefault(a => a.WatchedDate != null);
                        }

                        var lastUpdated = vlUser?.LastUpdatedAt;

                        ShokoEpisode_User epUser = null;
                        if (epUsers.Contains((ep.AniDB_EpisodeID, juser.JMMUserID)))
                        {
                            epUser = epUsers[(ep.AniDB_EpisodeID, juser.JMMUserID)]
                                .FirstOrDefault(a => a.WatchedDate != null);
                        }

                        if (vlUser?.LastWatchedAt == null && epUser?.LastWatchedAt == null)
                        {
                            if (ep.IsHidden)
                                Interlocked.Increment(ref hiddenUnwatchedCount);
                            else
                                Interlocked.Increment(ref unwatchedCount);
                            return;
                        }

                        lock (lck)
                        {
                            if (vlUser != null)
                            {
                                if (watchedDate == null || (vlUser.LastWatchedAt != null &&
                                                            vlUser.LastWatchedAt.Value > watchedDate.Value))
                                {
                                    watchedDate = vlUser.LastWatchedAt;
                                }

                                if (lastEpisodeUpdate == null || lastUpdated.Value > lastEpisodeUpdate.Value)
                                {
                                    lastEpisodeUpdate = lastUpdated;
                                }
                            }

                            if (epUser != null)
                            {
                                if (watchedDate == null || (epUser.LastWatchedAt != null &&
                                                            epUser.LastWatchedAt.Value > watchedDate.Value))
                                {
                                    watchedDate = epUser.LastWatchedAt;
                                }
                            }
                        }

                        Interlocked.Increment(ref watchedEpisodeCount);
                        Interlocked.Add(ref watchedCount, vlUser?.WatchedCount ?? epUser.WatchedCount);
                    });
                userRecord.UnwatchedEpisodeCount = unwatchedCount;
                userRecord.HiddenUnwatchedEpisodeCount = hiddenUnwatchedCount;
                userRecord.WatchedEpisodeCount = watchedEpisodeCount;
                userRecord.WatchedCount = watchedCount;
                userRecord.WatchedDate = watchedDate;
                userRecord.LastEpisodeUpdate = lastEpisodeUpdate;
                RepoFactory.Shoko_Series_User.Save(userRecord);
            }
        }

        var ts = DateTime.Now - start;
        logger.Trace($"Updated WATCHED stats for SERIES {name} in {ts.TotalMilliseconds}ms");
        start = DateTime.Now;

        if (missingEpsStats)
        {
            var animeType = GetAnime()?.GetAnimeTypeEnum() ?? AnimeType.TVSeries;

            MissingEpisodeCount = 0;
            MissingEpisodeCountGroups = 0;
            HiddenMissingEpisodeCount = 0;
            HiddenMissingEpisodeCountGroups = 0;

            // get all the group status records
            var grpStatuses = RepoFactory.AniDB_Anime_ReleaseGroup_Status.GetByAnimeID(AniDB_ID);

            // find all the episodes for which the user has a file
            // from this we can determine what their latest episode number is
            // find out which groups the user is collecting

            var latestLocalEpNumber = 0;
            DateTime? lastEpAirDate = null;
            var epReleasedList = new EpisodeList(animeType);
            var epGroupReleasedList = new EpisodeList(animeType);
            var daysofweekcounter = new Dictionary<DayOfWeek, int>();

            var userReleaseGroups = eps.Where(a => a.EpisodeTypeEnum == EpisodeType.Episode).SelectMany(
                a =>
                {
                    var vls = a.GetVideoLocals();
                    if (!vls.Any())
                    {
                        return Array.Empty<int>();
                    }

                    var aniFiles = vls.Select(b => b.GetAniDBFile()).Where(b => b != null).ToList();
                    if (!aniFiles.Any())
                    {
                        return Array.Empty<int>();
                    }

                    return aniFiles.Select(b => b.GroupID);
                }
            ).ToList();

            var videoLocals = eps.Where(a => a.EpisodeTypeEnum == EpisodeType.Episode).SelectMany(a =>
                    a.GetVideoLocals().Select(b => new { a.AniDB_EpisodeID, VideoLocal = b }))
                .ToLookup(a => a.AniDB_EpisodeID, a => a.VideoLocal);

            // This was always Episodes only. Maybe in the future, we'll have a reliable way to check specials.
            eps.AsParallel().Where(a => a.EpisodeTypeEnum == EpisodeType.Episode).ForAll(ep =>
            {
                var vids = videoLocals[ep.AniDB_EpisodeID].ToList();

                var aniEp = ep.AniDB_Episode;
                var thisEpNum = aniEp.EpisodeNumber;

                if (thisEpNum > latestLocalEpNumber && vids.Any())
                {
                    latestLocalEpNumber = thisEpNum;
                }

                var airdate = ep.AniDB_Episode.GetAirDateAsDate();

                // Only count episodes that have already aired
                if (!aniEp.GetFutureDated())
                {
                    // Only convert if we have time info
                    DateTime airdateLocal;
                    // ignore the possible null on airdate, it's checked in GetFutureDated
                    if (airdate!.Value.Hour == 0 && airdate.Value.Minute == 0 && airdate.Value.Second == 0)
                    {
                        airdateLocal = airdate.Value;
                    }
                    else
                    {
                        airdateLocal = DateTime.SpecifyKind(airdate.Value, DateTimeKind.Unspecified);
                        airdateLocal = TimeZoneInfo.ConvertTime(airdateLocal,
                            TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"), TimeZoneInfo.Local);
                    }

                    lock (daysofweekcounter)
                    {
                        if (!daysofweekcounter.ContainsKey(airdateLocal.DayOfWeek))
                        {
                            daysofweekcounter.Add(airdateLocal.DayOfWeek, 0);
                        }

                        daysofweekcounter[airdateLocal.DayOfWeek]++;
                    }

                    if (lastEpAirDate == null || lastEpAirDate < airdate)
                    {
                        lastEpAirDate = airdate.Value;
                    }
                }

                // does this episode have a file released
                // does this episode have a file released by the group the user is collecting
                var epReleased = false;
                var epReleasedGroup = false;
                foreach (var gs in grpStatuses)
                {
                    if (gs.LastEpisodeNumber >= thisEpNum)
                    {
                        epReleased = true;
                    }

                    if (userReleaseGroups.Contains(gs.GroupID) && gs.HasGroupReleasedEpisode(thisEpNum))
                    {
                        epReleasedGroup = true;
                    }
                }

                try
                {
                    lock (epReleasedList)
                    {
                        epReleasedList.Add(ep, !epReleased || vids.Any());
                    }

                    lock (epGroupReleasedList)
                    {
                        epGroupReleasedList.Add(ep, !epReleasedGroup || vids.Any());
                    }
                }
                catch (Exception e)
                {
                    logger.Trace($"Error updating release group stats {e}");
                    throw;
                }
            });

            foreach (var eplst in epReleasedList)
            {
                if (!eplst.Available)
                {
                    if (eplst.Hidden)
                        HiddenMissingEpisodeCount++;
                    else
                        MissingEpisodeCount++;
                }
            }

            foreach (var eplst in epGroupReleasedList)
            {
                if (!eplst.Available)
                {
                    if (eplst.Hidden)
                        HiddenMissingEpisodeCountGroups++;
                    else
                        MissingEpisodeCountGroups++;
                }
            }

            LatestLocalEpisodeNumber = latestLocalEpNumber;
            if (daysofweekcounter.Count > 0)
            {
                AirsOn = daysofweekcounter.OrderByDescending(a => a.Value).FirstOrDefault().Key;
            }

            LatestEpisodeAirDate = lastEpAirDate;
        }

        ts = DateTime.Now - start;
        logger.Trace($"Updated MISSING EPS stats for SERIES {name} in {ts.TotalMilliseconds}ms");
        start = DateTime.Now;

        // Skip group filters if we are doing group stats, as the group stats will regenerate group filters
        RepoFactory.Shoko_Series.Save(this, false, false);
        ts = DateTime.Now - start;
        logger.Trace($"Saved stats for SERIES {name} in {ts.TotalMilliseconds}ms");


        ts = DateTime.Now - initialStart;
        logger.Info($"Finished updating stats for SERIES {name} in {ts.TotalMilliseconds}ms");
    }

    public static Dictionary<ShokoSeries, CrossRef_Anime_Staff> SearchSeriesByStaff(string staffname,
        bool fuzzy = false)
    {
        var allseries = RepoFactory.Shoko_Series.GetAll();
        var results = new Dictionary<ShokoSeries, CrossRef_Anime_Staff>();
        var stringsToSearchFor = new List<string>();
        if (staffname.Contains(" "))
        {
            stringsToSearchFor.AddRange(staffname.Split(' ').GetPermutations()
                .Select(permutation => string.Join(" ", permutation)));
            stringsToSearchFor.Remove(staffname);
            stringsToSearchFor.Insert(0, staffname);
        }
        else
        {
            stringsToSearchFor.Add(staffname);
        }

        foreach (var series in allseries)
        {
            List<(CrossRef_Anime_Staff, AnimeStaff)> staff = RepoFactory.CR_ShokoSeries_ShokoStaff
                .GetByAnimeID(series.AniDB_ID).Select(a => (a, RepoFactory.Shoko_Staff.GetByID(a.StaffID))).ToList();

            foreach (var animeStaff in staff)
            foreach (var search in stringsToSearchFor)
            {
                if (fuzzy)
                {
                    if (!animeStaff.Item2.Name.FuzzyMatch(search))
                    {
                        continue;
                    }
                }
                else
                {
                    if (!animeStaff.Item2.Name.Equals(search, StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }
                }

                if (!results.ContainsKey(series))
                {
                    results.Add(series, animeStaff.Item1);
                }
                else
                {
                    if (!Enum.TryParse(results[series].Role, out CharacterAppearanceType type1))
                    {
                        continue;
                    }

                    if (!Enum.TryParse(animeStaff.Item1.Role, out CharacterAppearanceType type2))
                    {
                        continue;
                    }

                    var comparison = ((int)type1).CompareTo((int)type2);
                    if (comparison == 1)
                    {
                        results[series] = animeStaff.Item1;
                    }
                }

                goto label0;
            }

            // People hate goto, but this is a legit use for it.
            label0: ;
        }

        return results;
    }

    public void DeleteSeries(bool deleteFiles, bool updateGroups)
    {
        GetEpisodes().ForEach(ep =>
        {
            ep.RemoveVideoLocals(deleteFiles);
            RepoFactory.Shoko_Episode.Delete(ep.AnimeEpisodeID);
        });
        RepoFactory.Shoko_Series.Delete(this);

        if (!updateGroups)
        {
            return;
        }

        // finally update stats
        var grp = ParentGroup;
        if (grp != null)
        {
            if (!grp.GetAllSeries().Any())
            {
                // Find the topmost group without series
                var parent = grp;
                while (true)
                {
                    var next = parent.Parent;
                    if (next == null || next.GetAllSeries().Any())
                    {
                        break;
                    }

                    parent = next;
                }

                parent.DeleteGroup();
            }
            else
            {
                grp.UpdateStatsFromTopLevel(true, true);
            }
        }
    }

    #region IShokoSeries

    #region Identifiers

    int IShokoSeries.Id => Id;

    int IShokoSeries.ParentGroupId => ParentGroupId;

    int IShokoSeries.TopLevelGroupId => TopLevelAnimeGroup.Id;

    int IShokoSeries.AnidbAnimeId => AniDB_ID;

    #endregion

    #region Links

    IShokoGroup IShokoSeries.ParentGroup => ParentGroup;

    IShokoGroup IShokoSeries.TopLevelGroup => TopLevelAnimeGroup;

    IShowMetadata IShokoSeries.AnidbAnime => GetAnime();

    IReadOnlyList<IMovieMetadata> IShokoSeries.AllMovies => new List<IMovieMetadata>();

    IReadOnlyList<IShowMetadata> IShokoSeries.AllShows
    {
        get
        {
            var list = new List<IShowMetadata>();
            list.AddRange(GetTvdbShows());
            return list;
        }
    }

    IReadOnlyList<IShokoVideoCrossReference> IShokoSeries.AllCrossReferences =>
        RepoFactory.CR_Video_Episode.GetByAnidbAnimeId(AniDB_ID);

    IReadOnlyList<IShokoEpisode> IShokoSeries.AllEpisodes => null;

    IReadOnlyList<IShokoVideo> IShokoSeries.AllVideos => null;

    #endregion

    #region Metadata

    DateTime IShokoSeries.CreatedAt => DateTimeCreated;

    DateTime IShokoSeries.LastUpdatedAt => UpdatedAt;

    #endregion

    #endregion
}
