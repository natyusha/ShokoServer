using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;

using MatchRatingEnum = Shoko.Models.Enums.MatchRating;

#nullable enable
namespace Shoko.Server.API.v3.Models.TMDB;

/// <summary>
/// APIv3 The Movie DataBase (TMDB) Episode Data Transfer Object (DTO).
/// </summary>
public class Episode
{
    /// <summary>
    /// TMDB Episode ID.
    /// </summary>
    public int ID;

    /// <summary>
    /// TMDB Season ID.
    /// </summary>
    public string SeasonID;

    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    public int ShowID;

    /// <summary>
    /// Preferred title based upon episode title preference.
    /// </summary>
    public string Title;

    /// <summary>
    /// All available titles for the episode, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Title>? Titles;

    /// <summary>
    /// Preferred overview based upon episode title preference.
    /// </summary>
    public string Overview;

    /// <summary>
    /// All available overviews for the episode, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Overview>? Overviews;

    /// <summary>
    /// The episode number for the main ordering or alternate ordering in use.
    /// </summary>
    public int EpisodeNumber;

    /// <summary>
    /// The season number for the main ordering or alternate ordering in use.
    /// </summary>
    public int SeasonNumber;

    /// <summary>
    /// All available ordering for the episode, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<OrderingInformation>? Ordering;

    /// <summary>
    /// User rating of the episode from TMDB users.
    /// </summary>
    public Rating UserRating;

    /// <summary>
    /// The episode run-time, if it is known.
    /// </summary>
    public TimeSpan? Runtime;

    /// <summary>
    /// The date the episode first aired, if it is known.
    /// </summary>
    public DateOnly? AiredAt;

    /// <summary>
    /// When the local metadata was first created.
    /// </summary>
    public DateTime CreatedAt;

    /// <summary>
    /// When the local metadata was last updated with new changes from the
    /// remote.
    /// </summary>
    public DateTime LastUpdatedAt;

    public Episode(TMDB_Episode episode, bool includeTitles = true, bool includeOverviews = true, bool includeOrdering = false) :
        this(episode, null, includeTitles, includeOverviews, includeOrdering)
    { }

    public Episode(TMDB_Episode episode, TMDB_AlternateOrdering_Episode? alternateOrderingEpisode, bool includeTitles = true, bool includeOverviews = true, bool includeOrdering = false)
    {
        var preferredOverview = episode.GetPreferredOverview(true);
        var preferredTitle = episode.GetPreferredTitle(true);

        ID = episode.TmdbEpisodeID;
        SeasonID = alternateOrderingEpisode != null
         ? alternateOrderingEpisode.TmdbEpisodeGroupID
         : episode.TmdbSeasonID.ToString();
        ShowID = episode.TmdbShowID;

        Title = preferredTitle!.Value;
        if (includeTitles)
            Titles = episode.GetAllTitles()
                .Select(title => new Title(title, episode.EnglishTitle, preferredTitle))
                .OrderByDescending(title => title.Preferred)
                .ThenByDescending(title => title.Default)
                .ThenBy(title => title.Language)
                .ToList();

        Overview = preferredOverview!.Value;
        if (includeOverviews)
            Overviews = episode.GetAllOverviews()
                .Select(title => new Overview(title, episode.EnglishOverview, preferredOverview))
                .OrderByDescending(title => title.Preferred)
                .ThenByDescending(title => title.Default)
                .ThenBy(title => title.Language)
                .ToList();

        if (alternateOrderingEpisode != null)
        {
            EpisodeNumber = alternateOrderingEpisode.EpisodeNumber;
            SeasonNumber = alternateOrderingEpisode.SeasonNumber;
        }
        else
        {
            EpisodeNumber = episode.EpisodeNumber;
            SeasonNumber = episode.SeasonNumber;
        }
        if (includeOrdering)
        {
            var ordering = new List<OrderingInformation>
            {
                new(episode, alternateOrderingEpisode),
            };
            foreach (var altOrderEp in episode.GetTmdbAlternateOrderingEpisodes())
                ordering.Add(new(altOrderEp, alternateOrderingEpisode));
            Ordering = ordering
                .OrderByDescending(o => o.InUse)
                .ThenByDescending(o => string.IsNullOrEmpty(o.OrderingID))
                .ThenBy(o => o.OrderingName)
                .ToList();
        }

        UserRating = new()
        {
            Value = (decimal)episode.UserRating,
            MaxValue = 10,
            Votes = episode.UserVotes,
            Source = "TMDB",
        };

        Runtime = episode.Runtime;
        AiredAt = episode.AiredAt;
        CreatedAt = episode.CreatedAt.ToUniversalTime();
        LastUpdatedAt = episode.LastUpdatedAt.ToUniversalTime();
    }

    public class OrderingInformation
    {
        /// <summary>
        /// The ordering ID. Will be null for the main ordering, or a hex id for
        /// any alternate ordering.
        /// </summary>
        public string? OrderingID;

        /// <summary>
        /// The alternate ordering type. Will not be set if the main ordering is
        /// used.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore), JsonConverter(typeof(StringEnumConverter))]
        public AlternateOrderingType? OrderingType;

        /// <summary>
        /// English name of the alternate ordering scheme.
        /// </summary>
        public string OrderingName;

        /// <summary>
        /// The season id. Will be a stringified integer for the main ordering,
        /// or a hex id any alternate ordering.
        /// </summary>
        public string SeasonID;

        /// <summary>
        /// English name of the season.
        /// </summary>
        public string SeasonName = string.Empty;

        /// <summary>
        /// The season number for the ordering.
        /// </summary>
        public int SeasonNumber;

        /// <summary>
        /// The episode number for the ordering.
        /// </summary>
        public int EpisodeNumber;

        /// <summary>
        /// Indicates the curent ordering is in use for the episode.
        /// </summary>
        public bool InUse;

        public OrderingInformation(TMDB_Episode episode, TMDB_AlternateOrdering_Episode? alternateOrderingEpisodeInUse)
        {
            var season = episode.GetTmdbSeason();
            OrderingID = null;
            OrderingName = "Seasons";
            OrderingType = null;
            SeasonID = episode.TmdbSeasonID.ToString();
            SeasonName = season?.EnglishTitle ?? "<unknown name>";
            SeasonNumber = episode.SeasonNumber;
            EpisodeNumber = episode.EpisodeNumber;
            InUse = alternateOrderingEpisodeInUse == null;
        }

        public OrderingInformation(TMDB_AlternateOrdering_Episode episode, TMDB_AlternateOrdering_Episode? alternateOrderingEpisodeInUse)
        {
            var ordering = episode.GetTmdbAlternateOrdering();
            var season = episode.GetTmdbAlternateOrderingSeason();
            OrderingID = episode.TmdbEpisodeGroupCollectionID;
            OrderingName = ordering?.EnglishTitle ?? "<unknown name>";
            OrderingType = ordering?.Type ?? AlternateOrderingType.Unknown;
            SeasonID = episode.TmdbEpisodeGroupID;
            SeasonName = season?.EnglishTitle ?? "<unknown name>";
            SeasonNumber = episode.SeasonNumber;
            EpisodeNumber = episode.EpisodeNumber;
            InUse = alternateOrderingEpisodeInUse != null &&
                episode.TMDB_AlternateOrdering_EpisodeID == alternateOrderingEpisodeInUse.TMDB_AlternateOrdering_EpisodeID;
        }
    }

    /// <summary>
    /// APIv3 The Movie DataBase (TMDB) Episode Cross-Reference Data Transfer Object (DTO).
    /// </summary>
    public class CrossReference
    {
        /// <summary>
        /// AniDB Anime ID.
        /// </summary>
        public int AnidbAnimeID;

        /// <summary>
        /// AniDB Episode ID.
        /// </summary>
        public int AnidbEpisodeID;

        /// <summary>
        /// TMDB Show ID.
        /// </summary>
        public int TmdbShowID;

        /// <summary>
        /// TMDB Episode ID. May be null if the <see cref="AnidbEpisodeID"/> is
        /// not mapped to a TMDB Episode yet.
        /// </summary>
        public int? TmdbEpisodeID;

        /// <summary>
        /// The index to order the cross-references if multiple refererences
        /// exists for the same anidb or tmdb episode.
        /// </summary>
        public int Index;

        /// <summary>
        /// The match rating.
        /// </summary>
        public string MatchRating;

        public CrossReference(CrossRef_AniDB_TMDB_Episode xref)
        {
            AnidbAnimeID = xref.AnidbAnimeID;
            AnidbEpisodeID = xref.AnidbEpisodeID;
            TmdbShowID = xref.TmdbShowID;
            TmdbEpisodeID = xref.TmdbEpisodeID == 0 ? null : xref.TmdbEpisodeID;
            MatchRating = "None";
            if (xref.MatchRating != MatchRatingEnum.SarahJessicaParker)
                MatchRating = xref.MatchRating.ToString();
        }
    }
}
