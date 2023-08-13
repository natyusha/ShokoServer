using FluentNHibernate.Mapping;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Databases.TypeConverters;

namespace Shoko.Server.Mappings;

public class AniDB_Anime_TitleMap : ClassMap<AniDB_AnimeTitle>
{
    public AniDB_Anime_TitleMap()
    {
        Table("AniDB_Anime_Title");
        Not.LazyLoad();
        Id(x => x.Id);

        Map(x => x.AnimeId).Not.Nullable();
        Map(x => x.Language).CustomType<TitleLanguageConverter>().Not.Nullable();
        Map(x => x.Title).Not.Nullable();
        Map(x => x.TitleType).CustomType<TitleTypeConverter>().Not.Nullable();
    }
}
