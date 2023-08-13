using FluentNHibernate.Mapping;
using Shoko.Server.Models.Internal;

#nullable enable
#pragma warning disable 0618
namespace Shoko.Server.Mappings;

public class ShokoSeries_UserMap : ClassMap<ShokoSeries_User>
{
    public ShokoSeries_UserMap()
    {
        Table("AnimeSeries_User");
        Not.LazyLoad();
        Id(x => x.Id).Column("AnimeSeries_UserID");

        Map(x => x.UserId).Column("JMMUserID").Not.Nullable();
        Map(x => x.SeriesId).Column("AnimeSeriesID").Not.Nullable();
        Map(x => x.PlayedCount).Column("PlayedCount").Not.Nullable();
        Map(x => x.StoppedCount).Column("StoppedCount").Not.Nullable();
        Map(x => x.UnwatchedEpisodeCount).Column("UnwatchedEpisodeCount").Not.Nullable();
        Map(x => x.HiddenUnwatchedEpisodeCount).Column("HiddenUnwatchedEpisodeCount").Not.Nullable();
        Map(x => x.WatchedCount).Column("WatchedCount").Not.Nullable();
        Map(x => x.WatchedEpisodeCount).Column("WatchedEpisodeCount").Not.Nullable();
        Map(x => x.WatchedDate).Column("WatchedDate");
        Map(x => x.LastEpisodeUpdate).Column("LastEpisodeUpdate");
    }
}
