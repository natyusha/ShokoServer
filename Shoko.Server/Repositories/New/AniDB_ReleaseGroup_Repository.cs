using System.Collections.Generic;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_ReleaseGroupRepository : BaseDirectRepository<AniDB_ReleaseGroup, int>
{
    public AniDB_ReleaseGroup GetByGroupID(int id)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var cr = session
                .CreateCriteria(typeof(AniDB_ReleaseGroup))
                .Add(Restrictions.Eq("GroupId", id))
                .UniqueResult<AniDB_ReleaseGroup>();
            return cr;
        }
    }

    public IList<string> GetAllReleaseGroups()
    {
        var query =
            @"SELECT g.GroupName
FROM AniDB_File a
INNER JOIN AniDB_ReleaseGroup g ON a.GroupID = g.GroupID
INNER JOIN CrossRef_File_Episode xref1 ON xref1.Hash = a.Hash
GROUP BY g.GroupName
ORDER BY count(DISTINCT xref1.AnimeID) DESC, g.GroupName ASC";

        IList<string> result;
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            result = session.CreateSQLQuery(query).List<string>();
        }

        if (result.Contains("raw/unknown"))
        {
            result.Remove("raw/unknown");
        }

        return result;
        }
}
