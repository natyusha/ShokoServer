using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Commons.Notification;
using Shoko.Server.API.SignalR.Models;
using Shoko.Server.Commands;
using Shoko.Server.Server;

namespace Shoko.Server.API.SignalR.Aggregate;

public class QueueEmitter : BaseEmitter, IDisposable
{
    private readonly Dictionary<string, object> _lastState = new();

    public QueueEmitter(IHubContext<AggregateHub> hub) : base(hub)
    {
        ShokoService.CmdProcessorGeneral.OnQueueCountChangedEvent += OnGeneralQueueCountChangedEvent;
        ShokoService.CmdProcessorHasher.OnQueueCountChangedEvent += OnHasherQueueCountChangedEvent;
        ShokoService.CmdProcessorImages.OnQueueCountChangedEvent += OnImageQueueCountChangedEvent;

        ShokoService.CmdProcessorGeneral.OnQueueStateChangedEvent += OnGeneralQueueStateChangedEvent;
        ShokoService.CmdProcessorHasher.OnQueueStateChangedEvent += OnHasherQueueStateChangedEvent;
        ShokoService.CmdProcessorImages.OnQueueStateChangedEvent += OnImageQueueStateChangedEvent;
        ServerState.Instance.PropertyChanged += ServerStatePropertyChanged;
    }

    public void Dispose()
    {
        ShokoService.CmdProcessorGeneral.OnQueueCountChangedEvent -= OnGeneralQueueCountChangedEvent;
        ShokoService.CmdProcessorHasher.OnQueueCountChangedEvent -= OnHasherQueueCountChangedEvent;
        ShokoService.CmdProcessorImages.OnQueueCountChangedEvent -= OnImageQueueCountChangedEvent;

        ShokoService.CmdProcessorGeneral.OnQueueStateChangedEvent -= OnGeneralQueueStateChangedEvent;
        ShokoService.CmdProcessorHasher.OnQueueStateChangedEvent -= OnHasherQueueStateChangedEvent;
        ShokoService.CmdProcessorImages.OnQueueStateChangedEvent -= OnImageQueueStateChangedEvent;
        ServerState.Instance.PropertyChanged -= ServerStatePropertyChanged;
    }

    private async void ServerStatePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == null) return;
        // Currently, only the DatabaseBlocked property, but we could use this for more.
        if (e.PropertyName == "DatabaseBlocked" || e.PropertyName.StartsWith("Server"))
        {
            await StateChangedAsync("ServerStateChanged", e.PropertyName, e.GetPropertyValue(sender));
        }
    }

    private async void OnGeneralQueueStateChangedEvent(QueueStateEventArgs e)
    {
        await StateChangedAsync("QueueStateChanged", "GeneralQueueState", new QueueStateSignalRModel(e.QueueState.queueState, e.QueueState.formatMessage()));
    }

    private async void OnHasherQueueStateChangedEvent(QueueStateEventArgs e)
    {
        await StateChangedAsync("QueueStateChanged", "HasherQueueState", new QueueStateSignalRModel(e.QueueState.queueState, e.QueueState.formatMessage()));
    }

    private async void OnImageQueueStateChangedEvent(QueueStateEventArgs e)
    {
        await StateChangedAsync("QueueStateChanged", "ImageQueueState", new QueueStateSignalRModel(e.QueueState.queueState, e.QueueState.formatMessage()));
    }

    private async void OnGeneralQueueCountChangedEvent(QueueCountEventArgs ev)
    {
        await StateChangedAsync("QueueCountChanged", "GeneralQueueCount", ev.QueueCount);
    }
        
    private async void OnHasherQueueCountChangedEvent(QueueCountEventArgs ev)
    {
        await StateChangedAsync("QueueCountChanged", "HasherQueueCount", ev.QueueCount);
    }
        
    private async void OnImageQueueCountChangedEvent(QueueCountEventArgs ev)
    {
        await StateChangedAsync("QueueCountChanged", "ImageQueueCount", ev.QueueCount);
    }

    private async Task StateChangedAsync(string method, string property, object currentState)
    {
        if (_lastState.ContainsKey(property) && _lastState.TryGetValue(property, out var previousState) &&
            previousState == currentState) return;

        _lastState[property] = currentState;
        await SendAsync(method, property, currentState);
    }

    public override object GetInitialMessage()
    {
        return new Dictionary<string, object>
        {
            {
                "GeneralQueueState",
                new QueueStateSignalRModel(ShokoService.CmdProcessorGeneral.QueueState.queueState, ShokoService.CmdProcessorGeneral.QueueState.formatMessage())
                
            },
            {
                "HasherQueueState",
                new QueueStateSignalRModel(ShokoService.CmdProcessorHasher.QueueState.queueState, ShokoService.CmdProcessorHasher.QueueState.formatMessage())
            },
            {
                "ImageQueueState",
                new QueueStateSignalRModel(ShokoService.CmdProcessorImages.QueueState.queueState, ShokoService.CmdProcessorImages.QueueState.formatMessage())
            },
            { "GeneralQueueCount", ShokoService.CmdProcessorGeneral.QueueCount },
            { "HasherQueueCount", ShokoService.CmdProcessorHasher.QueueCount },
            { "ImageQueueCount", ShokoService.CmdProcessorImages.QueueCount },
        };
    }
}
