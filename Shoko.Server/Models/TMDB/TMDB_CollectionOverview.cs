using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;

#nullable enable
namespace Shoko.Models.Server.TMDB;

public class TMDB_Collection_Overview
{
    public int Id { get; set; }

    public string CollectionId { get; set; } = string.Empty;

    public string LanguageCode { get; set; } = "und";

    public TextLanguage Language
        => LanguageCode.ToTextLanguage();

    public string Value { get; set; } = string.Empty;
}
