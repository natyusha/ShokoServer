using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models.Internal;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class ShokoSeries_UserRepository : BaseCachedRepository<ShokoSeries_User, int>
{
    private PocoIndex<int, ShokoSeries_User, int>? Users;
    private PocoIndex<int, ShokoSeries_User, int>? Series;
    private PocoIndex<int, ShokoSeries_User, (int UserID, int SeriesID)>? UsersSeries;
    private Dictionary<int, ChangeTracker<int>> Changes = new();

    public ShokoSeries_UserRepository()
    {
        EndDeleteCallback = cr =>
        {
            if (!Changes.ContainsKey(cr.UserId))
            {
                Changes[cr.UserId] = new ChangeTracker<int>();
            }

            Changes[cr.UserId].Remove(cr.SeriesId);

            cr.DeleteFromFilters();
        };
    }

    protected override int SelectKey(ShokoSeries_User entity) =>
        entity.Id;

    public override void PopulateIndexes()
    {
        Users = Cache.CreateIndex(a => a.UserId);
        Series = Cache.CreateIndex(a => a.SeriesId);
        UsersSeries = Cache.CreateIndex(a => (a.UserId, a.SeriesId));
    }

    public override void RegenerateDb()
    {
    }


    public override void Save(ShokoSeries_User obj)
    {
        ShokoSeries_User? old = null;
        lock (GlobalDBLock)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
                old = session.Get<ShokoSeries_User>(obj.Id);
        }

        var changedTypes = obj.GetConditionTypesChanged(old);

        base.Save(obj);

        if (!Changes.ContainsKey(obj.UserId))
            Changes[obj.UserId] = new ChangeTracker<int>();
        Changes[obj.UserId].AddOrUpdate(obj.SeriesId);

        obj.UpdateGroupFilter(changedTypes);
    }

    public ShokoSeries_User GetByUserAndSeriesIds(int userId, int seriesId)
    {
        return ReadLock(() => UsersSeries!.GetOne((userId, seriesId)));
    }

    public List<ShokoSeries_User> GetByUserId(int userId)
    {
        return ReadLock(() => Users!.GetMultiple(userId));
    }

    public List<ShokoSeries_User> GetBySeriesId(int seriesId)
    {
        return ReadLock(() => Series!.GetMultiple(seriesId));
    }

    public List<ShokoSeries_User> GetMostRecentlyWatched(int userId)
    {
        return
            GetByUserId(userId)
                .Where(a => a.UnwatchedEpisodeCount > 0)
                .OrderByDescending(a => a.WatchedDate)
                .ToList();
    }

    public ChangeTracker<int> GetChangeTracker(int userId)
    {
        return Changes.ContainsKey(userId) ? Changes[userId] : new ChangeTracker<int>();
    }
}
