using Shoko.Plugin.Abstractions.Events;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class FileNotMatchedEventSignalRModel : FileEventSignalRModel
{
    /// <summary>
    /// Number of times we've tried to auto-match this file up until now.
    /// </summary>
    public int AutoMatchAttempts { get; }

    /// <summary>
    /// True if this file had existing cross-refernces before this match
    /// attempt.
    /// </summary>
    public bool HasCrossReferences { get; }

    /// <summary>
    /// True if we're currently UDP banned.
    /// </summary>
    public bool IsUDPBanned { get; }

    public FileNotMatchedEventSignalRModel(FileNotMatchedEventArgs eventArgs) : base(eventArgs)
    {
        AutoMatchAttempts = eventArgs.AutoMatchAttempts;
        HasCrossReferences = eventArgs.HasCrossReferences;
        IsUDPBanned = eventArgs.IsUDPBanned;
    }
}
