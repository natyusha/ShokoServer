using Shoko.Plugin.Abstractions.Enums;

#nullable enable
namespace Shoko.Plugin.Abstractions.DataModels;

public interface ITitle : IText
{
    public TitleType Type { get; }
}
