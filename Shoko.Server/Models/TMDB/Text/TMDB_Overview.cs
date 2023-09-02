
#nullable enable
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Server;

namespace Shoko.Server.Models.TMDB;

public class TMDB_Overview
{
    public int TMDB_OverviewID { get; set; }

    public int ParentID { get; set; }

    public ForeignEntityType ParentType { get; set; }

    public TitleLanguage Language { get; set; }

    public string? LanguageCode
    {
        get => Language == TitleLanguage.None ? null : Language.GetString();
        set => Language = string.IsNullOrEmpty(value) ? TitleLanguage.None : value.GetTitleLanguage();
    }

    public string Value { get; set; } = string.Empty;

    public TMDB_Overview(ForeignEntityType parentType, int parentId, string value, TitleLanguage language)
    {
        ParentType = parentType;
        ParentID = parentId;
        Value = value;
        Language = language;
    }

    public TMDB_Overview(ForeignEntityType parentType, int parentId, string value, string? language)
    {
        ParentType = parentType;
        ParentID = parentId;
        Value = value;
        LanguageCode = language;
    }
}
