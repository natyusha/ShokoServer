
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Plex;

/// <summary>
/// Plex user settings
/// </summary>
public class PlexUserSettings
{
    public PlexUserSettings()
    {
        LocalUsers = new();
    }

    /// <summary>
    /// A list of local Plex usernames to sync with the current Shoko user
    /// through the Plex web-hook.
    /// </summary>
    /// <remarks>
    /// You only need to configure this setting if you haven't linked your Shoko
    /// account to your Plex account.
    /// </remarks>
    [Required]
    public HashSet<string> LocalUsers { get; set; }
}
