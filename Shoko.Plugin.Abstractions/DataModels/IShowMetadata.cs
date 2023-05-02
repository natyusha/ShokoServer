using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.DataModels;

public interface IShowMetadata : IMetadata, IImageContainer, ITitleContainer, IOverviewContainer
{
    #region Identifiers

    /// <summary>
    /// A list of Shoko Series IDs associated with the show.
    /// </summary>
    IReadOnlyList<int> ShokoSeriesIds { get; }

    #endregion

    #region Links

    /// <summary>
    /// A list of Shoko series linked to the show.
    /// </summary>
    IReadOnlyList<IShokoSeries> ShokoSeries { get; }

    /// <summary>
    /// A list of season metadata associated with the show.
    /// </summary>
    IReadOnlyList<ISeasonMetadata> Seasons { get; }

    /// <summary>
    /// A list of episode metadata associated with the show.
    /// </summary>
    IReadOnlyList<IEpisodeMetadata> Episodes { get; }

    #endregion

    #region Metadata

    /// <summary>
    /// The anime type of the show, e.g. "TVSeries".
    /// </summary>
    AnimeType AnimeType { get; }

    /// <summary>
    /// The first air date for the show, if known.
    /// </summary>
    DateTime? AirDate { get; }

    /// <summary>
    /// The last air date for the show, unless the show is still airing.
    /// </summary>
    DateTime? EndDate { get; }

    /// <summary>
    /// The user rating for the show.
    /// </summary>
    IRating? Rating { get; }

    /// <summary>
    /// The preferred content rating according to the language preference
    /// settings.
    /// </summary>
    IContentRating? PreferredContentRating { get; }

    /// <summary>
    /// All content ratings available for the show.
    /// </summary>
    IReadOnlyDictionary<TextLanguage, IContentRating> ContentRatings { get; }

    /// <summary>
    /// A list of genres associated with the show.
    /// </summary>
    IReadOnlyList<IGenre> Genres { get; }

    /// <summary>
    /// A list of tags associated with the show.
    /// </summary>
    IReadOnlyList<ITag> Tags { get; }

    /// <summary>
    /// A list of relations to other shows or movies related to the show.
    /// </summary>
    IReadOnlyList<IRelatedEntryMetadata> Relations { get; }

    /// <summary>
    /// Indicates whether the show is considered "pornographic."
    /// </summary>
    bool IsPorn { get; }

    /// <summary>
    /// The total episode counts for all episodes (potentially across all
    /// seasons) in the show.
    /// </summary>
    EpisodeCounts EpisodeCounts { get; }

    /// <summary>
    /// The material source of the show, if known, or "Original Work" if there
    /// is no source, since it's an original.
    /// </summary>
    string? Source { get; }

    #endregion
}
