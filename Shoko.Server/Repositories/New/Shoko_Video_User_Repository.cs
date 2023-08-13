using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.Internal;

#nullable enable
namespace Shoko.Server.Repositories.Direct;

public class Shoko_Video_User_Repository : BaseDirectRepository<Shoko_Video_User, int>
{
    public IReadOnlyList<Shoko_Video_User> GetByVideoId(int videoId)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var cr = session
                .CreateCriteria(typeof(Shoko_Video_User))
                .Add(Restrictions.Eq("VideoLocalID", videoId))
                .List<Shoko_Video_User>()
                .ToList();
            return cr;
        }
    }

    public IReadOnlyList<Shoko_Video_User> GetByUserId(int userId)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var cr = session
                .CreateCriteria(typeof(Shoko_Video_User))
                .Add(Restrictions.Eq("JMMUserID", userId))
                .List<Shoko_Video_User>()
                .ToList();
            return cr;
        }
    }

    public Shoko_Video_User? GetByUserAndVideoIds(int userId, int videoId)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var cr = session
                .CreateCriteria(typeof(Shoko_Video_User))
                .Add(Restrictions.Eq("VideoLocalID", videoId))
                .Add(Restrictions.Eq("JMMUserID", userId))
                .UniqueResult<Shoko_Video_User>();
            return cr;
        }
    }
}
