using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.Repositories.Direct;

public class TMDB_AlternateOrdering_SeasonRepository : BaseDirectRepository<TMDB_AlternateOrdering_Season, int>
{
    public IReadOnlyList<TMDB_AlternateOrdering_Season> GetByTmdbShowID(int showId)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_AlternateOrdering_Season>()
                .Where(a => a.TmdbShowID == showId)
                .ToList();
        });
    }

    public IReadOnlyList<TMDB_AlternateOrdering_Season> GetByTmdbEpisodeGroupCollectionID(string collectionId)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_AlternateOrdering_Season>()
                .Where(a => a.TmdbEpisodeGroupCollectionID == collectionId)
                .ToList();
        });
    }

    public TMDB_AlternateOrdering_Season? GetByTmdbEpisodeGroupID(string groupId)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_AlternateOrdering_Season>()
                .Where(a => a.TmdbEpisodeGroupID == groupId)
                .Take(1)
                .SingleOrDefault();
        });
    }
}
