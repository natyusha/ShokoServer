using System;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Fired on series metadata updates, currently, AniDB, TvDB, etc will trigger this
/// </summary>
public class ShowUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// The data source which was updated.
    /// </summary>
    public DataSource DataSource { get; set; }

    /// <summary>
    /// The updated show info.
    /// </summary>
    public IShowMetadata Show { get; set; }

    public ShowUpdatedEventArgs(IShowMetadata showMetadata)
    {
        DataSource = showMetadata.DataSource;
        Show = showMetadata;
    }
}
