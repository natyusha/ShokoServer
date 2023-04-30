using Shoko.Models.Queue;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class QueueStateSignalRModel
{
    /// <summary>
    /// The new queue state.
    /// </summary>
    public QueueStateEnum State { get; }

    /// <summary>
    /// The human-readable description of the queue state.
    /// </summary>
    public string Description { get; }

    public QueueStateSignalRModel(QueueStateEnum state, string description)
    {
        State = state;
        Description = description;
    }
}
