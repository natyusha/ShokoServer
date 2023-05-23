using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.Models.Shoko;

public interface IShokoGroup : IMetadata<int>, IImageContainer, ITitleContainer, IOverviewContainer
{
    #region Identifiers

    int? ParentGroudId { get; }

    int TopLevelGroupId { get; }

    #endregion

    #region Links

    IShokoGroup ParentGroup { get; }

    IShokoGroup TopLevelGroup { get; }

    /// <summary>
    /// The series that is used for the name. Just use Series.FirstOrDefault() at that point.
    /// </summary>
    IShokoSeries MainSeries { get; }

    /// <summary>
    /// The series in a group, ordered by AirDate
    /// </summary>
    IReadOnlyList<IShokoSeries> Series { get; }

    #endregion

}
