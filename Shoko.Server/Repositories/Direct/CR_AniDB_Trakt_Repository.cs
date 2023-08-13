using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models.CrossReferences;

namespace Shoko.Server.Repositories.Cached;

public class CR_AniDB_Trakt_Repository : BaseCachedRepository<CR_AniDB_Trakt, int>
{
    private PocoIndex<int, CR_AniDB_Trakt, int> AnimeIDs;

    protected override int SelectKey(CR_AniDB_Trakt entity)
    {
        return entity.Id;
    }

    public override void PopulateIndexes()
    {
        AnimeIDs = new(Cache, a => a.AnimeID);
    }

    public override void RegenerateDb()
    {
    }

    public List<CR_AniDB_Trakt> GetByAnimeID(int id)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return GetByAnimeID(session, id);
        }
    }

    public List<CR_AniDB_Trakt> GetByAnimeID(ISession session, int id)
    {
        lock (GlobalDBLock)
        {
            var xrefs = session
                .CreateCriteria(typeof(CR_AniDB_Trakt))
                .Add(Restrictions.Eq("AnimeID", id))
                .AddOrder(Order.Asc("AniDBStartEpisodeType"))
                .AddOrder(Order.Asc("AniDBStartEpisodeNumber"))
                .List<CR_AniDB_Trakt>();

            return new List<CR_AniDB_Trakt>(xrefs);
        }
    }

    public List<CR_AniDB_Trakt> GetByAnimeIDEpTypeEpNumber(ISession session, int id, int aniEpType,
        int aniEpisodeNumber)
    {
        lock (GlobalDBLock)
        {
            var xrefs = session
                .CreateCriteria(typeof(CR_AniDB_Trakt))
                .Add(Restrictions.Eq("AnimeID", id))
                .Add(Restrictions.Eq("AniDBStartEpisodeType", aniEpType))
                .Add(Restrictions.Eq("AniDBStartEpisodeNumber", aniEpisodeNumber))
                .List<CR_AniDB_Trakt>();

            return new List<CR_AniDB_Trakt>(xrefs);
        }
    }

    public CR_AniDB_Trakt GetByTraktID(ISession session, string id, int season, int episodeNumber,
        int animeID,
        int aniEpType, int aniEpisodeNumber)
    {
        lock (GlobalDBLock)
        {
            var cr = session
                .CreateCriteria(typeof(CR_AniDB_Trakt))
                .Add(Restrictions.Eq("TraktID", id))
                .Add(Restrictions.Eq("TraktSeasonNumber", season))
                .Add(Restrictions.Eq("TraktStartEpisodeNumber", episodeNumber))
                .Add(Restrictions.Eq("AnimeID", animeID))
                .Add(Restrictions.Eq("AniDBStartEpisodeType", aniEpType))
                .Add(Restrictions.Eq("AniDBStartEpisodeNumber", aniEpisodeNumber))
                .UniqueResult<CR_AniDB_Trakt>();
            return cr;
        }
    }

    public CR_AniDB_Trakt GetByTraktID(string id, int season, int episodeNumber, int animeID, int aniEpType,
        int aniEpisodeNumber)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return GetByTraktID(session, id, season, episodeNumber, animeID, aniEpType, aniEpisodeNumber);
        }
    }

    public List<CR_AniDB_Trakt> GetByTraktID(string traktID)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var xrefs = session
                .CreateCriteria(typeof(CR_AniDB_Trakt))
                .Add(Restrictions.Eq("TraktID", traktID))
                .List<CR_AniDB_Trakt>();

            return new List<CR_AniDB_Trakt>(xrefs);
        }
    }

    internal ILookup<int, CR_AniDB_Trakt> GetByAnimeIDs(IReadOnlyCollection<int> animeIds)
    {
        if (animeIds == null)
        {
            throw new ArgumentNullException(nameof(animeIds));
        }

        if (animeIds.Count == 0)
        {
            return EmptyLookup<int, CR_AniDB_Trakt>.Instance;
        }

        return ReadLock(() => animeIds.SelectMany(id => AnimeIDs.GetMultiple(id))
            .ToLookup(xref => xref.AnimeID));
    }
}
