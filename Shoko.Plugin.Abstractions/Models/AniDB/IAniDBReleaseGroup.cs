
namespace Shoko.Plugin.Abstractions.Models.AniDB;

public interface IAniDBReleaseGroup
{
    int Id { get; }

    string? Name { get; }

    string? ShortName { get; }
}

