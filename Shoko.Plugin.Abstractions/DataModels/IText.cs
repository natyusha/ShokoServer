using Shoko.Plugin.Abstractions.Enums;

#nullable enable
namespace Shoko.Plugin.Abstractions.DataModels;

public interface IText
{
    public TextLanguage Language { get; }
    public string LanguageCode { get; }
    public string Value { get; }
}
