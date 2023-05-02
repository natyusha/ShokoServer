
namespace Shoko.Plugin.Abstractions.DataModels.AniDB;

public interface IAniDBReleaseGroup
{
    int Id { get; }

    string? Name { get; }

    string? ShortName { get; }
}

