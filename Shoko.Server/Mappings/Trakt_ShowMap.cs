using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings;

public class Trakt_ShowMap : ClassMap<Trakt_Show>
{
    public Trakt_ShowMap()
    {
        Not.LazyLoad();
        Id(x => x.Id);

        Map(x => x.MainOverview);
        Map(x => x.MainTitle);
        Map(x => x.TraktShowID);
        Map(x => x.TvdbShowId);
        Map(x => x.URL);
        Map(x => x.Year);
    }
}
