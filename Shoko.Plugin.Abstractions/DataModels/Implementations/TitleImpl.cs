using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.DataModels.Implementations;

public class TitleImpl : TextImpl, ITitle
{
    /// <inheritdoc/>
    public bool IsDefault { get; set; }

    /// <inheritdoc/>
    public TitleType Type { get; set; }

    public TitleImpl() : base()
    {
        IsDefault = false;
        Type = TitleType.None;
    }

    public TitleImpl(DataSource source, TextLanguage language, string value, TitleType type, bool isDefault = false) : base(source, language, value)
    {
        IsDefault = isDefault;
        Type = type;
    }
}
