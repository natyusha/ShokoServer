using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models.Shoko;

namespace Shoko.Plugin.Abstractions.Models.Provider;

public interface IMovieMetadata : IBaseMetadata
{
    IReadOnlyList<int> ShokoSeriesIds { get; }

    IReadOnlyList<int> IShokoEpisodeIds { get; }

    IReadOnlyList<IShokoSeries> ShokoSeries { get; }

    IReadOnlyList<IShokoEpisode> ShokoEpisodes { get; }

    /// <summary>
    /// The first air date for the show, if known.
    /// </summary>
    DateTime? AirDate { get; }

    /// <summary>
    /// The user rating for the show.
    /// </summary>
    IRating? Rating { get; }

    /// <summary>
    /// The preferred content-rating according to the language preference
    /// settings.
    /// </summary>
    IContentRating? PreferredContentRating { get; }

    /// <summary>
    /// All content ratings available for the show.
    /// </summary>
    IReadOnlyDictionary<TextLanguage, IContentRating> ContentRatings { get; }

    IReadOnlyList<IGenre> Genres { get; }

    IReadOnlyList<ITag> Tags { get; }

    IReadOnlyList<IMovieRoleMetadata> Roles { get; }

    IReadOnlyList<IRelationMetadata> Relations { get; }
}
