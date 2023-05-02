using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models;

public interface IText
{
    TextLanguage Language { get; }
    string LanguageCode { get; }
    string Value { get; }
    DataSource DataSource { get; }
}
