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

public class CR_AniDB_TMDB_Movie_Repository : BaseDirectRepository<CR_AniDB_TMDB_Movie, int>
{
    public IReadOnlyList<CR_AniDB_TMDB_Movie> GetByAnidbAnimeId(int anidbAnimeId)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var cr = session
                .CreateCriteria(typeof(CR_AniDB_TMDB_Movie))
                .Add(Restrictions.Eq("AnidbAnimeId", anidbAnimeId))
                .List<CR_AniDB_TMDB_Movie>()
                .ToList();
            return cr;
        }
    }

    public CR_AniDB_TMDB_Movie? GetByAnidbEpisodeId(int anidbEpisodeId)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var cr = session
                .CreateCriteria(typeof(CR_AniDB_TMDB_Movie))
                .Add(Restrictions.Eq("AnidbEpisodeId", anidbEpisodeId))
                .UniqueResult<CR_AniDB_TMDB_Movie>();
            return cr;
        }
    }

    public CR_AniDB_TMDB_Movie? GetByTmdbMovieId(int tmdbMovieId)
    {

        lock (GlobalDBLock)
        {

            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var cr = session
                .CreateCriteria(typeof(CR_AniDB_TMDB_Movie))
                .Add(Restrictions.Eq("TmdbMovieId", tmdbMovieId))
                .UniqueResult<CR_AniDB_TMDB_Movie>();
            return cr;
        }
    }
}
