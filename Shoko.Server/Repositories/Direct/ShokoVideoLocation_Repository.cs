using System.Collections.Generic;
using System.IO;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Server.Databases;
using Shoko.Server.Models.Internal;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class ShokoVideoLocation_Repository : BaseCachedRepository<Shoko_Video_Location, int>
{
    private PocoIndex<int, Shoko_Video_Location, int>? VideoIndex;
    private PocoIndex<int, Shoko_Video_Location, int>? ImportFolderIndex;
    private PocoIndex<int, Shoko_Video_Location, string>? RelativePathIndex;

    protected override int SelectKey(Shoko_Video_Location entity)
    {
        return entity.Id;
    }

    public override void PopulateIndexes()
    {
        VideoIndex = new PocoIndex<int, Shoko_Video_Location, int>(Cache, a => a.VideoId);
        ImportFolderIndex = new PocoIndex<int, Shoko_Video_Location, int>(Cache, a => a.ImportFolderId);
        RelativePathIndex = new PocoIndex<int, Shoko_Video_Location, string>(Cache, a => a.RelativePath);
    }

    public override void RegenerateDb()
    {
        ServerState.Instance.ServerStartingStatus = string.Format(
            Resources.Database_Validating, nameof(Shoko_Video_Location), "Removing orphaned ShokoVideoLocation");
        var count = 0;
        int max;

        var list = Cache.Values.Where(a => a is { VideoId: 0 }).ToList();
        max = list.Count;

        using var session = DatabaseFactory.SessionFactory.OpenSession();
        foreach (var batch in list.Batch(50))
        {
            using var transaction = session.BeginTransaction();
            foreach (var a in batch)
            {
                DeleteWithOpenTransaction(session, a);
                count++;
                ServerState.Instance.ServerStartingStatus = string.Format(Resources.Database_Validating, nameof(Shoko_Video_Location),
                    " Removing Orphaned ShokoVideoLocation - " + count + "/" + max);
            }

            transaction.Commit();
        }
    }

    public List<Shoko_Video_Location> GetByImportFolderId(int importFolderId, bool resolve = false)
    {
        var list = ReadLock(() => ImportFolderIndex!.GetMultiple(importFolderId));
        if (!resolve)
            return list
                .OrderBy(location => location.RelativePath)
                .ToList();

        return list
            .OrderBy(location => location.RelativePath)
            .Where(location =>
            {
                var absolutePath = location.AbsolutePath;
                return !string.IsNullOrEmpty(absolutePath) && File.Exists(absolutePath);
            })
            .ToList();
    }

    public Shoko_Video_Location? GetByFilePathAndImportFolderID(string relativePath, int importFolderId)
    {
        return ReadLock(() => RelativePathIndex!.GetMultiple(relativePath).FirstOrDefault(a => a.ImportFolderId == importFolderId));
    }

    public IReadOnlyList<Shoko_Video_Location> GetByVideoId(int videoId, bool resolve = false)
    {
        if (videoId <= 0)
            return new Shoko_Video_Location[0] {};

        var list = ReadLock(() => VideoIndex!.GetMultiple(videoId));
        if (!resolve)
            return list
                .OrderBy(location => new { ImportFolderId = location.ImportFolderId, RelativePath = location.RelativePath })
                .ToList();

        return list
            .OrderBy(location => new { ImportFolderId = location.ImportFolderId, RelativePath = location.RelativePath })
            .Where(location =>
            {
                var absolutePath = location.AbsolutePath;
                return !string.IsNullOrEmpty(absolutePath) && File.Exists(absolutePath);
            })
            .ToList();
    }
}
