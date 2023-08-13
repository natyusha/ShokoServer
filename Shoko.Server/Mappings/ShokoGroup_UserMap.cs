using FluentNHibernate.Mapping;
using Shoko.Server.Models.Internal;

#nullable enable
#pragma warning disable 0618
namespace Shoko.Server.Mappings;

public class ShokoGroup_UserMap : ClassMap<ShokoGroup_User>
{
    public ShokoGroup_UserMap()
    {
        Table("AnimeGroup_User");
        Not.LazyLoad();
        Id(x => x.Id).Column("AnimeGroup_UserID");

        Map(x => x.UserId).Column("JMMUserID");
        Map(x => x.GroupId).Column("AnimeGroupID");
        Map(x => x.IsFavorite).Column("IsFave").Not.Nullable();
        Map(x => x.PlayedCount).Column("PlayedCount").Not.Nullable();
        Map(x => x.StoppedCount).Column("StoppedCount").Not.Nullable();
        Map(x => x.UnwatchedEpisodeCount).Column("UnwatchedEpisodeCount").Not.Nullable();
        Map(x => x.WatchedCount).Column("WatchedCount").Not.Nullable();
        Map(x => x.LastWatchedAt).Column("WatchedDate");
        Map(x => x.WatchedEpisodeCount).Column("WatchedEpisodeCount");
    }
}
