using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models;

public interface IText : IMetadata
{
    TextLanguage Language { get; }
    string LanguageCode { get; }
    string Value { get; }
}
