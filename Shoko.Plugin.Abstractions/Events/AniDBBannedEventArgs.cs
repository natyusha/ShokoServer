using System;

namespace Shoko.Plugin.Abstractions.Events;

public class AniDBBannedEventArgs : EventArgs
{
    /// <summary>
    /// The type of ban.
    /// </summary>
    public AniDBBanType Type { get; set; }

    /// <summary>
    /// The time the ban occurred.
    /// </summary>
    public DateTime Time { get; set; }

    /// <summary>
    /// The time when Shoko will attempt again.
    /// </summary>
    /// <remarks>
    /// This time is just a guess, as we get no data or hint of any kind for
    /// this value to prevent additional bans.
    /// </remarks>
    public DateTime ResumeTime { get; set; }

    public AniDBBannedEventArgs(AniDBBanType type, DateTime time, DateTime resumeTime)
    {
        Type = type;
        Time = time;
        ResumeTime = resumeTime;
    }
}

public enum AniDBBanType
{
    UDP,
    HTTP,
}
