using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class EpisodeUpdatedEventSignalRModel
{
    /// <summary>
    /// The id of the episode affected by the update.
    /// </summary>
    public string EpisodeID { get; }

    /// <summary>
    /// The ids of the shoko episodes affected by the update.
    /// </summary>
    public IReadOnlyList<int> ShokoEpisodeIDs { get; }

    /// <summary>
    /// The id of the season affected by the update, if applicable.
    /// </summary>
    public string? SeasonID { get; }

    /// <summary>
    /// The id of the show affected by the update.
    /// </summary>
    public string ShowID { get; }

    /// <summary>
    /// The ids of the shoko series affected by the update.
    /// </summary>
    public IReadOnlyList<int> ShokoSeriesIDs { get; }

    /// <summary>
    /// The data-source/provider for the metadata that got updated.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public DataSource DataSource { get; }

    public EpisodeUpdatedEventSignalRModel(EpisodeUpdatedEventArgs eventArgs)
    {
        EpisodeID = eventArgs.Episode.Id;
        ShokoEpisodeIDs = eventArgs.Episode.ShokoEpisodeIds;
        SeasonID = eventArgs.Season?.Id;
        ShowID = eventArgs.Show.Id;
        ShokoSeriesIDs = eventArgs.Show.ShokoSeriesIds;
        DataSource = eventArgs.DataSource;
    }

}
