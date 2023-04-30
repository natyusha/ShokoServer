
using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels;

public interface IShokoEpisode : IImageContainer, ITitleContainer, IOverviewContainer
{
    int Id { get; }
    
    int SeriesId { get; }

    int AniDBId { get; }

    IShokoSeries Series { get; }

    IEpisodeMetadata AniDB { get; }

    IReadOnlyList<IMovieMetadata> Movies { get; }

    IReadOnlyList<IEpisodeMetadata> Episodes { get; }
}
