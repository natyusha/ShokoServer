using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate;
using Shoko.Commons.Collections;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct;

public class MovieDB_FanartRepository : BaseDirectRepository<MovieDB_Fanart, int>
{
    public MovieDB_Fanart GetByOnlineID(string url)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.Query<MovieDB_Fanart>().Where(a => a.URL == url).Take(1).SingleOrDefault();
        });
    }

    public List<MovieDB_Fanart> GetByMovieID(int id)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.Query<MovieDB_Fanart>().Where(a => a.MovieId == id).ToList();
        });
    }

    public ILookup<int, MovieDB_Fanart> GetByAnimeIDs(ISessionWrapper session, int[] animeIds)
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
            return EmptyLookup<int, MovieDB_Fanart>.Instance;
        }

        return Lock(() =>
        {
            var fanartByAnime = session.CreateSQLQuery(
                    @"
                    SELECT DISTINCT cr.AnidbAnimeID, {mdbFanart.*}
                    FROM CrossRef_AniDB_TMDB_Movie AS cr
                        INNER JOIN MovieDB_Fanart AS mdbFanart
                            ON mdbFanart.MovieId = cr.TmdbMovieID
                    WHERE cr.AnidbAnimeID IN (:animeIds)"
                )
                .AddScalar("AnimeID", NHibernateUtil.Int32)
                .AddEntity("mdbFanart", typeof(MovieDB_Fanart))
                .SetParameterList("animeIds", animeIds)
                .List<object[]>()
                .ToLookup(r => (int)r[0], r => (MovieDB_Fanart)r[1]);

            return fanartByAnime;
        });
    }

    public List<MovieDB_Fanart> GetAllOriginal()
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session
                .Query<MovieDB_Fanart>()
                .Where(a => a.ImageSize == Shoko.Models.Constants.MovieDBImageSize.Original)
                .ToList();
        });
    }
}
