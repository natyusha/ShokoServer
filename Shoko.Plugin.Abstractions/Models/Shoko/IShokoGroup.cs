using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.Models.Shoko;

public interface IShokoGroup : IBaseMetadata<int>
{
    #region Identifiers

    int? ParentGroudId { get; }

    int TopLevelGroupId { get; }

    #endregion

    #region Links

    /// <summary>
    /// The direct parent of the group if the group is a sub-group.
    /// </summary>
    IShokoGroup ParentGroup { get; }

    /// <summary>
    /// The top-level group this group belongs to. It can refer to itself if it
    /// is a top-level group.
    /// </summary>
    IShokoGroup TopLevelGroup { get; }

    /// <summary>
    /// The main series within the group. It can be auto-selected (when
    /// auto-grouping is enabled) or user overwritten, and will fallback to the
    /// earliest airing series within the group or any sub-groups if nothing is
    /// selected.
    /// </summary>
    IShokoSeries MainSeries { get; }

    /// <summary>
    /// The series directly within the group, ordered by air-date.
    /// </summary>
    IReadOnlyList<IShokoSeries> Series { get; }

    /// <summary>
    /// All series directly within the group and within all sub-groups (if any),
    /// ordered by air-date.
    /// </summary>
    IReadOnlyList<IShokoSeries> AllSeries { get; }

    #endregion
}
