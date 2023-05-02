
namespace Shoko.Plugin.Abstractions.DataModels.Implementations;

public class RenameScriptImpl : IRenameScript
{
    /// <inheritdoc/>
    public string? Script { get; set; }

    /// <inheritdoc/>
    public string Type { get; set; }

    /// <inheritdoc/>
    public string? ExtraData { get; }

    public RenameScriptImpl()
    {
        Script = null;
        Type = string.Empty;
        ExtraData = null;
    }

    public RenameScriptImpl(string type, string? script = null, string? extraData = null)
    {
        Script = script;
        Type = type;
        ExtraData = extraData;
    }
}
