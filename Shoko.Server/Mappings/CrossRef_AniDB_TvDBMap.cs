using FluentNHibernate.Mapping;
using Shoko.Models.Enums;
using Shoko.Server.Models.CrossReferences;

namespace Shoko.Server.Mappings;

public class CrossRef_AniDB_TvDBMap : ClassMap<CR_AniDB_TvDB>
{
    public CrossRef_AniDB_TvDBMap()
    {
        Not.LazyLoad();
        Table("CrossRef_AniDB_TvDB");
        Id(x => x.Id).Column("CrossRef_AniDB_TvDBID");

        Map(x => x.AnidbAnimeId).Column("AniDBID").Not.Nullable();
        Map(x => x.TvdbShowId).Column("TvDBID").Not.Nullable();
        Map(x => x.Source).Column("CrossRefSource").CustomType<CrossRefSource>().Not.Nullable();
    }
}
