using FluentNHibernate.Mapping;
using Shoko.Server.Models.Internal;

namespace Shoko.Server.Mappings;

public class ShokoGroup_FilterConditionMap : SubclassMap<ShokoGroup_FilterCondition>
{
    public ShokoGroup_FilterConditionMap()
    {
        Not.LazyLoad();
        Map(x => x.ConditionOperator).Column("ConditionOperator").Not.Nullable();
        Map(x => x.ConditionParameter).Column("ConditionParameter");
        Map(x => x.ConditionType).Column("ConditionType").Not.Nullable();
    }
}
