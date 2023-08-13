using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Properties;
using Shoko.Server.Models.Internal;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class ShokoEpisode_UserRepository : BaseCachedRepository<ShokoEpisode_User, int>
{
    private PocoIndex<int, ShokoEpisode_User, (int UserID, int EpisodeID)>? UsersEpisodes;
    private PocoIndex<int, ShokoEpisode_User, int>? Users;
    private PocoIndex<int, ShokoEpisode_User, int>? Episodes;
    private PocoIndex<int, ShokoEpisode_User, (int UserID, int SeriesID)>? UsersSeries;

    protected override int SelectKey(ShokoEpisode_User entity) =>
        entity.Id;

    public override void PopulateIndexes()
    {
        UsersEpisodes = Cache.CreateIndex(a => (a.UserId, a.EpisodeId));
        Users = Cache.CreateIndex(a => a.UserId);
        Episodes = Cache.CreateIndex(a => a.EpisodeId);
        UsersSeries = Cache.CreateIndex(a => (a.UserId, a.SeriesId));
    }

    public override void RegenerateDb()
    {
        var cnt = 0;
        var sers =
            Cache.Values.Where(a => a.Id == 0)
                .ToList();
        var max = sers.Count;
        ServerState.Instance.ServerStartingStatus = string.Format(Resources.Database_Validating,
            typeof(ShokoEpisode_User).Name, " DbRegen");
        if (max <= 0)
        {
            return;
        }

        foreach (var g in sers)
        {
            Save(g);
            cnt++;
            if (cnt % 10 == 0)
            {
                ServerState.Instance.ServerStartingStatus = string.Format(
                    Resources.Database_Validating, typeof(ShokoEpisode_User).Name,
                    " DbRegen - " + cnt + "/" + max);
            }
        }

        ServerState.Instance.ServerStartingStatus = string.Format(Resources.Database_Validating,
            typeof(ShokoEpisode_User).Name,
            " DbRegen - " + max + "/" + max);
    }

    public List<ShokoEpisode_User> GetByUserId(int userId)
    {
        return ReadLock(() => Users!.GetMultiple(userId));
    }

    public List<ShokoEpisode_User> GetByEpisodeId(int epid)
    {
        return ReadLock(() => Episodes!.GetMultiple(epid));
    }

    public ShokoEpisode_User? GetByUserAndEpisodeIds(int userId, int episodeId)
    {
        return ReadLock(() => UsersEpisodes!.GetOne((userId, episodeId)));
    }

    public List<ShokoEpisode_User> GetByUserAndSeriesIds(int userId, int seriesId)
    {
        return ReadLock(() => UsersSeries!.GetMultiple((userId, seriesId)));
    }

    public List<ShokoEpisode_User> GetMostRecentlyWatched(int userId, int limit = 100)
    {
        return GetByUserId(userId)
            .Where(a => a.WatchedCount > 0)
            .OrderByDescending(a => a.LastWatchedAt)
            .Take(limit)
            .ToList();
    }

    public ShokoEpisode_User? GetLastWatchedEpisodeForSeries(int userId, int seriesId)
    {
        return GetByUserAndSeriesIds(userId, seriesId)
            .Where(a => a.LastWatchedAt.HasValue)
            .OrderByDescending(a => a.LastWatchedAt)
            .FirstOrDefault();
    }
}
