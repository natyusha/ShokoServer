using System;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models.Provider;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Fired on season metadata updates.
/// </summary>
public class SeasonUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// The data source which was updated.
    /// </summary>
    public DataSource DataSource { get; set; }

    /// <summary>
    /// The show assosiated with the season that got updated.
    /// </summary>
    public IShowMetadata Show { get; set; }

    /// <summary>
    /// The updated season data.
    /// </summary>
    public ISeasonMetadata Season { get; set; }

    public SeasonUpdatedEventArgs(ISeasonMetadata seasonMetadata)
    {
        DataSource = seasonMetadata.DataSource;
        Show = seasonMetadata.Show;
        Season = seasonMetadata;
    }
}
