using FluentNHibernate.Mapping;
using Shoko.Server.Models.Internal;

#nullable enable
#pragma warning disable 0618
namespace Shoko.Server.Mappings;

public class ShokoEpisode_UserMap : ClassMap<ShokoEpisode_User>
{
    public ShokoEpisode_UserMap()
    {
        Table("AnimeEpisode_User");
        Not.LazyLoad();
        Id(x => x.Id).Column("AnimeEpisode_UserID");

        Map(x => x.EpisodeId).Column("AnimeEpisodeID").Not.Nullable();
        Map(x => x.SeriesId).Column("AnimeSeriesID").Not.Nullable();
        Map(x => x.UserId).Column("JMMUserID").Not.Nullable();
        Map(x => x.WatchedCount).Column("WatchedCount").Not.Nullable();
        Map(x => x.PlayedCount).Column("PlayedCount").Not.Nullable();
        Map(x => x.StoppedCount).Column("StoppedCount").Not.Nullable();
        Map(x => x.LastWatchedAt).Column("WatchedDate");
    }
}
