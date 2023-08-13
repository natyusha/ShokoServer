using FluentNHibernate.Mapping;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Mappings;

public class AniDB_EpisodeMap : ClassMap<AniDB_Episode>
{
    public AniDB_EpisodeMap()
    {
        Table("AniDB_Episode");
        Not.LazyLoad();
        Id(x => x.AniDB_EpisodeID);

        Map(x => x.AirDate).Not.Nullable();
        Map(x => x.AnimeId).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
        Map(x => x.Overview).Not.Nullable().CustomType("StringClob");
        Map(x => x.EpisodeId).Not.Nullable();
        Map(x => x.Number).Not.Nullable();
        Map(x => x.Type).Not.Nullable();
        Map(x => x.RawDuration).Not.Nullable();
        Map(x => x.Rating).Not.Nullable();
        Map(x => x.Votes).Not.Nullable();
    }
}
