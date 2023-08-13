using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Commons.Properties;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Repositories;

public class AniDB_Anime_TitleRepository : BaseCachedRepository<AniDB_AnimeTitle, int>
{
    private PocoIndex<int, AniDB_AnimeTitle, int>? Animes;

    public override void PopulateIndexes()
    {
        Animes = new PocoIndex<int, AniDB_AnimeTitle, int>(Cache, a => a.AnimeId);
    }

    protected override int SelectKey(AniDB_AnimeTitle entity)
    {
        return entity.Id;
    }

    public override void RegenerateDb()
    {
        // Don't need lock in init
        ServerState.Instance.ServerStartingStatus = string.Format(
            Resources.Database_Validating, typeof(AniDB_AnimeTitle).Name, " DbRegen");
        var titles = Cache.Values.Where(title => title.Value.Contains('`')).ToList();
        foreach (var title in titles)
        {
            title.Value = title.Value.Replace('`', '\'');
            Save(title);
        }
    }

    public IReadOnlyList<AniDB_AnimeTitle> GetByAnimeId(int animeId)
    {
        return ReadLock(() => Animes!.GetMultiple(animeId));
    }

    public AniDB_AnimeTitle? GetByAnimeTitleAndValue(int animeId, string value)
    {
        return GetByAnimeId(animeId).FirstOrDefault(title => string.Equals(title.Value, value));
    }

    public ILookup<int, AniDB_AnimeTitle> GetByAnimeIDs(ISessionWrapper session, ICollection<int> ids)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (ids == null)
        {
            throw new ArgumentNullException(nameof(ids));
        }

        if (ids.Count == 0)
        {
            return EmptyLookup<int, AniDB_AnimeTitle>.Instance;
        }

        lock (GlobalDBLock)
        {
            var titles = session.CreateCriteria<AniDB_AnimeTitle>()
                .Add(Restrictions.InG(nameof(AniDB_AnimeTitle.AnimeId), ids))
                .List<AniDB_AnimeTitle>()
                .ToLookup(t => t.AnimeId);

            return titles;
        }
    }
}
