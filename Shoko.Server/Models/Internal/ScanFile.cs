using System;
using Shoko.Models.Enums;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Internal;

public class ScanFile
{
    #region Database Columns
        
    public int Id { get; set; }

    public int ScanId { get; set; }

    public int ImportFolderId { get; set; }

    public int VideoLocationId { get; set; }

    public ScanFileStatus Status { get; set; } = ScanFileStatus.Waiting;

    public string AbsolutePath { get; set; } = string.Empty;

    public string ED2K { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string? CheckedED2K { get; set; }

    public DateTime? CheckedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    #endregion

    #region Constructors

    public ScanFile() { }

    public ScanFile(Scan scan, Shoko_Video video, Shoko_Video_Location videoLocation)
    {
        ScanId = scan.Id;
        ImportFolderId = videoLocation.ImportFolderId;
        VideoLocationId = videoLocation.Id;
        Status = ScanFileStatus.Waiting;
        AbsolutePath = videoLocation.AbsolutePath;
        ED2K = video.ED2K;
        FileSize = video.Size;
        CreatedAt = DateTime.Now;
    }

    #endregion

    #region Helpers

    public bool IsChecked =>
        !string.IsNullOrEmpty(CheckedED2K);
    
    public bool IsFaulty =>
        IsChecked && !string.Equals(ED2K, CheckedED2K, StringComparison.InvariantCultureIgnoreCase);

    public ImportFolder GetImportFolder()
    {
        var folder = RepoFactory.ImportFolder.GetByID(ImportFolderId);
        if (folder == null)
            throw new NullReferenceException($"ImportFolder with Id {ImportFolderId} not found.");

        return folder;
    }

    public Shoko_Video GetVideo()
    {
        
        var video = RepoFactory.Shoko_Video.GetByED2K(ED2K);
        if (video == null)
            throw new NullReferenceException($"ShokoVideo with ED2K {ED2K} not found.");

        return video;
    }

    public Shoko_Video_Location GetVideoLocation()
    {
        var videoLocation = RepoFactory.Shoko_Video_Location.GetByID(VideoLocationId);
        if (videoLocation == null)
            throw new NullReferenceException($"ShokoVideoLocation with Id {VideoLocationId} not found.");

        return videoLocation;
    }

    #endregion
}
