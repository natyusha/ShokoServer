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
using Shoko.Server.Utilities;
using TMDbLib.Objects.TvShows;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// The Movie DataBase (TMDB) Show Database Model.
/// </summary>
public class TMDB_Show : TMDB_Base<int>, IEntityMetadata
{
    #region Properties

    /// <summary>
    /// IEntityMetadata.Id
    /// </summary>
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

    /// <summary>
    /// Constructor for NHibernate to work correctly while hydrating the rows
    /// from the database.
    /// </summary>
    public TMDB_Show() { }

    /// <summary>
    /// Constructor to create a new show in the provider.
    /// </summary>
    /// <param name="showId">The TMDB show id.</param>
    public TMDB_Show(int showId)
    {
        TmdbShowID = showId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Populate the fields from the raw data.
    /// </summary>
    /// <param name="show">The raw TMDB Tv Show object.</param>
    /// <returns>True if any of the fields have been updated.</returns>
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
            UpdateProperty(IsRestricted, show.Adult, v => IsRestricted = v),
            UpdateProperty(Genres, show.Genres.SelectMany(genre => genre.Name.Split('&', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)).ToList(), v => Genres = v),
            UpdateProperty(ContentRatings, show.ContentRatings.Results.Select(rating => new TMDB_ContentRating(rating.Iso_3166_1, rating.Rating)).ToList(), v => ContentRatings = v),
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

    /// <summary>
    /// Get the preferred title using the preferred series title preferrence
    /// from the application settings.
    /// </summary>
    /// <param name="useFallback">Use a fallback title if no title was found in
    /// any of the preferred languages.</param>
    /// <param name="force">Forcefully re-fetch all show titles if they're
    /// already cached from a previous call to <seealso cref="GetAllTitles"/>.
    /// </param>
    /// <returns>The preferred show title, or null if no preferred title was
    /// found.</returns>
    public TMDB_Title? GetPreferredTitle(bool useFallback = false, bool force = false)
    {
        var titles = GetAllTitles(force);

        foreach (var preferredLanguage in Languages.PreferredNamingLanguages)
        {
            var title = titles.FirstOrDefault(title => title.Language == preferredLanguage.Language);
            if (title != null)
                return title;
        }

        return useFallback ? new(ForeignEntityType.Show, TmdbShowID, EnglishTitle, "en", "US") : null;
    }

    /// <summary>
    /// Cached reference to all titles for the show, so we won't have to hit the
    /// database twice to get all titles _and_ the preferred title.
    /// </summary>
    private IReadOnlyList<TMDB_Title>? _allTitles = null;

    /// <summary>
    /// Get all titles for the show.
    /// </summary>
    /// <param name="force">Forcefully re-fetch all show titles if they're
    /// already cached from a previous call. </param>
    /// <returns>All titles for the show.</returns>
    public IReadOnlyList<TMDB_Title> GetAllTitles(bool force = false) => force
        ? _allTitles = RepoFactory.TMDB_Title.GetByParentTypeAndID(ForeignEntityType.Show, TmdbShowID)
        : _allTitles ??= RepoFactory.TMDB_Title.GetByParentTypeAndID(ForeignEntityType.Show, TmdbShowID);

    /// <summary>
    /// Get the preferred overview using the preferred episode title preferrence
    /// from the application settings.
    /// </summary>
    /// <param name="useFallback">Use a fallback overview if no overview was
    /// found in any of the preferred languages.</param>
    /// <param name="force">Forcefully re-fetch all episode overviews if they're
    /// already cached from a previous call to
    /// <seealso cref="GetAllOverviews"/>.
    /// </param>
    /// <returns>The preferred episode overview, or null if no preferred
    /// overview was found.</returns>
    public TMDB_Overview? GetPreferredOverview(bool useFallback = false, bool force = false)
    {
        var overviews = GetAllOverviews(force);

        foreach (var preferredLanguage in Languages.PreferredEpisodeNamingLanguages)
        {
            var overview = overviews.FirstOrDefault(overview => overview.Language == preferredLanguage.Language);
            if (overview != null)
                return overview;
        }

        return useFallback ? new(ForeignEntityType.Show, TmdbShowID, EnglishOverview, "en", "US") : null;
    }

    /// <summary>
    /// Cached reference to all overviews for the show, so we won't have to
    /// hit the database twice to get all overviews _and_ the preferred
    /// overview.
    /// </summary>
    private IReadOnlyList<TMDB_Overview>? _allOverviews = null;

    /// <summary>
    /// Get all overviews for the show.
    /// </summary>
    /// <param name="force">Forcefully re-fetch all show overviews if they're
    /// already cached from a previous call. </param>
    /// <returns>All overviews for the show.</returns>
    public IReadOnlyList<TMDB_Overview> GetAllOverviews(bool force = false) => force
        ? _allOverviews = RepoFactory.TMDB_Overview.GetByParentTypeAndID(ForeignEntityType.Show, TmdbShowID)
        : _allOverviews ??= RepoFactory.TMDB_Overview.GetByParentTypeAndID(ForeignEntityType.Show, TmdbShowID);

    /// <summary>
    /// Get all images for the show, or all images for the given
    /// <paramref name="entityType"/> provided for the show.
    /// </summary>
    /// <param name="entityType">If set, will restrict the returned list to only
    /// containing the images of the given entity type.</param>
    /// <returns>A read-only list of images that are linked to the epiosde.
    /// </returns>
    public IReadOnlyList<TMDB_Image> GetImages(ImageEntityType? entityType = null) => entityType.HasValue
        ? RepoFactory.TMDB_Image.GetByTmdbShowIDAndType(TmdbShowID, entityType.Value)
        : RepoFactory.TMDB_Image.GetByTmdbShowID(TmdbShowID);

    /// <summary>
    /// Get all TMDB company cross-references linked to the show.
    /// </summary>
    /// <returns>All TMDB company cross-references linked to the show.</returns>
    public IReadOnlyList<TMDB_Company_Entity> GetTmdbCompanyCrossReferences() =>
        RepoFactory.TMDB_Company_Entity.GetByTmdbEntityTypeAndID(ForeignEntityType.Show, TmdbShowID);

    /// <summary>
    /// Get all TMDB companies linked to the show.
    /// </summary>
    /// <returns>All TMDB companies linked to the show.</returns>
    public IReadOnlyList<TMDB_Company> GetTmdbCompanies() =>
        GetTmdbCompanyCrossReferences()
            .Select(xref => xref.GetTmdbCompany())
            .OfType<TMDB_Company>()
            .ToList();

    /// <summary>
    /// Get all TMDB alternate ordering schemes assosiated with the show in the
    /// local database. You need alternate ordering to be enabled in the
    /// settings file for these to be populated.
    /// </summary>
    /// <returns>The list of TMDB alternate ordering schemes.</returns>
    public IReadOnlyList<TMDB_AlternateOrdering> GetTmdbAlternateOrdering() =>
        RepoFactory.TMDB_AlternateOrdering.GetByTmdbShowID(TmdbShowID);

    /// <summary>
    /// Get all TMDB seasons assosiated with the show in the local database. Or
    /// an empty list if the show data have not been downloaded yet or have been
    /// purged from the local database for whatever reason.
    /// </summary>
    /// <returns>The TMDB seasons.</returns>
    public IReadOnlyList<TMDB_Season> GetTmdbSeasons() =>
        RepoFactory.TMDB_Season.GetByTmdbShowID(TmdbShowID);

    /// <summary>
    /// Get all TMDB episodes assosiated with the show in the local database. Or
    /// an empty list if the show data have not been downloaded yet or have been
    /// purged from the local database for whatever reason.
    /// </summary>
    /// <returns>The TMDB episodes.</returns>
    public IReadOnlyList<TMDB_Episode> GetTmdbEpisodes() =>
        RepoFactory.TMDB_Episode.GetByTmdbShowID(TmdbShowID);

    /// <summary>
    /// Get AniDB/TMDB cross-references for the show.
    /// </summary>
    /// <returns>The cross-references.</returns>
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

