
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
    string AbsolutePath { get; }

    /// <summary>
    /// Indicates the server can access the file location right now, and the
    /// file location exists.
    /// </summary>
    bool IsAccessible
    {
        get
        {
            return GetFileInfo() != null;
        }
    }

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

    #endregion

    #region Methods

    /// <summary>
    /// Get the file-info object for the on-disk location if it exists and the
    /// file size matches what we know.
    /// </summary>
    /// <returns>The file info object if successfull, otherwise null.</returns>
    FileInfo? GetFileInfo();

    /// <summary>
    /// Get a byte-stream for the on-disk content, if the file location still
    /// exists and the file size matches what we know.
    /// </summary>
    /// <returns>The file </returns>
    FileStream? GetFileStream();

    #endregion
}
