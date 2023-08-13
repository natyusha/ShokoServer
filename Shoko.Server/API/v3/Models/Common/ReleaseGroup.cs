
#nullable enable
using Shoko.Plugin.Abstractions.Models;

namespace Shoko.Server.API.v3.Models.Common;

public class ReleaseGroup
{
    /// <summary>
    /// Release group id.
    /// </summary>
    public int ID { get; }

    /// <summary>
    /// The release group's long name, if any. E.g.
    /// "Unlimited Translation Works".
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// The release group's short-form name, if any. E.g. "UTW".
    /// </summary>
    public string? ShortName { get; }

    /// <summary>
    /// The release group's metadata source.
    /// </summary>
    public DataSource Source { get; }

    public ReleaseGroup(IReleaseGroup releaseGroup)
    {
        ID = releaseGroup.Id;
        Name = releaseGroup.Name;
        ShortName = releaseGroup.ShortName;
        Source = (DataSource)releaseGroup.DataSource;
    }
}
