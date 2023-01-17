using FluentNHibernate.Mapping;
using Shoko.Server.Databases.TypeConverters;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings;

public class JMMUserMap : ClassMap<SVR_JMMUser>
{
    public JMMUserMap()
    {
        Table("JMMUser");

        Not.LazyLoad();
        Id(x => x.JMMUserID);

        Map(x => x.RestrictedTags).CustomType<StringHashSetConverter<string>>().Not.Nullable();
        Map(x => x.IsAniDBUser).Not.Nullable();
        Map(x => x.IsTraktUser).Not.Nullable();
        Map(x => x.IsAdmin).Not.Nullable();
        Map(x => x.Password);
        Map(x => x.Username);
    }
}
