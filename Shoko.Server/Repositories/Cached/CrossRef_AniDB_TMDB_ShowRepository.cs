using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Server.Models.CrossReference;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class CrossRef_AniDB_TMDB_ShowRepository : BaseCachedRepository<CrossRef_AniDB_TMDB_Show, int>
{
    private PocoIndex<int, CrossRef_AniDB_TMDB_Show, int>? _anidbAnimeIDs;
    private PocoIndex<int, CrossRef_AniDB_TMDB_Show, int>? _tmdbShowIDs;
    private PocoIndex<int, CrossRef_AniDB_TMDB_Show, (int, int)>? _pairedIDs;

    public List<CrossRef_AniDB_TMDB_Show> GetByAnidbAnimeID(int animeId)
        => ReadLock(() => _anidbAnimeIDs!.GetMultiple(animeId));

    public List<CrossRef_AniDB_TMDB_Show> GetByTmdbShowID(int episodeId)
        => ReadLock(() => _tmdbShowIDs!.GetMultiple(episodeId));

    public CrossRef_AniDB_TMDB_Show? GetByAnidbAnimeAndTmdbShowIDs(int anidbId, int tmdbId)
        => ReadLock(() => _pairedIDs!.GetOne((anidbId, tmdbId)));

    /// <summary>
    /// Gets other cross references by anime ID.
    /// </summary>
    /// <param name="animeIds">An optional list of anime IDs whose cross references are to be retrieved.
    /// Can be <c>null</c> to get cross references for ALL anime.</param>
    /// <returns>A <see cref="ILookup{TKey,TElement}"/> that maps anime ID to their associated other cross references.</returns>
    public ILookup<int, CrossRef_AniDB_TMDB_Show> GetByAnimeIDsAndType(IReadOnlyCollection<int> animeIds)
    {
        if (animeIds == null || animeIds?.Count == 0)
            return EmptyLookup<int, CrossRef_AniDB_TMDB_Show>.Instance;

        return Lock(
            () => animeIds!.SelectMany(animeId => _anidbAnimeIDs!.GetMultiple(animeId)).ToLookup(xref => xref.AnidbAnimeID)
        );
    }

    protected override int SelectKey(CrossRef_AniDB_TMDB_Show entity)
        => entity.CrossRef_AniDB_TMDB_ShowID;

    public override void PopulateIndexes()
    {
        _tmdbShowIDs = new(Cache, a => a.TmdbShowID);
        _anidbAnimeIDs = new(Cache, a => a.AnidbAnimeID);
        _pairedIDs = new(Cache, a => (a.AnidbAnimeID, a.TmdbShowID));
    }

    public override void RegenerateDb()
    {
    }
}
