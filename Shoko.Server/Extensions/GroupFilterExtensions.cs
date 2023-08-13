
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Server.Models.Internal;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Extensions;

public static class GroupFilterExtensions

{
    #region Shoko Series User

    public static HashSet<GroupFilterConditionType> GetConditionTypesChanged(this ShokoSeries_User contract, ShokoSeries_User? oldContract)
    {
        var conditions = new HashSet<GroupFilterConditionType>();
        if (oldContract == null || oldContract.UnwatchedEpisodeCount > 0 != contract.UnwatchedEpisodeCount > 0)
            conditions.Add(GroupFilterConditionType.HasUnwatchedEpisodes);

        if (oldContract == null || oldContract.WatchedDate != contract.WatchedDate)
            conditions.Add(GroupFilterConditionType.EpisodeWatchedDate);

        if (oldContract == null || oldContract.WatchedEpisodeCount > 0 != contract.WatchedEpisodeCount > 0)
            conditions.Add(GroupFilterConditionType.HasWatchedEpisodes);

        return conditions;
    }

    public static void UpdateGroupFilter(this ShokoSeries_User contract, HashSet<GroupFilterConditionType> types)
    {
        var series = RepoFactory.Shoko_Series.GetByID(contract.SeriesId);
        var user = RepoFactory.Shoko_User.GetByID(contract.UserId);
        if (series != null && user != null)
            series.UpdateGroupFilters(types, user);
    }

    public static void DeleteFromFilters(this ShokoSeries_User contract)
    {
        foreach (var groupFilter in RepoFactory.Shoko_Group_Filter.GetAll())
        {
            var isChanged = false;
            if (groupFilter.SeriesIds.ContainsKey(contract.UserId))
            {
                if (groupFilter.SeriesIds[contract.UserId].Contains(contract.SeriesId))
                {
                    groupFilter.SeriesIds[contract.UserId].Remove(contract.SeriesId);
                    isChanged = true;
                }
            }

            if (isChanged)
                RepoFactory.Shoko_Group_Filter.Save(groupFilter);
        }
    }

    #endregion

    #region Shoko Group User

    public static HashSet<GroupFilterConditionType> GetConditionTypesChanged(this ShokoGroup_User contract, ShokoGroup_User? oldContract)
    {
        var conditions = new HashSet<GroupFilterConditionType>();

        if (oldContract == null ||
            oldContract.UnwatchedEpisodeCount > 0 != contract.UnwatchedEpisodeCount > 0)
        {
            conditions.Add(GroupFilterConditionType.HasUnwatchedEpisodes);
        }

        if (oldContract == null || oldContract.IsFavorite != contract.IsFavorite)
        {
            conditions.Add(GroupFilterConditionType.Favourite);
        }

        if (oldContract == null || oldContract.LastWatchedAt != contract.LastWatchedAt)
        {
            conditions.Add(GroupFilterConditionType.EpisodeWatchedDate);
        }

        if (oldContract == null || oldContract.WatchedEpisodeCount > 0 != contract.WatchedEpisodeCount > 0)
        {
            conditions.Add(GroupFilterConditionType.HasWatchedEpisodes);
        }

        return conditions;
    }

    public static void UpdateGroupFilters(this ShokoGroup_User contract, HashSet<GroupFilterConditionType> types)
    {
        var grp = RepoFactory.Shoko_Group.GetByID(contract.GroupId);
        var usr = RepoFactory.Shoko_User.GetByID(contract.UserId);
        if (grp != null && usr != null)
        {
            grp.UpdateGroupFilters(types, usr);
        }
    }

    public static void DeleteFromFilters(this ShokoGroup_User contract)
    {
        var toSave = RepoFactory.Shoko_Group_Filter.GetAll().AsParallel()
            .Where(gf => gf.DeleteGroupFromFilters(contract.UserId, contract.GroupId)).ToList();
        RepoFactory.Shoko_Group_Filter.Save(toSave);
    }

    #endregion

}
