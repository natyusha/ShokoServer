using Shoko.Plugin.Abstractions.Models.Provider;

namespace Shoko.Plugin.Abstractions.Models.Shoko;

public interface IShokoVideoCrossReference : IMetadata<int>
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
    /// The anidb episode id.
    /// </summary>
    int AnidbEpisodeId { get; }

    /// <summary>
    /// The shoko series id.
    /// </summary>
    int SeriesId { get; }

    /// <summary>
    /// The anidb anime id.
    /// </summary>
    int AnidbAnimeId { get; }

    /// <summary>
    /// The anidb release group id, if assosiated with this cross-reference.
    /// </summary>
    int? AnidbReleaseGroupId { get; }

    /// <summary>
    /// The custom release group id, if assosiated with this cross-reference.
    /// </summary>
    int? CustomReleaseGroupId { get; }

    #endregion

    #region Metadata

    int Order { get; }

    decimal Percentage { get; }

    #endregion

    #region Links

    IShokoVideo Video { get; }

    IShokoEpisode Episode { get; }

    IEpisodeMetadata AnidbEpisode { get; }

    IShokoSeries Series { get; }

    IShowMetadata AnidbAnime { get; }

    IReleaseGroup? ReleaseGroup { get; }

    #endregion
}
