using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using TMDbLib.Objects.Collections;
using TMDbLib.Objects.General;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_Collection : TMDB_Base<int>, IEntityMetadata
{
    #region Properties

    public override int Id => TmdbCollectionID;

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_CollectionID { get; set; }

    /// <summary>
    /// TMDB Collection ID.
    /// </summary>
    public int TmdbCollectionID { get; set; }

    /// <summary>
    /// The english title of the collection, used as a fallback for when no
    /// title is available in the preferred language.
    /// </summary>
    public string EnglishTitle { get; set; } = string.Empty;

    /// <summary>
    /// The english overview, used as a fallback for when no overview is
    /// available in the preferred language.
    /// </summary>
    public string EnglishOverview { get; set; } = string.Empty;

    /// <summary>
    /// Number of movies in the collection.
    /// </summary>
    public int MovieCount { get; set; }

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

    public TMDB_Collection() { }

    public TMDB_Collection(int collectionId)
    {
        TmdbCollectionID = collectionId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion

    #region Methods

    public bool Populate(Collection collection)
    {
        var translation = collection.Translations?.Translations.FirstOrDefault(translation => translation.Iso_639_1 == "en");
        var updates = new[]
        {
            UpdateProperty(EnglishTitle, string.IsNullOrEmpty(translation?.Data.Name) ? collection.Name : translation.Data.Name, v => EnglishTitle = v),
            UpdateProperty(EnglishOverview, string.IsNullOrEmpty(translation?.Data.Overview) ? collection.Overview : translation.Data.Overview, v => EnglishOverview = v),
            UpdateProperty(MovieCount, collection.Parts.Count, v => MovieCount = v),
        };

        return updates.Any(updated => updated);
    }

    public TMDB_Title? GetPreferredTitle(bool useFallback = false)
    {
        // TODO: Implement this logic once the repositories are added.

        // Fallback.
        return useFallback ? new(ForeignEntityType.Collection, TmdbCollectionID, EnglishTitle, "en", "US") : null;
    }

    public IReadOnlyList<TMDB_Title> GetAllTitles() =>
        RepoFactory.TMDB_Title.GetByParentTypeAndID(ForeignEntityType.Collection, TmdbCollectionID);

    public TMDB_Overview? GetPreferredOverview(bool useFallback = false)
    {
        // TODO: Implement this logic once the repositories are added.

        return useFallback ? new(ForeignEntityType.Collection, TmdbCollectionID, EnglishOverview, "en", "US") : null;
    }

    public IReadOnlyList<TMDB_Overview> GetAllOverviews() =>
        RepoFactory.TMDB_Overview.GetByParentTypeAndID(ForeignEntityType.Collection, TmdbCollectionID);

    public IReadOnlyList<TMDB_Movie> GetTmdbMovies() =>
        RepoFactory.TMDB_Movie.GetByTmdbCollectionID(TmdbCollectionID);

    #endregion

    #region IEntityMetadata

    ForeignEntityType IEntityMetadata.Type => ForeignEntityType.Collection;

    DataSourceType IEntityMetadata.DataSource => DataSourceType.TMDB;

    string? IEntityMetadata.OriginalTitle => null;

    TitleLanguage? IEntityMetadata.OriginalLanguage => null;

    string? IEntityMetadata.OriginalLanguageCode => null;

    DateOnly? IEntityMetadata.ReleasedAt => null;

    #endregion
}
