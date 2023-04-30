using Shoko.Plugin.Abstractions.Events;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class FileMovedEventSignalRModel : FileEventSignalRModel
{
    /// <summary>
    /// The relative path of the old file from the import folder base location.
    /// </summary>
    public string OldRelativePath { get; }

    /// <summary>
    /// The ID of the old import folder the event was detected in.
    /// </summary>
    /// <value></value>
    public int OldImportFolderID { get; }

    public FileMovedEventSignalRModel(FileMovedEventArgs eventArgs) : base(eventArgs)
    {
        OldRelativePath = eventArgs.OldRelativePath;
        OldImportFolderID = eventArgs.OldImportFolder.Id;
    }
}
