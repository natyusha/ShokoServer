
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.API.v3.Models.Plex;

/// <summary>
/// Plex user settings
/// </summary>
public class PlexUserSettings
{
    public PlexUserSettings(JMMUser_Plex settings)
    {
        LocalUsers = new();
    }
    
    public PlexUserSettings MergeWithExisting(JMMUser_Plex existing)
    {
        if (!existing.LocalUsers.SetEquals(LocalUsers))
        {
            existing.LocalUsers = LocalUsers;
            RepoFactory.JMMUser_Plex.Save(existing);
        }

        return new(existing);
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
