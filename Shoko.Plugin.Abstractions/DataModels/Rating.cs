using Shoko.Plugin.Abstractions.Enums;

#nullable enable
namespace Shoko.Plugin.Abstractions.DataModels;

public class Rating : IRating
{
    /// <summary>
    /// The value of the rating.
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>
    /// The maximum possible value of the rating.
    /// </summary>
    public int MaxValue { get; set; }

    /// <summary>
    /// The number of votes cast for this rating, if known, otherwise null.
    /// </summary>
    public int? Votes { get; set; }

    /// <summary>
    /// A string value indicating the type of rating, e.g. "temporary" vs
    /// "permanent", or any other situations that may arise later. Can be null.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// The data source of the rating, e.g. AniDB.
    /// </summary>
    public DataSource Source { get; set; }
}
