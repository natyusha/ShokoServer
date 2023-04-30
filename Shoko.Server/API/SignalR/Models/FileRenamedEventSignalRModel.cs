using Shoko.Plugin.Abstractions.Events;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class FileRenamedEventSignalRModel : FileEventSignalRModel
{
    /// <summary>
    /// The new File name.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// The old file name.
    /// </summary>
    public string OldFileName { get; }

    public FileRenamedEventSignalRModel(FileRenamedEventArgs eventArgs) : base(eventArgs)
    {
        FileName = eventArgs.FileName;
        OldFileName = eventArgs.OldFileName;
    }
}
