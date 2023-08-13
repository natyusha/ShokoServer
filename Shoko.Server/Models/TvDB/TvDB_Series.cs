using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.Implementations;
using Shoko.Plugin.Abstractions.Models.Provider;
using Shoko.Plugin.Abstractions.Models.Shoko;
using Shoko.Server.Models.CrossReferences;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.TvDB;

public class TvDB_Show : IShowMetadata
{
    #region Database Columns

    public int TvDB_SeriesID { get; set; }

    public int Id { get; set; }

    public string MainOverview { get; set; }

    public string MainTitle { get; set; }

    public string Status { get; set; }

    public string Banner { get; set; }

    public string Fanart { get; set; }

    public string Poster { get; set; }

    public DateTime LastUpdatedAt { get; set; }

    public int? Rating { get; set; } // saved at * 10 to preserve decimal. resulting in 82/100

    #endregion

    #region Helpers

    public IReadOnlyList<CR_AniDB_TvDB> GetCrossReferences() =>
        RepoFactory.CR_AniDB_TvDB.GetByTvDBId(Id);

    public IReadOnlyList<ShokoSeries> GetShokoSeries() =>
        GetCrossReferences()
            .Select(xref => RepoFactory.Shoko_Series.GetByAnidbAnimeId(xref.AnidbAnimeId))
            .Where(series => series != null)
            .ToList();

    public IReadOnlyList<TvDB_Episode> GetEpisodes() =>
        RepoFactory.TvDB_Episode.GetBySeriesID(Id);

    #endregion

    #region IShowMetadata

    #region IMetadata

    string IMetadata<string>.Id
        => Id.ToString();

    /// <summary>
    /// The metadata source.
    /// </summary>
    DataSource IMetadata.DataSource
        => DataSource.TvDB;

    #endregion
    #region Identifiers

    /// <summary>
    /// A list of Shoko Series IDs associated with the show.
    /// </summary>
    IReadOnlyList<int> IShowMetadata.ShokoSeriesIds
        => GetShokoSeries()
            .Select(series => series.Id)
            .ToList();

    #endregion

    #region Links

    /// <summary>
    /// A list of Shoko series linked to the show.
    /// </summary>
    IReadOnlyList<IShokoSeries> IShowMetadata.ShokoSeries
        => GetShokoSeries();

    /// <summary>
    /// A list of season metadata associated with the show.
    /// </summary>
    IReadOnlyList<ISeasonMetadata> IShowMetadata.Seasons
        => new List<ISeasonMetadata>();

    /// <summary>
    /// A list of episode metadata associated with the show.
    /// </summary>
    IReadOnlyList<IEpisodeMetadata> IShowMetadata.Episodes
        => GetEpisodes();

    #endregion

    #region Metadata

    /// <summary>
    /// The anime type of the show, e.g. "TVSeries".
    /// </summary>
    AnimeType IShowMetadata.AnimeType =>
        AnimeType.None;

    /// <summary>
    /// The first air date for the show, if known.
    /// </summary>
    DateTime? IShowMetadata.AirDate =>
        null;

    /// <summary>
    /// The last air date for the show, unless the show is still airing.
    /// </summary>
    DateTime? IShowMetadata.EndDate =>
        null;

    /// <summary>
    /// The user rating for the show.
    /// </summary>
    IRating? IShowMetadata.Rating =>
        Rating.HasValue ? new RatingImpl(DataSource.TvDB, (decimal)(Rating.Value * 10)) : null;

    /// <summary>
    /// The preferred content rating according to the language preference
    /// settings.
    /// </summary>
    IContentRating? IShowMetadata.PreferredContentRating =>
        null;

    /// <summary>
    /// All content ratings available for the show.
    /// </summary>
    IReadOnlyDictionary<TextLanguage, IContentRating> IShowMetadata.ContentRatings =>
        new Dictionary<TextLanguage, IContentRating>();

    /// <summary>
    /// A list of genres associated with the show.
    /// </summary>
    IReadOnlyList<IGenre> IShowMetadata.Genres =>
        new List<IGenre>();

    /// <summary>
    /// A list of tags associated with the show.
    /// </summary>
    IReadOnlyList<ITag> IShowMetadata.Tags =>
        new List<ITag>();

    /// <summary>
    /// A list of relations to other shows or movies related to the show.
    /// </summary>
    IReadOnlyList<IRelationMetadata> IShowMetadata.Relations => new List<IRelationMetadata>();

    /// <summary>
    /// Indicates whether the show is considered "pornographic."
    /// </summary>
    bool IShowMetadata.IsPorn => false;

    /// <summary>
    /// The total episode counts for all episodes (potentially across all
    /// seasons) in the show.
    /// </summary>
    IEpisodeCounts IShowMetadata.EpisodeCounts => new EpisodeCountsImpl();

    /// <summary>
    /// The material source of the show, if known, or "Original Work" if there
    /// is no source, since it's an original.
    /// </summary>
    string? IShowMetadata.Source => null;

    #endregion

    #endregion
}

