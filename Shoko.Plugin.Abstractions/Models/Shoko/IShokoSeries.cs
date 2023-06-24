using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models.Provider;
using Shoko.Plugin.Abstractions.Models.Search;

namespace Shoko.Plugin.Abstractions.Models.Shoko;

public interface IShokoSeries : IBaseMetadata<int>
{
    #region Identifiers

    int ParentGroupId { get; }

    int TopLevelGroupId { get; }

    int AnidbAnimeId { get; }

    IReadOnlyList<IMetadata<string>> AllMovieIds { get; }

    IReadOnlyList<IMetadata<string>> AllShowIds { get; }

    #endregion

    #region Links

    IShokoGroup ParentGroup { get; }

    IShokoGroup TopLevelGroup { get; }

    IShowMetadata AnidbAnime { get; }

    IReadOnlyList<IMovieMetadata> AllMovies { get; }

    IReadOnlyList<IMovieMetadata> GetMovies(BaseSearchOptions? options = null);

    IReadOnlyList<IShowMetadata> AllShows { get; }

    IReadOnlyList<IShowMetadata> GetShows(BaseSearchOptions? options = null);

    IReadOnlyList<IShokoVideoCrossReference> AllCrossReferences { get; }

    IReadOnlyList<IShokoVideoCrossReference> GetCrossReferences(ShokoVideoCrossReferenceSearchOptions? options = null);

    IReadOnlyList<IShokoEpisode> AllEpisodes { get; }

    IReadOnlyList<IShokoEpisode> GetEpisodes(BaseSearchOptions? options = null);

    IReadOnlyList<IShokoVideo> AllVideos { get; }

    IReadOnlyList<IShokoVideo> GetVideos(BaseSearchOptions? options = null);

    #endregion
}
