using System;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Currently, these will fire a lot in succession, as these are updated in batch with a series.
/// </summary>
public class EpisodeUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// The data source which was updated.
    /// </summary>
    public DataSource DataSource { get; set; }

    /// <summary>
    /// The show assosiated with the episode that got updated.
    /// </summary>
    public IShowMetadata Show { get; set; }

    /// <summary>
    /// The season assosiated with the episode that got updated, if appropriate.
    /// </summary>
    /// <value></value>
    public ISeasonMetadata? Season { get; set; }

    /// <summary>
    /// The updated episode data.
    /// </summary>
    public IEpisodeMetadata Episode { get; set; }

    public EpisodeUpdatedEventArgs(IEpisodeMetadata episodeMetadata)
    {
        DataSource = episodeMetadata.DataSource;
        Show = episodeMetadata.Show;
        Season = episodeMetadata.Season;
        Episode = episodeMetadata;
    }
}
