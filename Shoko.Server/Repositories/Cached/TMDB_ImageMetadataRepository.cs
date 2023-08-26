using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server.TMDB;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class TMDB_ImageRepository : BaseCachedRepository<TMDB_Image, int>
{
    private PocoIndex<int, TMDB_Image, int?>? _tmdbMovieIDs;
    private PocoIndex<int, TMDB_Image, int?>? _tmdbEpisodeIDs;
    private PocoIndex<int, TMDB_Image, int?>? _tmdbSeasonIDs;
    private PocoIndex<int, TMDB_Image, int?>? _tmdbShowIDs;
    private PocoIndex<int, TMDB_Image, int?>? _tmdbCollectionIDs;
    private PocoIndex<int, TMDB_Image, ImageEntityType>? _tmdbTypes;
    private PocoIndex<int, TMDB_Image, (string filePath, ImageEntityType type)>? _tmdbRemoteFileNames;

    public IReadOnlyList<TMDB_Image> GetByTmdbMovieID(int? movieId)
        => ReadLock(() => _tmdbMovieIDs!.GetMultiple(movieId)) ?? new();

    public IReadOnlyList<TMDB_Image> GetByTmdbMovieIDAndType(int? movieId, ImageEntityType type)
        => ReadLock(() => _tmdbMovieIDs!.GetMultiple(movieId))?.Where(image => image.ImageType == type).ToList() ?? new();

    public IReadOnlyList<TMDB_Image> GetByTmdbEpisodeID(int? episodeId)
        => ReadLock(() => _tmdbEpisodeIDs!.GetMultiple(episodeId)) ?? new();

    public IReadOnlyList<TMDB_Image> GetByTmdbEpisodeIDAndType(int? episodeId, ImageEntityType type)
        => ReadLock(() => _tmdbEpisodeIDs!.GetMultiple(episodeId)).Where(image => image.ImageType == type).ToList();

    public IReadOnlyList<TMDB_Image> GetByTmdbSeasonID(int? seasonId)
        => ReadLock(() => _tmdbSeasonIDs!.GetMultiple(seasonId)) ?? new();

    public IReadOnlyList<TMDB_Image> GetByTmdbSeasonIDAndType(int? seasonId, ImageEntityType type)
        => ReadLock(() => _tmdbSeasonIDs!.GetMultiple(seasonId))?.Where(image => image.ImageType == type).ToList() ?? new();

    public IReadOnlyList<TMDB_Image> GetByTmdbShowID(int? showId)
        => ReadLock(() => _tmdbShowIDs!.GetMultiple(showId)) ?? new();

    public IReadOnlyList<TMDB_Image> GetByTmdbShowIDAndType(int? showId, ImageEntityType type)
        => ReadLock(() => _tmdbShowIDs!.GetMultiple(showId)).Where(image => image.ImageType == type).ToList();

    public IReadOnlyList<TMDB_Image> GetByTmdbCollectionID(int? collectionId)
        => ReadLock(() => _tmdbCollectionIDs!.GetMultiple(collectionId)) ?? new();

    public IReadOnlyList<TMDB_Image> GetByTmdbCollectionIDAndType(int? collectionId, ImageEntityType type)
        => ReadLock(() => _tmdbCollectionIDs!.GetMultiple(collectionId)).Where(image => image.ImageType == type).ToList();

    public IReadOnlyList<TMDB_Image> GetByType(ImageEntityType type)
        => ReadLock(() => _tmdbTypes!.GetMultiple(type)) ?? new();

    public IReadOnlyList<TMDB_Image> GetByForeignIDAndType(int? id, ForeignEntityType foreignType, ImageEntityType type)
        => foreignType switch
        {
            ForeignEntityType.Movie => GetByTmdbMovieIDAndType(id, type),
            ForeignEntityType.Episode => GetByTmdbEpisodeIDAndType(id, type),
            ForeignEntityType.Season => GetByTmdbSeasonIDAndType(id, type),
            ForeignEntityType.Show => GetByTmdbShowIDAndType(id, type),
            ForeignEntityType.Collection => GetByTmdbCollectionIDAndType(id, type),
            _ => new List<TMDB_Image>(),
        };

    public TMDB_Image? GetByRemoteFileNameAndType(string fileName, ImageEntityType type)
        => ReadLock(() => _tmdbRemoteFileNames!.GetOne((fileName, type)));

    public ILookup<int, TMDB_Image> GetByAnimeIDsAndType(int[] animeIds, ImageEntityType type)
    {
        return animeIds
            .SelectMany(animeId =>
                RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID(animeId).SelectMany(xref => GetByTmdbMovieIDAndType(xref.TmdbMovieID, type))
                .Concat(RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID(animeId).SelectMany(xref => GetByTmdbShowIDAndType(xref.TmdbShowID, type)))
                .Select(image => (AnimeID: animeId, Image: image))
            )
            .ToLookup(a => a.AnimeID, a => a.Image);
    }

    protected override int SelectKey(TMDB_Image entity)
        => entity.TMDB_ImageID;

    public override void PopulateIndexes()
    {
        _tmdbMovieIDs = new(Cache, a => a.TmdbMovieID);
        _tmdbEpisodeIDs = new(Cache, a => a.TmdbEpisodeID);
        _tmdbSeasonIDs = new(Cache, a => a.TmdbSeasonID);
        _tmdbShowIDs = new(Cache, a => a.TmdbShowID);
        _tmdbCollectionIDs = new(Cache, a => a.TmdbCollectionID);
        _tmdbTypes = new(Cache, a => a.ImageType);
        _tmdbRemoteFileNames = new(Cache, a => (a.RemoteFileName, a.ImageType));
    }

    public override void RegenerateDb()
    {
    }
}
