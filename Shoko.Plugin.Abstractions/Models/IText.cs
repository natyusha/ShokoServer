using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models;

public interface IText : IMetadata
{
    /// <summary>
    /// The language.
    /// </summary>
    TextLanguage Language { get; }

    /// <summary>
    /// The language code.
    /// </summary>
    string LanguageCode { get; }

    /// <summary>
    /// The text value.
    /// </summary>
    string Value { get; }
}
