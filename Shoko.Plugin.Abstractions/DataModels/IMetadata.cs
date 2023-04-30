

using Shoko.Plugin.Abstractions.Enums;

public interface IMetadata
{
    public string Id { get; }

    /// <summary>
    /// The metadata source.
    /// </summary>
    DataSource DataSource { get; }
}
