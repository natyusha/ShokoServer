using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server.TMDB;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class TMDB_ImageMetadataRepository : BaseCachedRepository<TMDB_ImageMetadata, int>
{
    private PocoIndex<int, TMDB_ImageMetadata, int?>? _tmdbMovieIDs;
    private PocoIndex<int, TMDB_ImageMetadata, int?>? _tmdbEpisodeIDs;
    private PocoIndex<int, TMDB_ImageMetadata, int?>? _tmdbSeasonIDs;
    private PocoIndex<int, TMDB_ImageMetadata, int?>? _tmdbShowIDs;
    private PocoIndex<int, TMDB_ImageMetadata, int?>? _tmdbCollectionIDs;
    private PocoIndex<int, TMDB_ImageMetadata, ImageEntityType>? _tmdbTypes;
    private PocoIndex<int, TMDB_ImageMetadata, (string filePath, ImageEntityType type)>? _tmdbRemoteFileNames;

    public IReadOnlyList<TMDB_ImageMetadata> GetByTmdbMovieID(int? movieId)
        => ReadLock(() => _tmdbMovieIDs!.GetMultiple(movieId)) ?? new();

    public IReadOnlyList<TMDB_ImageMetadata> GetByTmdbMovieIDAndType(int? movieId, ImageEntityType type)
        => ReadLock(() => _tmdbMovieIDs!.GetMultiple(movieId))?.Where(image => image.ImageType == type).ToList() ?? new();

    public IReadOnlyList<TMDB_ImageMetadata> GetByTmdbEpisodeID(int? episodeId)
        => ReadLock(() => _tmdbEpisodeIDs!.GetMultiple(episodeId)) ?? new();

    public IReadOnlyList<TMDB_ImageMetadata> GetByTmdbEpisodeIDAndType(int? episodeId, ImageEntityType type)
        => ReadLock(() => _tmdbEpisodeIDs!.GetMultiple(episodeId)).Where(image => image.ImageType == type).ToList();

    public IReadOnlyList<TMDB_ImageMetadata> GetByTmdbSeasonID(int? seasonId)
        => ReadLock(() => _tmdbSeasonIDs!.GetMultiple(seasonId)) ?? new();

    public IReadOnlyList<TMDB_ImageMetadata> GetByTmdbSeasonIDAndType(int? seasonId, ImageEntityType type)
        => ReadLock(() => _tmdbSeasonIDs!.GetMultiple(seasonId))?.Where(image => image.ImageType == type).ToList() ?? new();

    public IReadOnlyList<TMDB_ImageMetadata> GetByTmdbShowID(int? showId)
        => ReadLock(() => _tmdbShowIDs!.GetMultiple(showId)) ?? new();

    public IReadOnlyList<TMDB_ImageMetadata> GetByTmdbShowIDAndType(int? showId, ImageEntityType type)
        => ReadLock(() => _tmdbShowIDs!.GetMultiple(showId)).Where(image => image.ImageType == type).ToList();

    public IReadOnlyList<TMDB_ImageMetadata> GetByTmdbCollectionID(int? collectionId)
        => ReadLock(() => _tmdbCollectionIDs!.GetMultiple(collectionId)) ?? new();

    public IReadOnlyList<TMDB_ImageMetadata> GetByTmdbCollectionIDAndType(int? collectionId, ImageEntityType type)
        => ReadLock(() => _tmdbCollectionIDs!.GetMultiple(collectionId)).Where(image => image.ImageType == type).ToList();

    public IReadOnlyList<TMDB_ImageMetadata> GetByType(ImageEntityType type)
        => ReadLock(() => _tmdbTypes!.GetMultiple(type)) ?? new();

    public IReadOnlyList<TMDB_ImageMetadata> GetByForeignIDAndType(int? id, ForeignEntityType foreignType, ImageEntityType type)
        => foreignType switch
        {
            ForeignEntityType.Movie => GetByTmdbMovieIDAndType(id, type),
            ForeignEntityType.Episode => GetByTmdbEpisodeIDAndType(id, type),
            ForeignEntityType.Season => GetByTmdbSeasonIDAndType(id, type),
            ForeignEntityType.Show => GetByTmdbShowIDAndType(id, type),
            ForeignEntityType.Collection => GetByTmdbCollectionIDAndType(id, type),
            _ => new List<TMDB_ImageMetadata>(),
        };

    public TMDB_ImageMetadata? GetByRemoteFileNameAndType(string fileName, ImageEntityType type)
        => ReadLock(() => _tmdbRemoteFileNames!.GetOne((fileName, type)));

    public ILookup<int, TMDB_ImageMetadata> GetByAnimeIDsAndType(int[] animeIds, ImageEntityType type)
    {
        return animeIds
            .SelectMany(animeId =>
                RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID(animeId).SelectMany(xref => GetByTmdbMovieIDAndType(xref.TmdbMovieID, type))
                .Concat(RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID(animeId).SelectMany(xref => GetByTmdbShowIDAndType(xref.TmdbShowID, type)))
                .Select(image => (AnimeID: animeId, Image: image))
            )
            .ToLookup(a => a.AnimeID, a => a.Image);
    }

    protected override int SelectKey(TMDB_ImageMetadata entity)
        => entity.TMDB_ImageMetadataID;

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
