using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions.Events;

public class FileMatchedEventArgs : FileEventArgs
{
    /// <summary>
    /// The cross-references for the video.
    /// </summary>
    public IReadOnlyList<IVideoEpisodeCrossReference> CrossReferences { get; set; }

    /// <summary>
    /// The episodes linked directly to the video.
    /// </summary>
    public IReadOnlyList<IShokoEpisode> Episodes { get; set; }

    /// <summary>
    /// The series linked directly to the video.
    /// </summary>
    public IReadOnlyList<IShokoSeries> Series { get; set; }

    /// <summary>
    /// The groups linked indirectly to the video.
    /// </summary>
    public IReadOnlyList<IShokoGroup> Groups { get; set; }

    public FileMatchedEventArgs(IShokoVideoFileLocation fileLocation) : base(fileLocation)
    {
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
    }
}
