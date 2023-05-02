using System.Collections.Generic;

#nullable enable
namespace Shoko.Plugin.Abstractions.DataModels;

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
