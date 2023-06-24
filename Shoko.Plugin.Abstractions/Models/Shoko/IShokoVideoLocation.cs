
using System.IO;

namespace Shoko.Plugin.Abstractions.Models.Shoko;

public interface IShokoVideoLocation : IMetadata<int>
{
    #region Identifiers

    /// <summary>
    /// The video id.
    /// </summary>
    int VideoId { get; }

    /// <summary>
    /// The import folder id.
    /// </summary>
    int ImportFolderId { get; }

    #endregion

    #region Metadata

    /// <summary>
    /// The file name.
    /// </summary>
    string FileName { get; }

    /// <summary>
    /// The relative path from the import folder to the file location, including
    /// the file name.
    /// </summary>
    /// <value></value>
    string RelativePath { get; }

    /// <summary>
    /// The Absolute path of the file, if it's still available.
    /// </summary>
    string AbsolutePath =>
        Path.Join(ImportFolder.Path, RelativePath);

    /// <summary>
    /// Indicates the server can access the file location right now, and the
    /// file location exists.
    /// </summary>
    bool IsAccessible { get; }

    #endregion

    #region Links

    /// <summary>
    /// Import folder.
    /// </summary>
    IImportFolder ImportFolder { get; }

    /// <summary>
    /// Video file.
    /// </summary>
    IShokoVideo Video { get; }

    /// <summary>
    /// Get a byte-stream for the on-disk content, if the file location still
    /// exists.
    /// </summary>
    FileStream? GetFileStream()
    {
        // It shouldn't be possible that the import folder is null here, but
        // hey, it doesn't hurt to check.
        var importFolder = ImportFolder;
        if (importFolder is null)
            return null;

        // It shouldn't be possible that the video is null here, but hey, it
        // doesn't hurt to check.
        var video = Video;
        if (video is null)
            return null;

        // Check if both the file location exists, and that the size is correct.
        var absolutePath = Path.Join(importFolder.Path, RelativePath);
        var fileInfo = new FileInfo(absolutePath);
        if (!fileInfo.Exists || fileInfo.Length != video.Size)
            return null;

        // Open the stream.
        return fileInfo.OpenRead();
    }

    #endregion
}
