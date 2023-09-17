using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Models.CrossReference;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class CrossRef_AniDB_TMDB_MovieRepository : BaseCachedRepository<CrossRef_AniDB_TMDB_Movie, int>
{
    private PocoIndex<int, CrossRef_AniDB_TMDB_Movie, int>? _anidbAnimeIDs;
    private PocoIndex<int, CrossRef_AniDB_TMDB_Movie, int?>? _anidbEpisodeIDs;
    private PocoIndex<int, CrossRef_AniDB_TMDB_Movie, int>? _tmdbMovieIDs;

    public List<CrossRef_AniDB_TMDB_Movie> GetByAnidbAnimeID(int animeId)
        => ReadLock(() => _anidbAnimeIDs!.GetMultiple(animeId));

    public CrossRef_AniDB_TMDB_Movie? GetByAnidbAnimeAndTmdbMovieIDs(int animeId, int movieId)
        => GetByAnidbAnimeID(animeId).FirstOrDefault(xref => xref.TmdbMovieID == movieId);

    public List<CrossRef_AniDB_TMDB_Movie> GetByAnidbEpisodeID(int episodeId)
        => ReadLock(() => _anidbEpisodeIDs!.GetMultiple(episodeId));

    public CrossRef_AniDB_TMDB_Movie? GetByAnidbEpisodeAndTmdbMovieIDs(int episodeId, int movieId)
        => GetByAnidbEpisodeID(episodeId).FirstOrDefault(xref => xref.TmdbMovieID == movieId);

    public List<CrossRef_AniDB_TMDB_Movie> GetByTmdbMovieID(int movieId)
        => ReadLock(() => _tmdbMovieIDs!.GetMultiple(movieId));

    public List<CrossRef_AniDB_TMDB_Movie> GetMissingEpisodeLinks()
        => ReadLock(() => _anidbEpisodeIDs!.GetMultiple(null));

    protected override int SelectKey(CrossRef_AniDB_TMDB_Movie entity)
        => entity.CrossRef_AniDB_TMDB_MovieID;

    public override void PopulateIndexes()
    {
        _tmdbMovieIDs = new(Cache, a => a.TmdbMovieID);
        _anidbAnimeIDs = new(Cache, a => a.AnidbAnimeID);
        _anidbEpisodeIDs = new(Cache, a => a.AnidbEpisodeID);
    }

    public override void RegenerateDb()
    {
    }
}
