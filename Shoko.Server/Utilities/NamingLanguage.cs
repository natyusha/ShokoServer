using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Server.Utilities;

public class NamingLanguage
{
    public TextLanguage Language { get; set; }

    public string LanguageCode => Language.ToLanguageCode();

    public string LanguageDescription => Language.GetDescription();

    public NamingLanguage()
    {
    }

    public NamingLanguage(TextLanguage language)
    {
        Language = language;
    }

    public NamingLanguage(string language)
    {
        Language = language.ToTextLanguage();
    }

    public override string ToString()
    {
        return string.Format("{0} - ({1})", Language, LanguageDescription);
    }
}
