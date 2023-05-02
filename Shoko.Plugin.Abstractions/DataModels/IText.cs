using Shoko.Plugin.Abstractions.Enums;

#nullable enable
namespace Shoko.Plugin.Abstractions.DataModels;

public interface IText
{
    TextLanguage Language { get; }
    string LanguageCode { get; }
    string Value { get; }
    DataSource DataSource { get; }
}
