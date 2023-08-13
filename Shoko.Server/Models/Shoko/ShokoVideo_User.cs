using System;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Internal;

public class Shoko_Video_User
{
    #region Database Columns

    /// <summary>
    /// User record id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Linked user id.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Linked video id.
    /// </summary>
    /// <value></value>
    public int VideoId { get; set; }

    /// <summary>
    /// How many times the user have watched the video. This count cannot be
    /// reset by any users.
    /// </summary>
    public int WatchedCount { get; set; }

    /// <summary>
    /// The raw resume position in milliseconds.
    /// </summary>
    /// <value></value>
    public long RawResumePosition { get; set; }

    /// <summary>
    /// When the video was last watched-till-completion (or otherwise marked as
    /// watched). This timestamp may be reset over and over again.
    /// </summary>
    public DateTime? LastWatchedAt { get; set; }

    /// <summary>
    /// When this user record was last updated.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region Constructors

    public Shoko_Video_User() { }

    public Shoko_Video_User(int userID, int fileID)
    {
        UserId = userID;
        VideoId = fileID;
        WatchedCount = 0;
        RawResumePosition = 0;
        LastWatchedAt = null;
        LastUpdatedAt = DateTime.Now;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Where to resume the playback of the <see cref="Shoko_Video"/>
    ///  as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan? ResumePosition
    {
        get => RawResumePosition > 0 ? TimeSpan.FromMilliseconds(RawResumePosition) : null;
        set => RawResumePosition = value.HasValue ? (long)Math.Round(value.Value.TotalMilliseconds) : 0;
    }

    /// <summary>
    /// Get the related <see cref="Shoko_Video"/>.
    /// </summary>
    public Shoko_Video Video =>
        RepoFactory.Shoko_Video.GetByID(VideoId)!;

    /// <summary>
    /// Get the related <see cref="Shoko_User"/>.
    /// </summary>
    public Shoko_User User =>
        RepoFactory.Shoko_User.GetByID(UserId)!;

# pragma warning disable 0618
    public override string ToString()
    {
        var video = Video;
        return $"{video.FileName} --- {video.ED2K} --- User {UserId}";
    }
# pragma warning restore 0618

    #endregion
}
