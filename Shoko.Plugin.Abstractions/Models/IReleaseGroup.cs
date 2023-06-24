
namespace Shoko.Plugin.Abstractions.Models;

public interface IReleaseGroup : IMetadata<int>
{
    string? Name { get; }

    string? ShortName { get; }
}

