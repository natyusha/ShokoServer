using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models;

public interface ITitle : IText
{
    /// <summary>
    /// Indicates the title is preferred for the metadata provider.
    /// </summary>
    bool IsPreferred { get; }

    /// <summary>
    /// The title type.
    /// </summary>
    TitleType Type { get; }
}
