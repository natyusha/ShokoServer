using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Internal;

public class Shoko_User : IIdentity
{
    #region Database Columns

    /// <summary>
    /// Local user id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Username.
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// Encrypted password. Can be empty if no password is set.
    /// </summary>
    public string Password { get; set; } = "";

    /// <summary>
    /// True if the user is a system administrator.
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// True if this user's watch state is synced with AniDB.
    /// </summary>
    public bool IsAniDBUser { get; set; }

    /// <summary>
    /// True is this user's watch state is synced with Trakt.
    /// </summary>
    public bool IsTraktUser { get; set; }

    /// <summary>
    /// Restricted tags. Any group/series containing any of these tags will be
    /// rendered inaccessible to the user.
    /// </summary>
    public HashSet<string> RestrictedTags { get; set; } = new();

    #endregion

    private ShokoUser_Plex? _plex { get; set; }

    public ShokoUser_Plex Plex
    {
        get
        {
            if (_plex != null)
                return _plex;
            return _plex = RepoFactory.ShokoUser_Plex.CreateOrGetByUserId(Id);
        }
    }

    /// <summary>
    /// Returns whether a user is allowed to view this series
    /// </summary>
    /// <param name="ser"></param>
    /// <returns></returns>
    public bool AllowedSeries(ShokoSeries ser)
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
    public bool AllowedAnime(AniDB_Anime anime)
    {
        if (this.RestrictedTags.Count == 0) return true;
        return !this.RestrictedTags.FindInEnumerable(anime.GetTags().Select(a => a.TagName));
    }

    public bool AllowedGroup(ShokoGroup grp)
    {
        if (this.RestrictedTags.Count == 0) return true;
        if (grp.Contract == null) return false;
        return !this.RestrictedTags.FindInEnumerable(grp.Contract.Stat_AllTags);
    }

    public bool AllowedTag(AniDB_Tag tag)
    {
        return !this.RestrictedTags.Contains(tag.TagName);
    }

    public void UpdateGroupFilters()
    {
        var gfs = RepoFactory.Shoko_Group_Filter.GetAll();
        var allGrps = RepoFactory.Shoko_Group.GetAllTopLevelGroups(); // No Need of subgroups
        foreach (var gf in gfs)
        {
            bool change = false;
            foreach (var grp in allGrps)
            {
                var cgrp = grp.GetUserContract(Id);
                change |= gf.UpdateGroupFilterFromGroup(cgrp, this);
            }
            if (change)
                RepoFactory.Shoko_Group_Filter.Save(gf);
        }
    }

    public CL_JMMUser ToClient()
    {
        var plex = Plex;
        return new()
        {
            JMMUserID = Id,
            CanEditServerSettings = IsAdmin ? 1 : 0,
            HideCategories = string.Join(",", RestrictedTags),
            IsAdmin = IsAdmin ? 1 : 0,
            IsAniDBUser = IsAniDBUser ? 1 : 0,
            IsTraktUser = IsTraktUser ? 1 : 0,
            Password = Password,
            PlexToken = !string.IsNullOrEmpty(plex.Token) ? "<hidden>" : null,
            PlexUsers = string.Join(",", plex.LocalUsers),
            Username = Username,
        };
    }
    #region IIdentity

    string IIdentity.AuthenticationType => "API";

    bool IIdentity.IsAuthenticated => true;

    string IIdentity.Name => Username;

    #endregion
}
