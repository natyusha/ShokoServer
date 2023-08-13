using FluentNHibernate.Mapping;
using Shoko.Server.Models;
using Shoko.Server.Models.Internal;

namespace Shoko.Server.Mappings;

public class ShokoVideo_UserMap : ClassMap<Shoko_Video_User>
{
    public ShokoVideo_UserMap()
    {
        Table("VideoLocal_User");
        Not.LazyLoad();
        Id(x => x.Id).Column("VideoLocal_UserID");

        Map(x => x.UserId).Column("JMMUserID").Not.Nullable();
        Map(x => x.VideoId).Column("VideoLocalID").Not.Nullable();
        Map(x => x.WatchedCount).Column("WatchedCount").Not.Nullable();
        Map(x => x.RawResumePosition).Column("ResumePosition").Not.Nullable();
        Map(x => x.LastWatchedAt).Column("WatchedDate");
        Map(x => x.LastUpdatedAt).Column("LastUpdated").Not.Nullable();
    }
}
