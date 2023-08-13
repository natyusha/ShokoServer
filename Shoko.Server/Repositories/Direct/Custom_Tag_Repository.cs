using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Models.Internal;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories;

public class Custom_Tag_Repository : BaseDirectRepository<Custom_Tag, int>
{
    public Custom_Tag_Repository()
    {
        DeleteWithOpenTransactionCallback = (ses, obj) =>
        {
            RepoFactory.CR_CustomTag.DeleteWithOpenTransaction(ses,
                RepoFactory.CR_CustomTag.GetByCustomTagID(obj.Id));
        };
    }

    public IReadOnlyList<Custom_Tag> GetByAnimeID(int animeID)
    {
        return RepoFactory.CR_CustomTag.GetByAnimeID(animeID)
            .Select(a => GetByID(a.CustomTagID))
            .Where(a => a != null)
            .ToList();
    }

    public Dictionary<int, IReadOnlyList<Custom_Tag>> GetByAnimeIDs(int[] animeIDs)
    {
        return animeIDs.ToDictionary(a => a, a => GetByAnimeID(a));
    }
}
