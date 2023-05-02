using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.DataModels;

public interface IMetadata
{
    string Id { get; }

    /// <summary>
    /// The metadata source.
    /// </summary>
    DataSource DataSource { get; }
}
