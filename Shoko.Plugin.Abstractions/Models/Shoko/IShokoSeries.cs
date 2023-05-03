using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Models.Provider;

namespace Shoko.Plugin.Abstractions.Models.Shoko;

public interface IShokoSeries : IImageContainer, ITitleContainer, IOverviewContainer
{
    #region Identifiers

    int Id { get; }

    int ParentGroupId { get; }

    int TopLevelGroupId { get; }

    int AniDBId { get; }

    #endregion
    
    #region Links

    IShokoGroup ParentGroup { get; }

    IShokoGroup TopLevelGroup { get; }

    IShowMetadata AniDB { get; }

    IReadOnlyList<IMovieMetadata> Movies { get; }

    IReadOnlyList<IShowMetadata> Shows { get; }

    IReadOnlyList<IVideoEpisodeCrossReference> CrossReferences { get; }

    IReadOnlyList<IShokoEpisode> Episodes { get; }

    IReadOnlyList<IShokoVideo> Videos { get; }

    #endregion

    #region Metadata

    DateTime CreatedAt { get; }

    DateTime LastUpdatedAt { get; }

    #endregion
}
