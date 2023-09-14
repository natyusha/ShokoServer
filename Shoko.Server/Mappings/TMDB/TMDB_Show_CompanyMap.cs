using FluentNHibernate.Mapping;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Mappings;

public class TMDB_Show_CompanyMap : ClassMap<TMDB_Show_Company>
{
    public TMDB_Show_CompanyMap()
    {
        Table("TMDB_Show_Company");

        Not.LazyLoad();
        Id(x => x.TMDB_Show_CompanyID);

        Map(x => x.TmdbShowID).Not.Nullable();
        Map(x => x.TmdbCompanyID).Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}
