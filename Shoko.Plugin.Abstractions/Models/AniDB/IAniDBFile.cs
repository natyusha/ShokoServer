using System;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models.AniDB;

public interface IAniDBFile
{
    /// <summary>
    /// The ID of the file on AniDB
    /// </summary>
    int Id { get; }

    /// <summary>
    /// Info about the release group of the file
    /// </summary>
    IAniDBReleaseGroup ReleaseGroup { get; }

    /// <summary>
    /// Where the file was ripped from, bluray, dvd, etc
    /// </summary>
    FileSource Source { get; }

    /// <summary>
    /// The Filename as released, according to AniDB. It's usually correct.
    /// </summary>
    string OriginalFilename { get; }

    /// <summary>
    /// Description of the file on AniDB. This will often be blank, and it's generally not useful
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Usually 1. Sometimes 2. 3 happens. It's incremented when a release is updated due to errors 
    /// </summary>
    int Version { get; }

    /// <summary>
    /// Indicates the released file contains censorship. This mostly applies to
    /// R-rated content, but may also apply to normal TV series releases which
    /// later releases a BD (or DVD) version without the censorship.
    /// </summary>
    bool IsCensored { get; }
    
    /// <summary>
    /// Indicates this file version is depreacated and you should seek out a
    /// newer version from the release group (or another source).
    /// </summary>
    bool IsDeprecated { get; }

    /// <summary>
    /// Indicates the video contains chapters.
    /// </summary>
    bool IsChaptered { get; }
    
    /// <summary>
    /// The reported file size from AniDB.
    /// If you got this far and it doesn't match, something very odd has
    /// occurred.
    /// </summary>
    long FileSize { get; }

    /// <summary>
    /// AniDB's user input data for streams
    /// </summary>
    AniDBMediaData MediaInfo { get; }

    /// <summary>
    /// When the file was released, according to AniDB. This will be wrong for a lot of older or less popular anime
    /// </summary>
    DateTime? ReleaseDate { get; }

    /// <summary>
    /// When the local metadata was last updated.
    /// </summary>
    DateTime LastUpdated { get; }
}
