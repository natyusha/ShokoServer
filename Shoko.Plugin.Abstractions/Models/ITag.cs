
using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.Models;

public interface ITag : IMetadata<string>, ITitleContainer, IOverviewContainer
{
    #region Identifiers

    string? ParentTagId { get; }

    string TopLevelTagId { get; }

    #endregion

    #region Links

    ITag? ParentTag { get; }

    ITag TopLevelTag { get; }

    IReadOnlyList<ITag> ChildTags { get; }

    #endregion

    #region Metadata

    /// <summary>
    /// Is a spoiler in general.
    /// </summary>
    /// <value></value>
    bool IsSpoiler { get; }

    /// <summary>
    /// Is spoiler when applied to the show or movie.
    /// </summary>
    bool IsLocalSpoiler { get; }

    /// <summary>
    /// AniDB spesific tag weight for the tag on the anime.
    /// </summary>
    int? Weight { get; }

    #endregion

}
