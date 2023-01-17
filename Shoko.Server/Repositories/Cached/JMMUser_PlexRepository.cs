using NutzCode.InMemoryIndex;
using Shoko.Models.Server;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class JMMUser_PlexRepository : BaseCachedRepository<JMMUser_Plex, int>
{
    private PocoIndex<int, JMMUser_Plex, int>? Users;

    public JMMUser_PlexRepository()
    {
    }

    protected override int SelectKey(JMMUser_Plex entity)
    {
        return entity.JMMUser_PlexID;
    }

    public JMMUser_Plex GetByUserID(int userid)
    {
        return ReadLock(() => Users!.GetOne(userid));
    }

    public override void PopulateIndexes()
    {
        Users = Cache.CreateIndex(a => a.JMMUserID);
    }

    public override void RegenerateDb()
    {
    }
}
