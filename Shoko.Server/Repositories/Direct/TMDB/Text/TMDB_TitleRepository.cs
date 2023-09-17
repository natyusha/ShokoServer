using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Repositories.Direct;

public class TMDB_TitleRepository : BaseDirectRepository<TMDB_Title, int>
{
    public IReadOnlyList<TMDB_Title> GetByParentTypeAndId(ForeignEntityType parentType, int parentId)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Title>()
                .Where(a => a.ParentType == parentType && a.ParentID == parentId)
                .ToList();
        });
    }
}
