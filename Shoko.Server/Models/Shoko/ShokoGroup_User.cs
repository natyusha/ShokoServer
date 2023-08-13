using System;
using Shoko.Models.PlexAndKodi;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Internal;

public class ShokoGroup_User : Base_User, IMemoryCollectable
{
    #region Database Columns

    public int GroupId { get; set; }

    public bool IsFavorite { get; set; }

    public int UnwatchedEpisodeCount { get; set; }

    public int WatchedEpisodeCount { get; set; }

    public int WatchedCount { get; set; }

    [Obsolete("Remove when removing desktop/v1 support. Only use in v1.")]
    public int PlayedCount { get; set; }

    [Obsolete("Remove when removing desktop/v1 support. Only use in v1.")]
    public int StoppedCount { get; set; }

    public DateTime? LastWatchedAt { get; set; }

    #endregion

    #region Constructors

    public ShokoGroup_User() { }

    public ShokoGroup_User(int userID, int groupID)
    {
        UserId = userID;
        GroupId = groupID;
    }

    #endregion

    #region Helpers

    public ShokoGroup GetGroup()
    {
        var group = RepoFactory.Shoko_Group.GetByID(GroupId);
        if (group == null)
            throw new NullReferenceException($"ShokoGroup with Id {GroupId} not found.");

        return group;
    }

    #endregion

    #region Plex Contract

    private DateTime _nextRegenAfter = DateTime.MinValue;

    private Video? _plexContract = null;

    public virtual Video? PlexContract
    {
        get
        {
            // If the contract is not null and the next regen is far off then return the currently cached contract.
            if (_plexContract != null && _nextRegenAfter > DateTime.Now)
                return _plexContract;

            // Otherwise try to update the contract.
            return UpdatePlexContract();
        }
    }

    public Video? UpdatePlexContract()
    {
        // Safely get the group, otherwise abort.
        var group = RepoFactory.Shoko_Group.GetByID(GroupId);
        if (group == null)
            return null;

        _nextRegenAfter = DateTime.Now.AddMinutes(10);
        var seriesList = group.GetAllSeries();
        return _plexContract = Helper.GenerateFromAnimeGroup(group, UserId, seriesList);
    }

    #endregion

    #region IMemoryCollectable

    public void CollectContractMemory()
    {
        _plexContract = null;
    }

    #endregion
}
