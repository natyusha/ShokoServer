using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models.Shoko;

namespace Shoko.Plugin.Abstractions.Models.Provider;

public interface IMovieMetadata : IBaseMetadata
{
    #region Identifiers

    int? ShokoSeriesId { get; }

    int? ShokoEpisodeId { get; }

    #endregion

    #region Links

    /// <summary>
    /// The shoko series linked to the movie.
    /// </summary>
    /// <value></value>
    IShokoSeries? ShokoSeries { get; }

    /// <summary>
    /// The Shoko episode linked to the movie.
    /// </summary>
    IShokoEpisode? ShokoEpisode { get; }

    #endregion

    /// <summary>
    /// The movie duration.
    /// </summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// The air date for the movie, if known.
    /// </summary>
    DateTime? AirDate { get; }

    /// <summary>
    /// The user rating for the movie, if known.
    /// </summary>
    IRating? Rating { get; }

    /// <summary>
    /// The preferred content-rating according to the language preference
    /// settings.
    /// </summary>
    IContentRating? PreferredContentRating { get; }

    /// <summary>
    /// All content ratings available for the movie.
    /// </summary>
    IReadOnlyDictionary<TextLanguage, IContentRating> ContentRatings { get; }

    IReadOnlyList<IGenre> Genres { get; }

    IReadOnlyList<ITag> Tags { get; }

    IReadOnlyList<IRoleMetadata> Roles { get; }

    IReadOnlyList<IRelationMetadata> Relations { get; }

    /// <summary>
    /// When the metadata was last updated.
    /// </summary>
    DateTime LastUpdated { get; }
}
