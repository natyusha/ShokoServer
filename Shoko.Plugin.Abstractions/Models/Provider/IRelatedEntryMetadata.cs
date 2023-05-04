using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models.Shoko;

namespace Shoko.Plugin.Abstractions.Models.Provider;

public interface IRelationMetadata : IMetadata<string>
{
    #region Identifiers

    string BaseId { get; }

    string RelatedId { get; }

    IReadOnlyList<int> BaseShokoSeriesIds { get; }
    
    IReadOnlyList<int> BaseShokoEpisodeIds { get; }

    IReadOnlyList<int> RelatedShokoSeriesIds { get; }

    IReadOnlyList<int> RelatedShokoEpisodeIds { get; }

    #endregion

    #region Links

    IBaseMetadata Base { get; }

    IReadOnlyList<IShokoSeries> BaseShokoSeries { get; }

    IReadOnlyList<IShokoEpisode> BaseShokoEpisodes { get; }

    IBaseMetadata? Related { get; }

    IReadOnlyList<IShokoSeries> RelatedShokoSeries { get; }

    IReadOnlyList<IShokoEpisode> RelatedShokoEpisodes { get; }

    #endregion

    #region Metadata

    RelationType RelationType { get; }

    #endregion
}
