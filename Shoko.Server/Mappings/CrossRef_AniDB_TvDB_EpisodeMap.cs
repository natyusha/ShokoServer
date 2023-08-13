using FluentNHibernate.Mapping;
using Shoko.Models.Enums;
using Shoko.Server.Models.TvDB;

namespace Shoko.Server.Mappings;

public class CrossRef_AniDB_TvDB_EpisodeMap : ClassMap<CR_AniDB_TvDB_Episode>
{
    public CrossRef_AniDB_TvDB_EpisodeMap()
    {
        Not.LazyLoad();
        Table("CrossRef_AniDB_TvDB_Episode");
        Id(x => x.Id).Column("CrossRef_AniDB_TvDB_EpisodeID");

        Map(x => x.AnidbEpisodeId).Column("AniDBEpisodeID").Not.Nullable();
        Map(x => x.TvdbEpisodeId).Column("TvDBEpisodeID").Not.Nullable();
        Map(x => x.MatchRating).CustomType<MatchRating>().Not.Nullable();
        Map(x => x.Source).CustomType<CrossRefSource>().Not.Nullable();
    }
}
