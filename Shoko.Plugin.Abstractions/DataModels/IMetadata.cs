using Shoko.Plugin.Abstractions.Enums;

#nullable enable
namespace Shoko.Plugin.Abstractions.DataModels;

public interface IMetadata
{
    string Id { get; }

    /// <summary>
    /// The metadata source.
    /// </summary>
    DataSource DataSource { get; }
}
