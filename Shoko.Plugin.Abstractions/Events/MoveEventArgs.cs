using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.Shoko;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Represents the event arguments for a Move event. The event can be cancelled
/// by setting the <see cref="CancelEventArgs.Cancel"/> property to true or
/// skipped by not setting the result parameters.
/// </summary>
public class MoveEventArgs : CancelEventArgs
{
    /// <summary>
    /// A read-only list of import folders available to choose as a destination.
    /// Set the <see cref="DestinationImportFolder"/> property to one of these
    /// folders. Folders with <see cref="DropFolderType.Excluded"/> set are not
    /// included in this list.
    /// </summary>
    public IReadOnlyList<IImportFolder> AvailableFolders { get; }

    public IImportFolder ImportFolder { get; }

    /// <summary>
    /// The file location being moved.
    /// </summary>
    public IShokoVideoLocation VideoLocation { get; }

    /// <summary>
    /// The video metadata for the file being moved.
    /// </summary>
    public IShokoVideo Video { get; }

    /// <summary>
    /// The cross-references for the video.
    /// </summary>
    public IReadOnlyList<IShokoVideoCrossReference> CrossReferences { get; }

    /// <summary>
    /// The episodes linked directly to the file being moved.
    /// </summary>
    public IReadOnlyList<IShokoEpisode> Episodes { get; }

    /// <summary>
    /// The series linked directly to the file being moved.
    /// </summary>
    public IReadOnlyList<IShokoSeries> Series { get; }

    /// <summary>
    /// The groups linked indirectly to the file being moved.
    /// </summary>
    public IReadOnlyList<IShokoGroup> Groups { get; }

    /// <summary>
    /// The contents of the renamer script.
    /// </summary>
    public IRenameScript? Script { get; }

    public MoveEventArgs(IEnumerable<IImportFolder> availableFolders, IImportFolder importFolder, IShokoVideoLocation videoLocation, IRenameScript? script) : base()
    {
        AvailableFolders = availableFolders is IReadOnlyList<IImportFolder> list ? list : availableFolders.ToList();
        ImportFolder = importFolder;
        VideoLocation = videoLocation;
        Video = videoLocation.Video;
        CrossReferences = Video.AllCrossReferences;
        Episodes = CrossReferences
            .Select(xref => xref.Episode)
            .ToList();
        Series = CrossReferences
            .Select(xref => xref.Series)
            .DistinctBy(series => series.Id)
            .ToList();
        Groups = Series
            .Select(series => series.ParentGroup)
            .DistinctBy(group => group.Id)
            .ToList();
        Script = script;
    }
}
