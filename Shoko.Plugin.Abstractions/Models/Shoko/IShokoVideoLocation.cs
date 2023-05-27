
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
        System.IO.Path.Join(ImportFolder.Path, RelativePath);

    /// <summary>
    /// Indicates the server can access the file location right now, and the
    /// file location exists.
    /// </summary>
    bool IsAccessible { get; }

    #endregion
}
