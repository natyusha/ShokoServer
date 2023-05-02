using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Models.AniDB;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models;

public interface IShokoVideo
{
    #region Identifiers

    /// <summary>
    /// The unique identifier of the video.
    /// </summary>
    int Id { get; }

    #endregion

    #region Links

    /// <summary>
    /// A read-only list of video files associated with the video.
    /// </summary>
    IReadOnlyList<IShokoVideoLocation> Locations { get; }

    IReadOnlyList<IVideoEpisodeCrossReference> CrossReferences { get; }

    IReadOnlyList<IShokoEpisode> Episodes { get; }

    IReadOnlyList<IShokoSeries> Series { get; }

    IReadOnlyList<IShokoGroup> Groups { get; }

    /// <summary>
    /// The AniDB file metadata associated with the video file. This will be null for manually added files, which can be used to determine if a file was manually added.
    /// </summary>
    IAniDBFile? AniDB { get; }

    #endregion

    #region Metadata

    /// <summary>
    /// The cross-refernece sources used, or <see cref="DataSource.None"/> if
    /// the video is still not linked to any episodes.
    /// </summary>
    DataSource CrossReferenceSources { get; }

    /// <summary>
    /// Indicates the video is marked as a variation.
    /// </summary>
    bool IsVariation { get; }

    /// <summary>
    /// The video file size counted in bytes.
    /// </summary>
    long Size { get; }

    /// <summary>
    /// Try to fit this file's resolution to something like 1080p, 480p, etc
    /// </summary>
    string Resolution { get; }

    /// <summary>
    /// The duration of the file.
    /// </summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// The relevant hashes for the video file. The CRC hash is the only one that should be used, but other hashes may be used for clever uses of the API.
    /// </summary>
    IHashes Hashes { get; }

    /// <summary>
    /// The media information data for the video file. This may be null if we
    /// failed to parse the media info for the file.
    /// </summary>
    IMediaInfo? Media { get; }

    /// <summary>
    /// When the file was last imported. "Imported" in this context is when it
    /// was linked to any episodes, be it automatically or manually.
    ///
    /// Usually a file is only imported once, but there may be exceptions,
    /// especially when manually linking the video files.
    /// </summary>
    DateTime? ImportedAt { get; }

    /// <summary>
    /// When the first file location for this video was discovered, and the
    /// video record was made.
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// When the video metadata was last updated.
    /// </summary>
    DateTime LastUpdatedAt { get; }

    #endregion
}
