using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models;

public interface ITitle : IText
{
    bool IsDefault { get; }
    TitleType Type { get; }
}
