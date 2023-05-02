using System;
using Shoko.Plugin.Abstractions.Events;

namespace Shoko.Plugin.Abstractions;

public interface IShokoEventHandler
{
    /// <summary>
    /// Fired when a file is deleted and removed from Shoko.
    /// </summary>
    public event EventHandler<FileDeletedEventArgs> FileDeleted;

    /// <summary>
    /// Fired when a file is detected, either during a forced import/scan or a watched folder.
    /// Nothing has been done with the file yet here.
    /// </summary>
    event EventHandler<FileDetectedEventArgs> FileDetected;

    /// <summary>
    /// Fired when a file is hashed. Has hashes and stuff.
    /// </summary>
    event EventHandler<FileHashedEventArgs> FileHashed;

    /// <summary>
    /// Fired when a file is scanned but no changes to the cross-refernce
    /// were made. It can be because the file is unrecognized, or because
    /// there was no changes to the existing cross-references linked to the
    /// file.
    /// </summary>
    event EventHandler<FileNotMatchedEventArgs> FileNotMatched;

    /// <summary>
    /// Fired when a cross reference is made and data is gathered for a file. This has most if not all relevant data for a file. TMDB may take longer.
    /// Use <see cref="EpisodeUpdated"/> with a filter on the data source to ensure the desired data is gathered.
    /// </summary>
    event EventHandler<FileMatchedEventArgs> FileMatched;

    /// <summary>
    /// Fired when a file is renamed
    /// </summary>
    event EventHandler<FileRenamedEventArgs> FileRenamed;

    /// <summary>
    /// Fired when a file is moved
    /// </summary>
    event EventHandler<FileMovedEventArgs> FileMoved;

    /// <summary>
    /// Fired when an AniDB Ban happens...and it will.
    /// </summary>
    event EventHandler<AniDBBannedEventArgs> AniDBBanned;

    /// <summary>
    /// Fired on show metadata updates. Currently, AniDB, TMDB, etc. will trigger this.
    /// </summary>
    event EventHandler<ShowUpdatedEventArgs> ShowUpdated;

    /// <summary>
    /// Fired on episode metadata updates. Currently, AniDB, TMDB, etc. will trigger this.
    /// </summary>
    event EventHandler<EpisodeUpdatedEventArgs> EpisodeUpdated;

    /// <summary>
    /// Fired on movie metadata updates. Currently, TMDB is the only source for movie data.
    /// </summary>
    event EventHandler<MovieUpdatedEventArgs> MovieUpdated;

    /// <summary>
    /// Fired when the core settings has been saved.
    /// </summary>
    event EventHandler<SettingsSavedEventArgs> SettingsSaved;
}
