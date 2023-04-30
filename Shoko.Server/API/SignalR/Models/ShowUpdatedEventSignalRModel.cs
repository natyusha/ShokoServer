using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class ShowUpdatedEventSignalRModel
{
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

    public ShowUpdatedEventSignalRModel(ShowUpdatedEventArgs eventArgs)
    {
        ShowID = eventArgs.Show.Id;
        ShokoSeriesIDs = eventArgs.Show.ShokoSeriesIds;
        DataSource = eventArgs.DataSource;
    }
}
