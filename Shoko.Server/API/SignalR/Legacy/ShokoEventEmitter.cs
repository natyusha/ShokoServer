using System;
using Microsoft.AspNetCore.SignalR;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Server.API.SignalR.Models;

namespace Shoko.Server.API.SignalR.Legacy;

public class ShokoEventEmitter : IDisposable
{
    private IHubContext<ShokoEventHub> Hub { get; set; }
    private IShokoEventHandler EventHandler { get; set; }

    public ShokoEventEmitter(IHubContext<ShokoEventHub> hub, IShokoEventHandler events)
    {
        Hub = hub;
        EventHandler = events;
        EventHandler.FileDetected += OnFileDetected;
        EventHandler.FileHashed += OnFileHashed;
        EventHandler.FileMatched += OnFileMatched;
        EventHandler.FileRenamed += OnFileRenamed;
        EventHandler.FileMoved += OnFileMoved;
        EventHandler.FileNotMatched += OnFileNotMatched;
        EventHandler.ShowUpdated += OnShowUpdated;
        EventHandler.EpisodeUpdated += OnEpisodeUpdated;
    }

    public void Dispose()
    {
        EventHandler.FileDetected -= OnFileDetected;
        EventHandler.FileHashed -= OnFileHashed;
        EventHandler.FileMatched -= OnFileMatched;
        EventHandler.FileRenamed -= OnFileRenamed;
        EventHandler.FileMoved -= OnFileMoved;
        EventHandler.FileNotMatched -= OnFileNotMatched;
        EventHandler.ShowUpdated -= OnShowUpdated;
        EventHandler.EpisodeUpdated -= OnEpisodeUpdated;
    }

    private async void OnFileDetected(object sender, FileDetectedEventArgs e)
    {
        await Hub.Clients.All.SendAsync("FileDetected", new FileDetectedEventSignalRModel(e));
    }

    private async void OnFileHashed(object sender, FileHashedEventArgs e)
    {
        await Hub.Clients.All.SendAsync("FileHashed", new FileHashedEventSignalRModel(e));
    }

    private async void OnFileMatched(object sender, FileMatchedEventArgs e)
    {
        await Hub.Clients.All.SendAsync("FileMatched", new FileMatchedEventSignalRModel(e));
    }

    private async void OnFileRenamed(object sender, FileRenamedEventArgs e)
    {
        await Hub.Clients.All.SendAsync("FileRenamed", new FileRenamedEventSignalRModel(e));
    }

    private async void OnFileMoved(object sender, FileMovedEventArgs e)
    {
        await Hub.Clients.All.SendAsync("FileMoved", new FileMovedEventSignalRModel(e));
    }

    private async void OnFileNotMatched(object sender, FileNotMatchedEventArgs e)
    {
        await Hub.Clients.All.SendAsync("FileNotMatched", new FileNotMatchedEventSignalRModel(e));
    }

    private async void OnShowUpdated(object sender, ShowUpdatedEventArgs e)
    {
        await Hub.Clients.All.SendAsync("SeriesUpdated", new ShowUpdatedEventSignalRModel(e));
    }

    private async void OnEpisodeUpdated(object sender, EpisodeUpdatedEventArgs e)
    {
        await Hub.Clients.All.SendAsync("EpisodeUpdated", new EpisodeUpdatedEventSignalRModel(e));
    }
}
