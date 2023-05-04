using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models;

public interface ITitle : IText
{
    bool IsPreferred { get; }
    TitleType Type { get; }
}
