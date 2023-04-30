
#nullable enable
namespace Shoko.Plugin.Abstractions.DataModels;

public interface IShokoVideoFileLocation
{
    #region Identifiers

    /// <summary>
    /// The video file location id.
    /// </summary>
    int Id { get; }

    /// <summary>
    /// The video id.
    /// </summary>
    int VideoId { get; }

    /// <summary>
    /// The import folder id.
    /// </summary>
    int ImportFolderId { get; }

    #endregion

    #region Links

    IImportFolder ImportFolder { get; }

    IShokoVideo Video { get; }

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
    string? Path
        => ImportFolder != null ?  System.IO.Path.Join(ImportFolder.Path, RelativePath) : null;

    /// <summary>
    /// The file size counted in bytes.
    /// </summary>
    long FileSize { get; }

    /// <summary>
    /// Indicates the server can access the file location right now, and the
    /// file location exists.
    /// </summary>
    bool IsAccessible { get; }

    #endregion
}
