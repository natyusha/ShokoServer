
using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels;

public interface IShokoSeries : IImageContainer, ITitleContainer, IOverviewContainer
{
    int Id { get; }

    int ParentGroupId { get; }

    int TopLevelGroupId { get; }

    int AniDBId { get; }

    IShokoGroup ParentGroup { get; }

    IShokoGroup TopLevelGroup { get; }

    IShowMetadata AniDB { get; }

    IReadOnlyList<IMovieMetadata> Movies { get; }

    IReadOnlyList<IShowMetadata> Shows { get; }

    IReadOnlyList<IShokoEpisode> Episodes { get; }
}
