using Shoko.Plugin.Abstractions.Events;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class FileEventSignalRModel
{
    /// <summary>
    /// The relative path of the file from the import folder base location
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// Shoko file id.
    /// </summary>
    public int FileID { get; }

    /// <summary>
    /// Shoko file location id.
    /// </summary>
    /// <value></value>
    public int FileLocationID { get; }

    /// <summary>
    /// The ID of the import folder the event was detected in.
    /// </summary>
    /// <value></value>
    public int ImportFolderID { get; }

    public FileEventSignalRModel(FileEventArgs eventArgs)
    {
        RelativePath = eventArgs.RelativePath;
        FileID = eventArgs.Video.Id;
        FileLocationID = eventArgs.FileLocation.Id;
        ImportFolderID = eventArgs.ImportFolder.Id;
    }
}
