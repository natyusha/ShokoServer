using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models.Internal;
using Shoko.Server.Repositories.NHibernate;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class ShokoGroup_UserRepository : BaseCachedRepository<ShokoGroup_User, int>
{
    private PocoIndex<int, ShokoGroup_User, int>? Groups;
    private PocoIndex<int, ShokoGroup_User, int>? Users;
    private PocoIndex<int, ShokoGroup_User, int, int>? UsersGroups;
    private Dictionary<int, ChangeTracker<int>> Changes = new();


    public ShokoGroup_UserRepository()
    {
        EndDeleteCallback = userRecord =>
        {
            if (!Changes.ContainsKey(userRecord.UserId))
            {
                Changes[userRecord.UserId] = new ChangeTracker<int>();
            }

            Changes[userRecord.UserId].Remove(userRecord.GroupId);

            userRecord.DeleteFromFilters();
        };
    }

    protected override int SelectKey(ShokoGroup_User entity) =>
        entity.Id;

    public override void PopulateIndexes()
    {
        Groups = Cache.CreateIndex(a => a.GroupId);
        Users = Cache.CreateIndex(a => a.UserId);
        UsersGroups = Cache.CreateIndex(a => a.UserId, a => a.GroupId);

        foreach (var n in Cache.Values.Select(a => a.UserId).Distinct())
        {
            Changes[n] = new ChangeTracker<int>();
            Changes[n].AddOrUpdateRange(Users.GetMultiple(n).Select(a => a.GroupId));
        }
    }

    public override void RegenerateDb()
    {
    }

    public override void Save(ShokoGroup_User obj)
    {
        // Get The previous AnimeGroup_User from db for comparison;
        ShokoGroup_User? old = null;
        lock (GlobalDBLock)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                old = session.Get<ShokoGroup_User>(obj.Id);
            }
        }
        var changedTypes = obj.GetConditionTypesChanged(old);

        base.Save(obj);

        if (!Changes.ContainsKey(obj.UserId))
            Changes[obj.UserId] = new ChangeTracker<int>();
        Changes[obj.UserId].AddOrUpdate(obj.GroupId);

        obj.UpdateGroupFilters(changedTypes);
    }


    /// <summary>
    /// Inserts a batch of <see cref="ShokoGroup_User"/> into the database.
    /// </summary>
    /// <remarks>
    /// <para>This method should NOT be used for updating existing entities.</para>
    /// <para>It is up to the caller of this method to manage transactions, etc.</para>
    /// <para>Group Filters, etc. will not be updated by this method.</para>
    /// </remarks>
    /// <param name="session">The NHibernate session.</param>
    /// <param name="userRecords">The batch of <see cref="ShokoGroup_User"/> to insert into the database.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="userRecords"/> is <c>null</c>.</exception>
    public void InsertBatch(ISessionWrapper session, IEnumerable<ShokoGroup_User> userRecords)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (userRecords == null)
        {
            throw new ArgumentNullException(nameof(userRecords));
        }

        using var trans = session.BeginTransaction();
        foreach (var groupUser in userRecords)
        {
            lock (GlobalDBLock) session.Insert(groupUser);

            UpdateCache(groupUser);
            if (!Changes.TryGetValue(groupUser.UserId, out var changeTracker))
            {
                changeTracker = new ChangeTracker<int>();
                Changes[groupUser.UserId] = changeTracker;
            }

            changeTracker.AddOrUpdate(groupUser.GroupId);
        }
        trans.Commit();
    }

    /// <summary>
    /// Inserts a batch of <see cref="ShokoGroup_User"/> into the database.
    /// </summary>
    /// <remarks>
    /// <para>It is up to the caller of this method to manage transactions, etc.</para>
    /// <para>Group Filters, etc. will not be updated by this method.</para>
    /// </remarks>
    /// <param name="session">The NHibernate session.</param>
    /// <param name="userRecords">The batch of <see cref="ShokoGroup_User"/> to insert into the database.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="userRecords"/> is <c>null</c>.</exception>
    public void UpdateBatch(ISessionWrapper session, IEnumerable<ShokoGroup_User> userRecords)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (userRecords == null)
        {
            throw new ArgumentNullException(nameof(userRecords));
        }

        using var trans = session.BeginTransaction();
        foreach (var groupUser in userRecords)
        {
            lock (GlobalDBLock) session.Update(groupUser);
            UpdateCache(groupUser);

            if (!Changes.TryGetValue(groupUser.UserId, out var changeTracker))
            {
                changeTracker = new ChangeTracker<int>();
                Changes[groupUser.UserId] = changeTracker;
            }

            changeTracker.AddOrUpdate(groupUser.GroupId);
        }
        trans.Commit();
    }

    /// <summary>
    /// Deletes all AnimeGroup_User records.
    /// </summary>
    /// <remarks>
    /// This method also makes sure that the cache is cleared.
    /// </remarks>
    /// <param name="session">The NHibernate session.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <c>null</c>.</exception>
    public void DeleteAll(ISessionWrapper session)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        // First, get all of the current user/groups so that we can inform the change tracker that they have been removed later
        var usrGrpMap = GetAll().GroupBy(g => g.UserId, g => g.GroupId);

        lock (GlobalDBLock)
        {
            // Then, actually delete the AnimeGroup_Users
            session.CreateQuery("delete ShokoGroup_User agu").ExecuteUpdate();
        }

        // Now, update the change trackers with all removed records
        foreach (var grp in usrGrpMap)
        {
            var jmmUserId = grp.Key;

            if (!Changes.TryGetValue(jmmUserId, out var changeTracker))
            {
                changeTracker = new ChangeTracker<int>();
                Changes[jmmUserId] = changeTracker;
            }

            changeTracker.RemoveRange(grp);
        }

        // Finally, we need to clear the cache so that it is in sync with the database
        ClearCache();
    }

    public ShokoGroup_User GetByUserAndGroupIds(int userId, int groupId)
    {
        return ReadLock(() => UsersGroups!.GetOne(userId, groupId));
    }

    public List<ShokoGroup_User> GetByUserId(int userId)
    {
        return ReadLock(() => Users!.GetMultiple(userId));
    }

    public List<ShokoGroup_User> GetByGroupId(int groupId)
    {
        return ReadLock(() => Groups!.GetMultiple(groupId));
    }

    public ChangeTracker<int> GetChangeTracker(int userId)
    {
        return ReadLock(() => Changes.ContainsKey(userId) ? Changes[userId] : new ChangeTracker<int>());
    }
}
