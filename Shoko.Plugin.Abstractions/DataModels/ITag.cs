
namespace Shoko.Plugin.Abstractions.DataModels;

public interface ITag : IMetadata, ITitleContainer, IOverviewContainer
{
    string? ParentTagId { get; }

    ITag? ParentTag { get; }

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
}
