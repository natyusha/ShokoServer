using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings;

public class CrossRef_AniDB_MALMap : ClassMap<CrossRef_AniDB_MAL>
{
    public CrossRef_AniDB_MALMap()
    {
        Not.LazyLoad();
        Id(x => x.Id);

        Map(x => x.AnidbAnimeId).Not.Nullable();
        Map(x => x.Source).Not.Nullable();
        Map(x => x.MalAnimeId).Not.Nullable();
    }
}
