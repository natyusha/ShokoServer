
#nullable enable
namespace Shoko.Plugin.Abstractions.Enums;

public enum AnimeType
{
    None = -1, // Not on AniDB, but for ease of processing
    Movie = 0,
    OVA = 1,
    TV = 2,
    TVSpecial = 3,
    Web = 4,
    Other = 5,
}
