using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;

namespace Shoko.Plugin.Abstractions.Models.Implementations;

public class TextImpl : IText
{
    /// <inheritdoc/>
    public TextLanguage Language { get; set; }

    /// <inheritdoc/>
    public string LanguageCode
    {
        get
        {
            return Language.ToLanguageCode();
        }
        set
        {
            Language = value.ToTextLanguage();
        }
    }

    /// <inheritdoc/>
    public string Value { get; }

    /// <inheritdoc/>
    public DataSource DataSource { get; }

    public TextImpl()
    {
        Value = string.Empty;
        Language = TextLanguage.Unknown;
        DataSource = DataSource.None;
    }

    public TextImpl(DataSource source, TextLanguage language, string value)
    {
        Value = value;
        Language = language;
        DataSource = source;
    }
}
