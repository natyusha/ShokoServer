using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models.Provider;
using Shoko.Plugin.Abstractions.Models.Search;

namespace Shoko.Plugin.Abstractions.Models.Shoko;

public interface IShokoEpisode : IBaseMetadata<int>
{
    #region Identifiers
    
    /// <summary>
    /// Shoko series id.
    /// </summary>
    int SeriesId { get; }

    /// <summary>
    /// Anidb episode id.
    /// </summary>
    int AnidbEpisodeId { get; }

    /// <summary>
    /// Short-cut to only get simple metadata objects with just the ids for each
    /// linked movie across providers.
    /// </summary>
    IReadOnlyList<IMetadata<string>> AllMovieIds { get; }

    /// <summary>
    /// Short-cut to only get simple metadata objects with just the ids for each
    /// linked episode across providers.
    /// </summary>
    IReadOnlyList<IMetadata<string>> AllEpisodeIds { get; }

    #endregion

    #region Metadata

    bool IsHidden { get; }

    #endregion
    
    #region Links

    IShokoSeries Series { get; }

    /// <summary>
    /// Anidb episode.
    /// </summary>
    IEpisodeMetadata AniDBEpisode { get; }

    IReadOnlyList<IMovieMetadata> AllMovies { get; }

    IReadOnlyList<IEpisodeMetadata> GetMovies(BaseSearchOptions? options = null);

    IReadOnlyList<IEpisodeMetadata> AllEpisodes { get; }

    IReadOnlyList<IEpisodeMetadata> GetEpisodes(BaseSearchOptions? options = null);

    IReadOnlyList<IShokoVideoCrossReference> AllCrossReferences { get; }

    IReadOnlyList<IShokoVideoCrossReference> GetCrossReferences(ShokoVideoCrossReferenceSearchOptions? options = null);

    IReadOnlyList<IShokoVideo> AllVideos { get; }

    IReadOnlyList<IShokoVideo> GetVideos(BaseSearchOptions? options = null);

    #endregion
}
