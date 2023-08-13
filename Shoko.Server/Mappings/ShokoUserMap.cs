using FluentNHibernate.Mapping;
using Shoko.Server.Models;
using Shoko.Server.Models.Internal;

namespace Shoko.Server.Mappings;

public class ShokoUserMap : ClassMap<Shoko_User>
{
    public ShokoUserMap()
    {
        Table("JMMUser");

        Not.LazyLoad();
        Id(x => x.Id).Column("JMMUserID");

        Map(x => x.RestrictedTags).Column("HideCategories");
        Map(x => x.IsAniDBUser).Column("IsAniDBUser").Not.Nullable();
        Map(x => x.IsTraktUser).Column("IsTraktUser").Not.Nullable();
        Map(x => x.IsAdmin).Column("IsAdmin").Not.Nullable();
        Map(x => x.Password).Column("Password");
        Map(x => x.Username).Column("Username");
    }
}
