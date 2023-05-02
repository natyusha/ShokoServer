using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Shoko.Plugin.Abstractions.Models;

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
    public IReadOnlyList<IImportFolder> AvailableFolders { get; set; }

    /// <summary>
    /// The file location being moved.
    /// </summary>
    public IShokoVideoFileLocation FileLocation { get; set; }

    /// <summary>
    /// The video metadata for the file being moved.
    /// </summary>
    public IShokoVideo Video { get; set; }

    /// <summary>
    /// The cross-references for the video.
    /// </summary>
    public IReadOnlyList<IVideoEpisodeCrossReference> CrossReferences { get; set; }

    /// <summary>
    /// The episodes linked directly to the file being moved.
    /// </summary>
    public IReadOnlyList<IShokoEpisode> Episodes { get; set; }

    /// <summary>
    /// The series linked directly to the file being moved.
    /// </summary>
    public IReadOnlyList<IShokoSeries> Series { get; set; }

    /// <summary>
    /// The groups linked indirectly to the file being moved.
    /// </summary>
    public IReadOnlyList<IShokoGroup> Groups { get; set; }

    /// <summary>
    /// The contents of the renamer script.
    /// </summary>
    public IRenameScript Script { get; set; }
    
    public MoveEventArgs(IEnumerable<IImportFolder> availableFolders, IShokoVideoFileLocation fileLocation, IRenameScript script) : base()
    {
        AvailableFolders = availableFolders is IReadOnlyList<IImportFolder> list ? list : availableFolders.ToList();
        FileLocation = fileLocation;
        Video = fileLocation.Video;
        CrossReferences = Video.CrossReferences;
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
