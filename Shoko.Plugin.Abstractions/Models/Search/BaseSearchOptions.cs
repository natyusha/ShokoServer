
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models.Search;

public class BaseSearchOptions
{
    public int? Limit { get; set; }

    public int? Offset { get; set; }

    public DataSource? DataSource { get; set; }
}
