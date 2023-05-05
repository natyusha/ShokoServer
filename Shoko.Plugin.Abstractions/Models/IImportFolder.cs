using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models;

public interface IImportFolder
{
    /// <summary>
    /// Import folder ID.
    /// </summary>
    int Id { get; }

    /// <summary>
    /// The Import Folder's name. This is user specified in WebUI,
    /// or "NA" for legacy.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The Base Location of the Import Folder in the host, VM, or container filesystem
    /// </summary>
    string Path { get; }

    /// <summary>
    /// Indicates the import folder is actively being monitored for file events.
    /// </summary>
    public bool IsWatched { get; set; }

    /// <summary>
    /// The rules that this Import Folder should adhere to. A folder that is both a Source and Destination cares not how files are moved in or out of it.
    /// </summary>
    ImportFolderType Type { get; }
}
