using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models;

public interface IMetadata<TId> : IMetadata
{
    /// <summary>
    /// The id of the metadata object.
    /// </summary>
    TId Id { get; }
}

public interface IMetadata
{
    /// <summary>
    /// The metadata source.
    /// </summary>
    DataSource DataSource { get; }
}
