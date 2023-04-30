using Shoko.Plugin.Abstractions.Events;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class FileDeletedEventSignalRModel : FileEventSignalRModel
{
    public FileDeletedEventSignalRModel(FileDeletedEventArgs eventArgs) : base(eventArgs) { }
}
