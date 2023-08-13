using FluentNHibernate.Mapping;
using Shoko.Server.Databases.TypeConverters;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Mappings;

public class AniDB_Episode_TitleMap : ClassMap<AniDB_Episode_Title>
{
    public AniDB_Episode_TitleMap()
    {
        Table("AniDB_Episode_Title");
        Not.LazyLoad();
        Id(x => x.Id);

        Map(x => x.EpisodeId).Not.Nullable();
        Map(x => x.Language).CustomType<TitleLanguageConverter>().Not.Nullable();
        Map(x => x.Value).Not.Nullable();
    }
}
