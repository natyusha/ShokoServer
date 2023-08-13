using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models.CrossReferences;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories.NHibernate;

#nullable enable
namespace Shoko.Server.Repositories.Direct;

public class TMDB_Show_Repository : BaseDirectRepository<TMDB_Show, int>
{
    public TMDB_Show? GetByShowId(int id)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return GetByShowId(session.Wrap(), id);
        }
    }

    public TMDB_Show? GetByShowId(ISessionWrapper session, int id)
    {
        lock (GlobalDBLock)
        {
            var cr = session
                .CreateCriteria(typeof(TMDB_Show))
                .Add(Restrictions.Eq("ShowId", id))
                .UniqueResult<TMDB_Show>();
            return cr;
        }
    }

    public Dictionary<int, List<Tuple<CR_AniDB_TMDB_Show, TMDB_Show>>> GetByAnimeIDs(ISessionWrapper session,
        int[] animeIds)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (animeIds == null)
        {
            throw new ArgumentNullException(nameof(animeIds));
        }

        if (animeIds.Length == 0)
        {
            return new();
        }

        lock (GlobalDBLock)
        {
            var showByAnime = session.CreateSQLQuery(
                    @"
                SELECT {cr.*}, {show.*}
                    FROM CR_AniDB_TMDB_Show cr
                        INNER JOIN TMDB_Show show
                            ON show.ShowId = cr.TmdbShowId
                    WHERE cr.AnidbAnimeId IN (:animeIds)"
                )
                .AddEntity("cr", typeof(CR_AniDB_TMDB_Show))
                .AddEntity("show", typeof(TMDB_Show))
                .SetParameterList("animeIds", animeIds)
                .List<object[]>()
                .GroupBy(r => ((CR_AniDB_TMDB_Show)r[0]).AnidbAnimeId)
                .ToDictionary(
                    r => r.Key,
                    y => y.Select(r => new Tuple<CR_AniDB_TMDB_Show, TMDB_Show>((CR_AniDB_TMDB_Show)r[0], (TMDB_Show)r[1])).ToList()
                );

            return showByAnime;
        }
    }
}
