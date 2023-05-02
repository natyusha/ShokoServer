using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.DataModels;

public interface IRating
{
    /// <summary>
    /// The value of the rating.
    /// </summary>
    decimal Value { get; }

    /// <summary>
    /// The maximum possible value of the rating. Assuming an integer value type, as the maximum value should be a whole number.
    /// </summary>
    int MaxValue { get; }

    /// <summary>
    /// The data source of the rating, e.g. AniDB.
    /// </summary>
    DataSource DataSource { get; }

    /// <summary>
    /// The number of votes cast for this rating, if known, otherwise null.
    /// </summary>
    int? Votes { get; }

    /// <summary>
    /// A string value indicating the type of rating, e.g. "temporary" vs
    /// "permanent", or any other situations that may arise later. Can be null.
    /// </summary>
    string? Type { get; }
}
