using Shoko.Plugin.Abstractions.Enums;

#nullable enable
namespace Shoko.Plugin.Abstractions.DataModels;

public interface IContentRating
{
    string LanguageCode { get; }

    TextLanguage Language { get; }

    string Rating { get; }
    
    DataSource Source { get; }
}
