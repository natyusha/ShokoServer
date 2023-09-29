using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.Repositories.Direct;

public class TMDB_SeasonRepository : BaseDirectRepository<TMDB_Season, int>
{
    public IReadOnlyList<TMDB_Season> GetByTmdbShowID(int tmdbShowId)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Season>()
                .Where(a => a.TmdbShowID == tmdbShowId)
                .ToList();
        });
    }

    public TMDB_Season? GetByTmdbSeasonID(int tmdbSeasonId)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Season>()
                .Where(a => a.TmdbSeasonID == tmdbSeasonId)
                .Take(1)
                .SingleOrDefault();
        });
    }
}
