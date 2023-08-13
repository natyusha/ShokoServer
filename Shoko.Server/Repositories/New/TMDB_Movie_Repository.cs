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

public class TMDB_Movie_Repository : BaseDirectRepository<TMDB_Movie, int>
{
    public TMDB_Movie? GetByMovieId(int movieId)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return GetByMovieId(session.Wrap(), movieId);
        }
    }

    public TMDB_Movie? GetByMovieId(ISessionWrapper session, int movieId)
    {
        lock (GlobalDBLock)
        {
            var cr = session
                .CreateCriteria(typeof(TMDB_Movie))
                .Add(Restrictions.Eq("MovieId", movieId))
                .UniqueResult<TMDB_Movie>();
            return cr;
        }
    }

    public Dictionary<int, List<Tuple<CR_AniDB_TMDB_Movie, TMDB_Movie>>> GetByAnimeIDs(ISessionWrapper session,
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
            var movieByAnime = session.CreateSQLQuery(
                    @"
                SELECT {cr.*}, {movie.*}
                    FROM CR_AniDB_TMDB_Movie cr
                        INNER JOIN TMDB_Movie movie
                            ON movie.MovieId = cr.TmdbMovieId
                    WHERE cr.AnidbAnimeId IN (:animeIds)"
                )
                .AddEntity("cr", typeof(CR_AniDB_TMDB_Movie))
                .AddEntity("movie", typeof(TMDB_Movie))
                .SetParameterList("animeIds", animeIds)
                .List<object[]>()
                .GroupBy(r => ((CR_AniDB_TMDB_Movie)r[0]).AnidbAnimeId)
                .ToDictionary(
                    r => r.Key,
                    y => y.Select(r => new Tuple<CR_AniDB_TMDB_Movie, TMDB_Movie>((CR_AniDB_TMDB_Movie)r[0], (TMDB_Movie)r[1])).ToList()
                );

            return movieByAnime;
        }
    }
}
