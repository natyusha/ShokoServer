using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models;

public interface IMetadata
{
    string Id { get; }

    /// <summary>
    /// The metadata source.
    /// </summary>
    DataSource DataSource { get; }
}
