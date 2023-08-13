
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models;

#nullable enable
namespace Shoko.Server.Models.Internal;

public class Custom_ReleaseGroup : IReleaseGroup
{
    public int Id { get; set; }

    public string Name { get; set; }

    public string ShortName { get; set; }

    public DataSource DataSource => DataSource.User;

    public Custom_ReleaseGroup()
    {
        Name = string.Empty;
        ShortName = string.Empty;
    }

    public Custom_ReleaseGroup(string name, string? shortName = null)
    {
        Name = name;
        ShortName = shortName ?? name;
    }
}
