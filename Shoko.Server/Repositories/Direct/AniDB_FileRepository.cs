using System.Collections.Generic;
using System.Linq;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Repositories;

public class AniDB_FileRepository : BaseCachedRepository<AniDB_File, int>
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private PocoIndex<int, AniDB_File, string> Hashes;
    private PocoIndex<int, AniDB_File, int> FileIds;
    private PocoIndex<int, AniDB_File, int> InternalVersions;

    protected override int SelectKey(AniDB_File entity)
    {
        return entity.Id;
    }

    public override void PopulateIndexes()
    {
        // Only populated from main thread before these are accessible, so no lock
        Hashes = new PocoIndex<int, AniDB_File, string>(Cache, a => a.ED2K);
        FileIds = new PocoIndex<int, AniDB_File, int>(Cache, a => a.AnidbFileId);
        InternalVersions = new PocoIndex<int, AniDB_File, int>(Cache, a => a.InternalVersion);
    }

    public override void RegenerateDb()
    {
    }

    public override void Save(AniDB_File obj)
    {
        Save(obj, true);
    }

    public void Save(AniDB_File obj, bool updateStats)
    {
        base.Save(obj);
        if (!updateStats)
        {
            return;
        }

        Logger.Trace("Updating group stats by file from AniDB_FileRepository.Save: {Hash}", obj.ED2K);
        var anime = RepoFactory.CR_Video_Episode.GetByED2K(obj.ED2K).Select(a => a.AnidbAnimeId).Distinct();
        anime.ForEach(AniDB_Anime.UpdateStatsByAnimeID);
    }


    public AniDB_File GetByED2K(string hash)
    {
        return ReadLock(() => Hashes.GetOne(hash));
    }

    public List<AniDB_File> GetByInternalVersion(int version)
    {
        return ReadLock(() => InternalVersions.GetMultiple(version));
    }

    public AniDB_File GetByHashAndFileSize(string hash, long fsize)
    {
        var list = ReadLock(() => Hashes.GetMultiple(hash));
        return list.Count == 1 ? list.FirstOrDefault() : list.FirstOrDefault(a => a.FileSize == fsize);
    }

    public AniDB_File GetByFileID(int fileID)
    {
        return ReadLock(() => FileIds.GetOne(fileID));
    }
}
