using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models;

public interface IContentRating
{
    string LanguageCode { get; }

    TextLanguage Language { get; }

    string Rating { get; }

    DataSource DataSource { get; }
}
