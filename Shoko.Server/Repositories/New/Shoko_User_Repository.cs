using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Shoko.Server.Databases;
using Shoko.Server.Models.Internal;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class Shoko_User_Repository : BaseCachedRepository<Shoko_User, int>
{
    private static Logger logger = LogManager.GetCurrentClassLogger();

    protected override int SelectKey(Shoko_User entity)
    {
        return entity.Id;
    }

    public override void PopulateIndexes()
    {
    }

    public override void RegenerateDb()
    {
    }

    public override void Save(Shoko_User obj)
    {
        Save(obj, true);
    }

    public void Save(Shoko_User obj, bool updateGroupFilters)
    {
        var isNew = false;
        if (obj.Id == 0)
        {
            isNew = true;
            base.Save(obj);
        }

        if (updateGroupFilters)
        {
            Shoko_User? old = null;
            if (!isNew)
            {
                lock (GlobalDBLock)
                {
                    using var session = DatabaseFactory.SessionFactory.OpenSession();
                    old = session.Get<Shoko_User>(obj.Id);
                }
            }

            updateGroupFilters = old == null || old.RestrictedTags.SetEquals(obj.RestrictedTags);
        }

        base.Save(obj);
        if (updateGroupFilters)
        {
            logger.Trace("Updating group filter stats by user from JMMUserRepository.Save: {0}", obj.Id);
            obj.UpdateGroupFilters();
        }
    }

    public IReadOnlyList<Shoko_User> GetAniDBUsers()
    {
        return ReadLock(() => Cache.Values.Where(a => a.IsAniDBUser).ToList());
    }

    public IReadOnlyList<Shoko_User> GetTraktUsers()
    {
        return ReadLock(() => Cache.Values.Where(a => a.IsTraktUser).ToList());
    }

    public Shoko_User? AuthenticateUser(string userName, string password)
    {
        password ??= string.Empty;
        var hashedPassword = Digest.Hash(password);
        return ReadLock(() => Cache.Values.FirstOrDefault(a =>
            a.Username.Equals(userName, StringComparison.InvariantCultureIgnoreCase) &&
            a.Password.Equals(hashedPassword)));
    }

    public bool RemoveUser(int userID, bool skipValidation = false)
    {
        var user = GetByID(userID);
        if (!skipValidation)
        {
            var allAdmins = GetAll().Where(a => a.IsAdmin).ToList();
            allAdmins.Remove(user);
            if (allAdmins.Count < 1)
            {
                return false;
            }
        }

        var toSave = RepoFactory.Shoko_Group_Filter.GetAll().AsParallel().Where(a => a.RemoveUser(userID)).ToList();
        RepoFactory.Shoko_Group_Filter.Save(toSave);

        RepoFactory.Shoko_Series_User.Delete(RepoFactory.Shoko_Series_User.GetByUserId(userID));
        RepoFactory.Shoko_Group_User.Delete(RepoFactory.Shoko_Group_User.GetByUserId(userID));
        RepoFactory.Shoko_Episode_User.Delete(RepoFactory.Shoko_Episode_User.GetByUserId(userID));
        RepoFactory.Shoko_Video_User.Delete(RepoFactory.Shoko_Video_User.GetByUserId(userID));

        Delete(user);
        return true;
    }
}
