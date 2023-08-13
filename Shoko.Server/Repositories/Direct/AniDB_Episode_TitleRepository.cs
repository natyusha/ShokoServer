using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models.AniDB;

#nullable enable
namespace Shoko.Server.Repositories;

public class AniDB_Episode_TitleRepository : BaseCachedRepository<AniDB_Episode_Title, int>
{
    private PocoIndex<int, AniDB_Episode_Title, int>? Episodes;

    public override void PopulateIndexes()
    {
        Episodes = new PocoIndex<int, AniDB_Episode_Title, int>(Cache, a => a.EpisodeId);
    }

    protected override int SelectKey(AniDB_Episode_Title entity)
    {
        return entity.Id;
    }

    public override void RegenerateDb()
    {
    }

    public List<AniDB_Episode_Title> GetByEpisodeIDAndLanguage(int id, TextLanguage language)
    {
        // TODO: Replace this with SQL when moving to a direct repo instead of a cached repo.
        return GetByEpisodeID(id).Where(a => a.Language == language).ToList();
    }

    public AniDB_Episode_Title? GetByEpisodeIDAndValue(int id, string value)
    {
        // TODO: Replace this with SQL when moving to a direct repo instead of a cached repo.
        return GetByEpisodeID(id).FirstOrDefault(a => a.Value == value);
    }

    public List<AniDB_Episode_Title> GetByEpisodeID(int id)
    {
        // TODO: Replace this with SQL when moving to a direct repo instead of a cached repo.
        return ReadLock(() => Episodes!.GetMultiple(id));
    }
}
