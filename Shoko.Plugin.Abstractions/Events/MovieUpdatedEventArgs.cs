using System;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Fired on movie metadata updates, currently, AniDB, TvDB, etc will trigger this
/// </summary>
public class MovieUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// The data source which was updated.
    /// </summary>
    public DataSource DataSource { get; set; }

    /// <summary>
    /// The updated movie info.
    /// </summary>
    public IMovieMetadata Movie { get; set; }

    public MovieUpdatedEventArgs(IMovieMetadata movieMetadata)
    {
        DataSource = movieMetadata.DataSource;
        Movie = movieMetadata;
    }
}
