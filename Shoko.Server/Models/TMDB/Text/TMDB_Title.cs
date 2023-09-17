using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_Title
{
    public int TMDB_TitleID { get; set; }

    public int ParentID { get; set; }

    public ForeignEntityType ParentType { get; set; }

    public TitleLanguage Language
    {
        get => string.IsNullOrEmpty(LanguageCode) ? TitleLanguage.None : LanguageCode.GetTitleLanguage();
    }

    /// <summary>
    /// ISO 639-1 alpha-2 language code.
    /// </summary>
    public string? LanguageCode { get; set; }

    /// <summary>
    /// ISO 3166-1 alpha-2 country code.
    /// </summary>
    public string? CountryCode { get; set; }

    public string Value { get; set; } = string.Empty;

    public TMDB_Title() { }

    public TMDB_Title(ForeignEntityType parentType, int parentId, string value, string? languageCode, string? countryCode)
    {
        ParentType = parentType;
        ParentID = parentId;
        Value = value;
        LanguageCode = languageCode;
        CountryCode = countryCode;
    }
}
