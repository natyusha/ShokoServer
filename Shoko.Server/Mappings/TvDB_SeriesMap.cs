using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings;

public class TvDB_SeriesMap : ClassMap<TvDB_Show>
{
    public TvDB_SeriesMap()
    {
        Not.LazyLoad();
        Id(x => x.Id).Not.Nullable();

        Map(x => x.Banner);
        Map(x => x.Fanart);
        Map(x => x.LastUpdatedAt);
        Map(x => x.MainOverview);
        Map(x => x.Poster);
        Map(x => x.MainTitle);
        Map(x => x.Status);
        Map(x => x.Rating);
    }
}
