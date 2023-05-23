using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models;

public interface IText : IMetadata
{
    /// <summary>
    /// The identifier of the parent metadata object, if any.
    /// </summary>
    string? ParentId { get; }

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
