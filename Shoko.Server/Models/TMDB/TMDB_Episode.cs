using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.TvShows;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_Episode : TMDB_Base<int>, IEntityMetadata
{
    #region Properties

    public override int Id => TmdbEpisodeID;

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_EpisodeID { get; set; }

    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Season ID.
    /// </summary>
    public int TmdbSeasonID { get; set; }

    /// <summary>
    /// TMDB Episode ID.
    /// </summary>
    public int TmdbEpisodeID { get; set; }

    /// <summary>
    /// The english title of the episode, used as a fallback for when no title
    /// is available in the preferred language.
    /// </summary>
    public string EnglishTitle { get; set; } = string.Empty;

    /// <summary>
    /// The english overview, used as a fallback for when no overview is
    /// available in the preferred language.
    /// </summary>
    public string EnglishOverview { get; set; } = string.Empty;

    /// <summary>
    /// Season number for default ordering.
    /// </summary>
    public int SeasonNumber { get; set; }

    /// <summary>
    /// Episode number for default ordering.
    /// </summary>
    public int EpisodeNumber { get; set; }

    /// <summary>
    /// Episode run-time in minutes.
    /// </summary>
    public int? RuntimeMintues
    {
        get => Runtime.HasValue ? (int)Math.Floor(Runtime.Value.TotalMinutes) : null;
        set => Runtime = value.HasValue ? TimeSpan.FromMinutes(value.Value) : null;
    }

    /// <summary>
    /// Episode run-time.
    /// </summary>
    public TimeSpan? Runtime { get; set; }

    /// <summary>
    /// Average user rating across all <see cref="UserVotes"/>.
    /// </summary>
    public double UserRating { get; set; }

    /// <summary>
    /// Number of users that cast a vote for a rating of this show.
    /// </summary>
    /// <value></value>
    public int UserVotes { get; set; }

    /// <summary>
    /// When the episode aired, or when it will air in the future if it's known.
    /// </summary>
    public DateOnly? AiredAt { get; set; }

    /// <summary>
    /// When the metadata was first downloaded.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the metadata was last syncronized with the remote.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region Constructors

    public TMDB_Episode() { }

    public TMDB_Episode(int episodeId)
    {
        TmdbEpisodeID = episodeId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion

    #region Methods

    public bool Populate(TvShow show, TvSeason season, TvSeasonEpisode episode, TranslationsContainer translations)
    {
        var translation = translations.Translations.FirstOrDefault(translation => translation.Iso_639_1 == "en");

        var updates = new[]
        {
            UpdateProperty(TmdbSeasonID, season.Id!.Value, v => TmdbSeasonID = v),
            UpdateProperty(TmdbShowID, show.Id, v => TmdbShowID = v),
            UpdateProperty(EnglishTitle, translation?.Data.Name ?? episode.Name, v => EnglishTitle = v),
            UpdateProperty(EnglishOverview, translation?.Data.Overview ?? episode.Overview, v => EnglishOverview = v),
            UpdateProperty(SeasonNumber, episode.SeasonNumber, v => SeasonNumber = v),
            UpdateProperty(EpisodeNumber, episode.EpisodeNumber, v => EpisodeNumber = v),
            UpdateProperty(Runtime, episode.Runtime.HasValue ? TimeSpan.FromMinutes(episode.Runtime.Value) : null, v => Runtime = v),
            UpdateProperty(UserRating, episode.VoteAverage, v => UserRating = v),
            UpdateProperty(UserVotes, episode.VoteCount, v => UserVotes = v),
            UpdateProperty(AiredAt, episode.AirDate.HasValue ? DateOnly.FromDateTime(episode.AirDate.Value) : null, v => AiredAt = v),
        };

        return updates.Any(updated => updated);
    }

    public TMDB_Title? GetPreferredTitle(bool useFallback = false)
    {
        // TODO: Implement this logic once the repositories are added.

        // Fallback.
        return useFallback ? new(ForeignEntityType.Episode, TmdbEpisodeID, EnglishTitle, "en", "US") : null;
    }

    public IReadOnlyList<TMDB_Title> GetAllTitles() =>
        RepoFactory.TMDB_Title.GetByParentTypeAndID(ForeignEntityType.Episode, TmdbEpisodeID);

    public TMDB_Overview? GetPreferredOverview(bool useFallback = false)
    {
        // TODO: Implement this logic once the repositories are added.

        return useFallback ? new(ForeignEntityType.Episode, TmdbEpisodeID, EnglishOverview, "en", "US") : null;
    }

    public IReadOnlyList<TMDB_Overview> GetAllOverviews() =>
        RepoFactory.TMDB_Overview.GetByParentTypeAndID(ForeignEntityType.Episode, TmdbEpisodeID);

    public TMDB_Season? GetTmdbSeason() =>
        RepoFactory.TMDB_Season.GetByTmdbSeasonID(TmdbSeasonID);

    public TMDB_Show? GetTmdbShow() =>
        RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID);

    public IReadOnlyList<TMDB_AlternateOrdering_Episode> GetTmdbAlternateOrderingEpisodes() =>
        RepoFactory.TMDB_AlternateOrdering_Episode.GetByTmdbEpisodeID(TmdbEpisodeID);

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetCrossReferences() =>
        RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByTmdbEpisodeID(TmdbEpisodeID);

    #endregion

    #region IEntityMetadata

    ForeignEntityType IEntityMetadata.Type => ForeignEntityType.Episode;

    DataSourceType IEntityMetadata.DataSource => DataSourceType.TMDB;

    string? IEntityMetadata.OriginalTitle => null;

    TitleLanguage? IEntityMetadata.OriginalLanguage => null;

    string? IEntityMetadata.OriginalLanguageCode => null;

    DateOnly? IEntityMetadata.ReleasedAt => AiredAt;

    #endregion
}
