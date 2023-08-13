using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using Shoko.Commons.Collections;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models.CrossReferences;
using Shoko.Server.Repositories.NHibernate;

#nullable enable
namespace Shoko.Server.Repositories.Direct;

public class CR_AniDB_TMDB_Show_Repository : BaseDirectRepository<CR_AniDB_TMDB_Show, int>
{
    public IReadOnlyList<CR_AniDB_TMDB_Show> GetByAnidbAnimeId(int anidbAnimeId)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var cr = session
                .CreateCriteria(typeof(CR_AniDB_TMDB_Show))
                .Add(Restrictions.Eq("AnidbAnimeId", anidbAnimeId))
                .List<CR_AniDB_TMDB_Show>()
                .ToList();
            return cr;
        }
    }

    public IReadOnlyList<CR_AniDB_TMDB_Show> GetByTmdbMovieId(int tmdbMovieId)
    {

        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var cr = session
                .CreateCriteria(typeof(CR_AniDB_TMDB_Show))
                .Add(Restrictions.Eq("TmdbMovieId", tmdbMovieId))
                .List<CR_AniDB_TMDB_Show>()
                .ToList();
            return cr;
        }
    }
}
