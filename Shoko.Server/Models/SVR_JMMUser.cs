using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.Principal;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models;

public class SVR_JMMUser : JMMUser, IIdentity
{
    public SVR_JMMUser()
    {
    }

    private JMMUser_Plex __plex = null;

    public JMMUser_Plex Plex
    {
        get
        {
            if (__plex != null)
                return __plex;

            // Try to get existing settings.
            var plexUserSettings = RepoFactory.JMMUser_Plex.GetByUserID(JMMUserID);
            if (plexUserSettings != null)
                return __plex = plexUserSettings;

            // Create the settings now.
            plexUserSettings = new(JMMUserID);
            RepoFactory.JMMUser_Plex.Save(plexUserSettings);
            return __plex = plexUserSettings;
        }
    }

    /// <summary>
    /// Returns whether a user is allowed to view this series
    /// </summary>
    /// <param name="ser"></param>
    /// <returns></returns>
    public bool AllowedSeries(SVR_AnimeSeries ser)
    {
        if (this.RestrictedTags.Count == 0) return true;
        var anime = ser?.GetAnime();
        if (anime == null) return false;
        return !this.RestrictedTags.FindInEnumerable(anime.GetTags().Select(a => a.TagName));
    }

    /// <summary>
    /// Returns whether a user is allowed to view this anime
    /// </summary>
    /// <param name="anime"></param>
    /// <returns></returns>
    public bool AllowedAnime(SVR_AniDB_Anime anime)
    {
        if (this.RestrictedTags.Count == 0) return true;
        return !this.RestrictedTags.FindInEnumerable(anime.GetTags().Select(a => a.TagName));
    }

    public bool AllowedGroup(SVR_AnimeGroup grp)
    {
        if (this.RestrictedTags.Count == 0) return true;
        if (grp.Contract == null) return false;
        return !this.RestrictedTags.FindInEnumerable(grp.Contract.Stat_AllTags);
    }

    public bool AllowedTag(AniDB_Tag tag)
    {
        return !this.RestrictedTags.Contains(tag.TagName);
    }

    public static bool CompareUser(SVR_JMMUser olduser, SVR_JMMUser newuser)
    {
        if (olduser == null || !olduser.RestrictedTags.SetEquals(newuser.RestrictedTags))
            return true;
        return false;
    }

    public void UpdateGroupFilters()
    {
        IReadOnlyList<SVR_GroupFilter> gfs = RepoFactory.GroupFilter.GetAll();
        List<SVR_AnimeGroup> allGrps = RepoFactory.AnimeGroup.GetAllTopLevelGroups(); // No Need of subgroups
        foreach (SVR_GroupFilter gf in gfs)
        {
            bool change = false;
            foreach (SVR_AnimeGroup grp in allGrps)
            {
                CL_AnimeGroup_User cgrp = grp.GetUserContract(JMMUserID);
                change |= gf.UpdateGroupFilterFromGroup(cgrp, this);
            }
            if (change)
                RepoFactory.GroupFilter.Save(gf);
        }
    }

    public CL_JMMUser ToClient()
    {
        var plex = Plex;
        return new()
        {
            CanEditServerSettings = IsAdmin ? 1 : 0,
            HideCategories = string.Join(",", RestrictedTags),
            IsAdmin = IsAdmin ? 1 : 0,
            IsAniDBUser = IsAniDBUser ? 1 : 0,
            IsTraktUser = IsTraktUser ? 1 : 0,
            JMMUserID = JMMUserID,
            Password = Password,
            PlexToken = !string.IsNullOrEmpty(plex.Token) ? "<hidden>" : null,
            PlexUsers = string.Join(",", plex.LocalUsers),
            Username = Username,
        };
    }

    // IUserIdentity implementation
    public string UserName
    {
        get { return Username; }
    }

    //[JsonIgnore]
    [NotMapped] public IEnumerable<string> Claims { get; set; }

    [NotMapped] string IIdentity.AuthenticationType => "API";

    [NotMapped] bool IIdentity.IsAuthenticated => true;

    [NotMapped] string IIdentity.Name => Username;
}
