using Shoko.Models.Enums;

#nullable enable
namespace Shoko.Server.Models.Internal
{
    public class ShokoGroup_FilterCondition
    {
        public GroupFilterConditionType ConditionType { get; set; }

        public GroupFilterOperator ConditionOperator { get; set; }

        public string ConditionParameter { get; set; }

        public ShokoGroup_FilterCondition()
        {
            ConditionParameter = string.Empty;
        }
    }
}
