using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Models.Provider;

namespace Shoko.Plugin.Abstractions.Models.Shoko;

public interface IShokoEpisode : IImageContainer, ITitleContainer, IOverviewContainer
{
    #region Identifiers

    int Id { get; }
    
    int SeriesId { get; }

    int AniDBId { get; }

    #endregion
    
    #region Links

    IShokoSeries Series { get; }

    IEpisodeMetadata AniDB { get; }

    IReadOnlyList<IMovieMetadata> Movies { get; }

    IReadOnlyList<IEpisodeMetadata> Episodes { get; }

    IReadOnlyList<IVideoEpisodeCrossReference> CrossReferences { get; }

    IReadOnlyList<IShokoVideo> Videos { get; }

    #endregion

    #region Metadata

    bool IsHidden { get; }

    #endregion

}
