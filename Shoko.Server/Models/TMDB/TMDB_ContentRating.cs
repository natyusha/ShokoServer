
#nullable enable
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;

namespace Shoko.Server.Models.TMDB;

public class TMDB_ContentRating
{
    public string LanguageCode = string.Empty;

    public TextLanguage Language
        => LanguageCode.ToTextLanguage();

    /// <summary>
    /// content ratings (certifications) that have been added to a TV show.
    /// </summary>
    public string Rating = string.Empty;

    public override string ToString()
    {
        return $"{LanguageCode} {Rating}";
    }
}
