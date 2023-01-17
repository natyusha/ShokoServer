using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Databases.TypeConverters;

namespace Shoko.Server.Mappings;

public class JMMUser_PlexMap : ClassMap<JMMUser_Plex>
{
    public JMMUser_PlexMap()
    {
        Table("JMMUser_Plex");

        Not.LazyLoad();
        Id(x => x.JMMUser_PlexID);

        Map(x => x.JMMUserID).Not.Nullable();
        Map(x => x.AccountID);
        Map(x => x.Token);
        Map(x => x.SelectedServer);
        Map(x => x.SelectedLibraries).CustomType<StringHashSetConverter<int>>().Not.Nullable();
        Map(x => x.LocalUsers).CustomType<StringHashSetConverter<string>>().Not.Nullable();
    }
}
