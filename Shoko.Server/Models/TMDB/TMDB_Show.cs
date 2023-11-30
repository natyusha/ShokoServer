using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using TMDbLib.Objects.TvShows;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_Show : TMDB_Base<int>, IEntityMetadata
{
    #region Properties

    public override int Id => TmdbShowID;

    /// <summary>
    /// Local id.
    /// </summary>
    public int TMDB_ShowID { get; }

    /// <summary>
    /// TMDB Show Id.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// The english title of the show, used as a fallback for when no title is
    /// available in the preferred language.
    /// </summary>
    public string EnglishTitle { get; set; } = string.Empty;

    /// <summary>
    /// The english overview, used as a fallback for when no overview is
    /// available in the preferred language.
    /// </summary>
    public string EnglishOverview { get; set; } = string.Empty;

    /// <summary>
    /// Original title in the original language.
    /// </summary>
    public string OriginalTitle { get; set; } = string.Empty;

    /// <summary>
    /// The original language this show was shot in, just as a title language
    /// enum instead.
    /// </summary>
    public TitleLanguage OriginalLanguage
    {
        get => string.IsNullOrEmpty(OriginalLanguageCode) ? TitleLanguage.None : OriginalLanguageCode.GetTitleLanguage();
    }

    /// <summary>
    /// The original language this show was shot in.
    /// </summary>
    public string OriginalLanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// Indicates the show is restricted to an age group above the legal age,
    /// because it's a pornography.
    /// </summary>
    public bool IsRestricted { get; set; }

    /// <summary>
    /// Genres.
    /// </summary>
    public List<string> Genres { get; set; } = new();

    /// <summary>
    /// Content ratings for different countries for this show.
    /// </summary>
    public List<TMDB_ContentRating> ContentRatings { get; set; } = new();

    /// <summary>
    /// Number of episodes using the default ordering.
    /// </summary>
    public int EpisodeCount { get; set; }

    /// <summary>
    /// Number of seasons using the default ordering.
    /// </summary>
    public int SeasonCount { get; set; }

    /// <summary>
    /// Number of alternate ordering schemas available for this show.
    /// </summary>
    public int AlternateOrderingCount { get; set; }

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
    /// First aired episode date.
    /// </summary>
    public DateOnly? FirstAiredAt { get; set; }

    /// <summary>
    /// Last aired episode date for the show, or null if the show is still
    /// running.
    /// </summary>
    public DateOnly? LastAiredAt { get; set; }

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

    public TMDB_Show() { }

    public TMDB_Show(int showId)
    {
        TmdbShowID = showId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion

    #region Methods

    public bool Populate(TvShow show)
    {
        // Don't trust 'show.Name' for the engrish title since it will fall-back
        // to the original language if there is no title in engrish.
        var translation = show.Translations.Translations.FirstOrDefault(translation => translation.Iso_639_1 == "en");

        var updates = new[]
        {
            UpdateProperty(OriginalTitle, show.OriginalName, v => OriginalTitle = v),
            UpdateProperty(OriginalLanguageCode, show.OriginalLanguage, v => OriginalLanguageCode = v),
            UpdateProperty(EnglishTitle, translation?.Data.Name ?? show.Name, v => EnglishTitle = v),
            UpdateProperty(EnglishOverview, translation?.Data.Overview ?? show.Name, v => EnglishOverview = v),
            // TODO: Waiting for https://github.com/Jellyfin/TMDbLib/pull/443 to be merged to uncomment the next line.
            UpdateProperty(IsRestricted, false /* show.Adult */, v => IsRestricted = v),
            UpdateProperty(Genres, show.Genres.SelectMany(genre => genre.Name.Split('&', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)).ToList(), v => Genres = v),
            UpdateProperty(ContentRatings, show.ContentRatings.Results.Select(rating => new TMDB_ContentRating(rating.Iso_3166_1.FromIso3166ToIso639().GetTitleLanguage(), rating.Rating)).ToList(), v => ContentRatings = v),
            UpdateProperty(EpisodeCount, show.NumberOfEpisodes, v => EpisodeCount = v),
            UpdateProperty(SeasonCount, show.NumberOfSeasons, v => SeasonCount = v),
            UpdateProperty(AlternateOrderingCount, show.EpisodeGroups.Results.Count, v => AlternateOrderingCount = v),
            UpdateProperty(UserRating, show.VoteAverage, v => UserRating = v),
            UpdateProperty(UserVotes, show.VoteCount, v => UserVotes = v),
            UpdateProperty(FirstAiredAt, show.FirstAirDate.HasValue ? DateOnly.FromDateTime(show.FirstAirDate.Value) : null, v => FirstAiredAt = v),
            UpdateProperty(LastAiredAt, !string.IsNullOrEmpty(show.Status) && show.Status.Equals("Ended", StringComparison.InvariantCultureIgnoreCase) && show.LastAirDate.HasValue ? DateOnly.FromDateTime(show.LastAirDate.Value) : null, v => LastAiredAt = v),
        };

        return updates.Any(updated => updated);
    }

    public TMDB_Title? GetPreferredTitle(bool useFallback = false)
    {
        // TODO: Implement this logic once the repositories are added.

        // Fallback.
        return useFallback ? new(ForeignEntityType.Show, TmdbShowID, EnglishTitle, "en", "US") : null;
    }

    public IReadOnlyList<TMDB_Title> GetAllTitles() =>
        RepoFactory.TMDB_Title.GetByParentTypeAndID(ForeignEntityType.Show, TmdbShowID);

    public TMDB_Overview? GetPreferredOverview(bool useFallback = false)
    {
        // TODO: Implement this logic once the repositories are added.

        return useFallback ? new(ForeignEntityType.Show, TmdbShowID, EnglishOverview, "en", "US") : null;
    }

    public IReadOnlyList<TMDB_Overview> GetAllOverviews() =>
        RepoFactory.TMDB_Overview.GetByParentTypeAndID(ForeignEntityType.Show, TmdbShowID);

    public IReadOnlyList<TMDB_AlternateOrdering> GetTmdbAlternateOrdering() =>
        RepoFactory.TMDB_AlternateOrdering.GetByTmdbShowID(TmdbShowID);

    public IReadOnlyList<TMDB_Season> GetTmdbSeasons() =>
        RepoFactory.TMDB_Season.GetByTmdbShowID(TmdbShowID);

    public IReadOnlyList<TMDB_Episode> GetTmdbEpisodes() =>
        RepoFactory.TMDB_Episode.GetByTmdbShowID(TmdbShowID);

    public IReadOnlyList<CrossRef_AniDB_TMDB_Show> GetCrossReferences() =>
        RepoFactory.CrossRef_AniDB_TMDB_Show.GetByTmdbShowID(TmdbShowID);

    #endregion

    #region IEntityMetadata

    ForeignEntityType IEntityMetadata.Type => ForeignEntityType.Show;

    DataSourceType IEntityMetadata.DataSource => DataSourceType.TMDB;

    TitleLanguage? IEntityMetadata.OriginalLanguage => OriginalLanguage;

    DateOnly? IEntityMetadata.ReleasedAt => FirstAiredAt;

    #endregion
}

