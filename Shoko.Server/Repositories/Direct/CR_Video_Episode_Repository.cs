using System.Collections.Generic;
using System.Linq;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Server.Models;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReferences;

#nullable enable
namespace Shoko.Server.Repositories;

public class CR_Video_Episode_Repository : BaseCachedRepository<CR_Video_Episode, int>
{
    private static Logger logger = LogManager.GetCurrentClassLogger();
    private PocoIndex<int, CR_Video_Episode, string>? ED2KIndex;
    private PocoIndex<int, CR_Video_Episode, int>? AnimeIndex;
    private PocoIndex<int, CR_Video_Episode, int>? EpisodeIndex;
    private PocoIndex<int, CR_Video_Episode, string>? FilenameIndex;

    public override void PopulateIndexes()
    {
        ED2KIndex = new PocoIndex<int, CR_Video_Episode, string>(Cache, a => a.ED2K);
        AnimeIndex = new PocoIndex<int, CR_Video_Episode, int>(Cache, a => a.AnidbAnimeId);
        EpisodeIndex = new PocoIndex<int, CR_Video_Episode, int>(Cache, a => a.AnidbEpisodeId);
        FilenameIndex = new PocoIndex<int, CR_Video_Episode, string>(Cache, a => a.FileName);
    }

    public override void RegenerateDb()
    {
    }

    public CR_Video_Episode_Repository() : base()
    {
        EndSaveCallback = obj =>
        {
            AniDB_Anime.UpdateStatsByAnimeID(obj.AnidbAnimeId);
        };
        EndDeleteCallback = obj =>
        {
            if (obj == null || obj.AnidbAnimeId <= 0)
            {
                return;
            }

            logger.Trace("Updating group stats by anime from CrossRef_File_EpisodeRepository.Delete: {0}",
                obj.AnidbAnimeId);
            AniDB_Anime.UpdateStatsByAnimeID(obj.AnidbAnimeId);
        };
    }

    protected override int SelectKey(CR_Video_Episode entity)
    {
        return entity.Id;
    }

    public IReadOnlyList<CR_Video_Episode> GetByED2K(string hash, bool resolve = false)
    {
        var list = ReadLock(() => ED2KIndex!.GetMultiple(hash).OrderBy(a => a.Order).ToList());
        if (!resolve)
            return list;

        return list
            .Where(xref => xref.Episode != null && xref.Video != null && xref.Series != null)
            .ToList();
    }

    public IReadOnlyList<CR_Video_Episode> GetByAnidbAnimeId(int anidbAnimeId)
    {
        return ReadLock(() => AnimeIndex!.GetMultiple(anidbAnimeId));
    }

    public IReadOnlyList<CR_Video_Episode> GetByFileNameAndSize(string fileName, long fileSize)
    {
        return ReadLock(() => FilenameIndex!.GetMultiple(fileName).Where(a => a.FileSize == fileSize).ToList());
    }

    /// <summary>
    /// This is the only way to uniquely identify the record other than the IDENTITY
    /// </summary>
    /// <param name="ed2k"></param>
    /// <param name="episodeId"></param>
    /// <returns></returns>
    public CR_Video_Episode? GetByED2KAndAnidbEpisodeId(string ed2k, int episodeId)
    {
        return ReadLock(() => ED2KIndex!.GetMultiple(ed2k).FirstOrDefault(a => a.AnidbEpisodeId == episodeId));
    }

    public IReadOnlyList<CR_Video_Episode> GetByAniDBEpisodeId(int episodeId, bool resolve = false)
    {
        var list = ReadLock(() => EpisodeIndex!.GetMultiple(episodeId));
        if (!resolve)
            return list;

        return list
            .Where(xref => xref.Episode != null && xref.Video != null && xref.Series != null)
            .ToList();
    }
}
