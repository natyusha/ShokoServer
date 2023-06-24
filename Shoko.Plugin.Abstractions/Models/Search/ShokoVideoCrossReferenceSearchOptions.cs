
namespace Shoko.Plugin.Abstractions.Models.Search;

public class ShokoVideoCrossReferenceSearchOptions : BaseSearchOptions
{
    public int? AnidbReleaseGroupId { get; set; }

    public int? CustomReleaseGroupId { get; set; }
}
