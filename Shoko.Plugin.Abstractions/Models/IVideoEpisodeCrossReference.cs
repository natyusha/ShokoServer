using Shoko.Plugin.Abstractions.Models.AniDB;
using Shoko.Plugin.Abstractions.Models.Shoko;

namespace Shoko.Plugin.Abstractions.Models;

public interface IVideoEpisodeCrossReference : IMetadata
{
    #region Identifiers

    /// <summary>
    /// The shoko video id.
    /// </summary>
    int VideoId { get; }

    /// <summary>
    /// The shoko episode id.
    /// </summary>
    int EpisodeId { get; }

    /// <summary>
    /// The shoko series id.
    /// </summary>
    int SeriesId { get; }

    /// <summary>
    /// The anidb release group id, if assosiated with this cross-reference.
    /// </summary>
    int? AnidbReleaseGroupId { get; }

    /// <summary>
    /// The custom release group id, if assosiated with this cross-reference.
    /// </summary>
    int? CustomReleaseGroupId { get; }

    #endregion

    #region Links

    IShokoVideo Video { get; }

    IShokoEpisode Episode { get; }

    IShokoSeries Series { get; }

    IReleaseGroup? ReleaseGroup { get; }

    #endregion

    #region Metadata

    int Order { get; }

    decimal Percentage { get; }

    #endregion
}
