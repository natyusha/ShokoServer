using FluentNHibernate.Mapping;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Mappings;

public class TMDB_Movie_CompanyMap : ClassMap<TMDB_Movie_Company>
{
    public TMDB_Movie_CompanyMap()
    {
        Table("TMDB_Movie_Company");

        Not.LazyLoad();
        Id(x => x.TMDB_Movie_CompanyID);

        Map(x => x.TmdbMovieID).Not.Nullable();
        Map(x => x.TmdbCompanyID).Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}
