using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.Shoko;

namespace Shoko.Plugin.Abstractions.Events;

public class RenameEventArgs : CancelEventArgs
{
    /// <summary>
    /// The file location being renamed.
    /// </summary>
    public IShokoVideoLocation VideoLocation { get; set; }

    /// <summary>
    /// The video metadata for the file being renamed.
    /// </summary>
    public IShokoVideo Video { get; set; }

    /// <summary>
    /// The cross-references for the video.
    /// </summary>
    public IReadOnlyList<IShokoVideoCrossReference> CrossReferences { get; set; }

    /// <summary>
    /// The episodes linked directly to the file being renamed.
    /// </summary>
    public IReadOnlyList<IShokoEpisode> Episodes { get; set; }

    /// <summary>
    /// The series linked directly to the file being renamed.
    /// </summary>
    public IReadOnlyList<IShokoSeries> Series { get; set; }

    /// <summary>
    /// The groups linked indirectly to the file being renamed.
    /// </summary>
    public IReadOnlyList<IShokoGroup> Groups { get; set; }

    /// <summary>
    /// The contents of the renamer scrpipt
    /// </summary>
    public IRenameScript? Script { get; set; }

    public RenameEventArgs(IShokoVideoLocation fileLocation, IRenameScript? script) : base()
    {
        VideoLocation = fileLocation;
        Video = fileLocation.Video;
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
