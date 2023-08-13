using FluentNHibernate.Mapping;
using Shoko.Server.Models.Internal;

namespace Shoko.Server.Mappings;

public class CustomTagMap : ClassMap<Custom_Tag>
{
    public CustomTagMap()
    {
        Table("CustomTag");
        Not.LazyLoad();
        Id(x => x.Id).Column("CustomTagID");

        Map(x => x.Name).Column("TagName");
        Map(x => x.Description).Column("TagDescription");
    }
}
