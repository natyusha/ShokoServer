using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

public class FileMatchedEventSignalRModel : FileEventSignalRModel
{
    /// <summary>
    /// Cross-references of episodes and series linked to the file.
    /// </summary>
    public IReadOnlyList<FileMatchedCrossReference> CrossReferences { get; }

    public FileMatchedEventSignalRModel(FileMatchedEventArgs eventArgs) : base(eventArgs)
    {
        var xRefs = eventArgs.CrossReferences;
        var seriesToGroupDict = xRefs
            .DistinctBy(e => e.SeriesId)
            .ToDictionary(s => s.SeriesId, s => s.Series.ParentGroupId);
        CrossReferences = xRefs
            .Select(xref => new FileMatchedCrossReference(xref, seriesToGroupDict[xref.SeriesId]))
            .ToList();
    }
}

public class FileMatchedCrossReference
{
    /// <summary>
    /// Shoko episode id.
    /// </summary>
    public int EpisodeID { get; }

    /// <summary>
    /// Shoko series id.
    /// </summary>
    public int SeriesID { get; }

    /// <summary>
    /// Shoko group id.
    /// </summary>
    public int GroupID { get; }

    public int Order { get; }

    public decimal Percentage { get; }

    [JsonConverter(typeof(StringEnumConverter))]
    public DataSource DataSource { get; }

    public FileMatchedCrossReference(IVideoEpisodeCrossReference xref, int groupID)
    {
        EpisodeID = xref.EpisodeId;
        SeriesID = xref.SeriesId;
        GroupID = groupID;
        Order = xref.Order;
        Percentage = xref.Percentage;
        DataSource = xref.DataSource;
    }
}
