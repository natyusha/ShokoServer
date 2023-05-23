using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models;

public interface ITitle : IText
{
    /// <summary>
    /// Indicates the title is the preferred title for the metadata provider.
    /// </summary>
    bool IsPreferred { get; }

    /// <summary>
    /// Indicates the title is the default title for the metadata provider.
    /// </summary>
    bool IsDefault { get; }

    /// <summary>
    /// The title type.
    /// </summary>
    TitleType Type { get; }
}
