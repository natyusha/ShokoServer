using FluentNHibernate.Mapping;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Mappings;

public class AniDB_Anime_RelationMap : ClassMap<AniDB_Anime_Relation>
{
    public AniDB_Anime_RelationMap()
    {
        Table("AniDB_Anime_Relation");
        Not.LazyLoad();
        Id(x => x.Id).Column("AniDB_Anime_RelationID");

        Map(x => x.AnidbAnimeId).Column("AnimeID").Not.Nullable();
        Map(x => x.RelatedAnidbAnimeId).Column("RelatedAnimeID").Not.Nullable();
        Map(x => x.RawType).Column("RelationType").Not.Nullable();
    }
}
