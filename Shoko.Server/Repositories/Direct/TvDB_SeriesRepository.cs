using System;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Server.Models.TvDB;
using Shoko.Server.Models.CrossReferences;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Cached;

public class TvDB_SeriesRepository : BaseCachedRepository<TvDB_Show, int>
{
    private PocoIndex<int, TvDB_Show, int> TvDBIDs;

    public override void PopulateIndexes()
    {
        TvDBIDs = new PocoIndex<int, TvDB_Show, int>(Cache, a => a.Id);
    }

    protected override int SelectKey(TvDB_Show entity)
    {
        return entity.Id;
    }

    public TvDB_Show GetByShowId(int id)
    {
        return ReadLock(() => TvDBIDs.GetOne(id));
    }

    public ILookup<int, Tuple<CR_AniDB_TvDB, TvDB_Show>> GetByAnimeIDs(ISessionWrapper session,
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
            return EmptyLookup<int, Tuple<CR_AniDB_TvDB, TvDB_Show>>.Instance;
        }

        lock (GlobalDBLock)
        {
            var tvDbSeriesByAnime = session.CreateSQLQuery(@"
                SELECT {cr.*}, {series.*}
                    FROM CrossRef_AniDB_TvDB cr
                        INNER JOIN TvDB_Series series
                            ON series.SeriesID = cr.TvDBID
                    WHERE cr.AniDBID IN (:animeIds)")
                .AddEntity("cr", typeof(CR_AniDB_TvDB))
                .AddEntity("series", typeof(TvDB_Show))
                .SetParameterList("animeIds", animeIds)
                .List<object[]>()
                .ToLookup(r => ((CR_AniDB_TvDB)r[0]).AnidbAnimeId,
                    r => new Tuple<CR_AniDB_TvDB, TvDB_Show>((CR_AniDB_TvDB)r[0],
                        (TvDB_Show)r[1]));

            return tvDbSeriesByAnime;
        }
    }

    public override void RegenerateDb()
    {
    }
}
