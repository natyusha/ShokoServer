using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models.Implementations;

public class TitleImpl : TextImpl, ITitle
{
    /// <inheritdoc/>
    public bool IsPreferred { get; set; }

    /// <inheritdoc/>
    public TitleType Type { get; set; }

    public TitleImpl() : base()
    {
        IsPreferred = false;
        Type = TitleType.None;
    }

    public TitleImpl(DataSource source, TextLanguage language, string value, TitleType type, bool isDefault = false) : base(source, language, value)
    {
        IsPreferred = isDefault;
        Type = type;
    }
}
