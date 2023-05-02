using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions.Events;

public class FileNotMatchedEventArgs : FileEventArgs
{
    /// <summary>
    /// Number of times we've tried to auto-match this video/file up until now.
    /// </summary>
    public int AutoMatchAttempts { get; set; }

    /// <summary>
    /// Indicates the video/file had existing cross-refernces before this match
    /// attempt.
    /// </summary>
    public bool HasCrossReferences { get; set; }

    /// <summary>
    /// Indicates we're currently UDP banned.
    /// </summary>
    public bool IsUDPBanned { get; set; }

    public FileNotMatchedEventArgs(IShokoVideoFileLocation fileLocation, int attempts, bool hasXRefs, bool isBanned) : base(fileLocation)
    {
        AutoMatchAttempts = attempts;
        HasCrossReferences = hasXRefs;
        IsUDPBanned = isBanned;
    }
}
