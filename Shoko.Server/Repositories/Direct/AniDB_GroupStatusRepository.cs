﻿using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using NHibernate.Linq;
using NLog;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_GroupStatusRepository : BaseDirectRepository<AniDB_GroupStatus, int>
{
    public List<AniDB_GroupStatus> GetByAnimeID(int id)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var objs = session
                .CreateCriteria(typeof(AniDB_GroupStatus))
                .Add(Restrictions.Eq("AnimeID", id))
                .List<AniDB_GroupStatus>();

            return new List<AniDB_GroupStatus>(objs);
        }
    }

    public void DeleteForAnime(int animeid)
    {
        using var session = DatabaseFactory.SessionFactory.OpenSession();
        session.Query<AniDB_GroupStatus>().Where(a => a.AnimeID == animeid).Delete();
        AniDB_Anime.UpdateStatsByAnimeID(animeid);
    }
}
