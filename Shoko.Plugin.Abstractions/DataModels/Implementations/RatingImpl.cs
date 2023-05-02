using Shoko.Plugin.Abstractions.Enums;

#nullable enable
namespace Shoko.Plugin.Abstractions.DataModels.Implementations;

public class RatingImpl : IRating
{
    /// <inheritdoc/>
    public decimal Value { get; set; }

    /// <inheritdoc/>
    public int MaxValue { get; set; }

    /// <inheritdoc/>
    public int? Votes { get; set; }

    /// <inheritdoc/>
    public string? Type { get; set; }

    /// <inheritdoc/>
    public DataSource DataSource { get; set; }

    public RatingImpl()
    {
        Value = 0;
        MaxValue = 10;
        Votes = null;
        Type = null;
        DataSource = DataSource.None;
    }

    public RatingImpl(DataSource source, decimal value, int maxValue = 10, int? votes = null, string? type = null)
    {
        Value = value;
        MaxValue = maxValue;
        Votes = votes;
        Type = type;
        DataSource = source;
    }
}
