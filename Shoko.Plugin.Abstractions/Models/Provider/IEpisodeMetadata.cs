using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models.Shoko;

namespace Shoko.Plugin.Abstractions.Models.Provider;

public interface IEpisodeMetadata : IMetadata<string>, IImageContainer, ITitleContainer, IOverviewContainer
{
    #region Idenitifers

    public string? SeasonId { get; }

    public string ShowId { get; }

    public IReadOnlyList<int> ShokoEpisodeIds { get; }

    #endregion

    #region Links

    public ISeasonMetadata? Season { get; }


    public IShowMetadata Show { get; }

    IReadOnlyList<IShokoEpisode> ShokoEpisodes { get; }

    #endregion

    #region Metadata

    /// <summary>
    /// The episode number.
    /// </summary>
    int Number { get; }

    /// <summary>
    /// The absolute episode number, if applicable.
    /// </summary>
    int? AbsoluteNumber { get; }

    /// <summary>
    /// The season number, if applicable.
    /// </summary>
    int? SeasonNumber { get; }

    int? AirsAfterSeason { get; }

    int? AirsBeforeSeason { get; }

    int? AirsBeforeEpisode { get; }

    /// <summary>
    /// The episode type.
    /// </summary>
    EpisodeType Type { get; }

    /// <summary>
    /// The episode duration.
    /// </summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// The user rating for the episode.
    /// </summary>
    IRating? Rating { get; }

    /// <summary>
    /// The first air date of the episode, if known, otherwise null.
    /// </summary>
    DateTime? AirDate { get; }

    /// <summary>
    /// When the metadata was last updated.
    /// </summary>
    DateTime LastUpdated { get; }

    #endregion
}
