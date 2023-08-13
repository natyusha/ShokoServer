using FluentNHibernate.Mapping;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models.Internal;

namespace Shoko.Server.Mappings;

public class AnimeSeriesMap : ClassMap<ShokoSeries>
{
    public AnimeSeriesMap()
    {
        Table("AnimeSeries");
        Not.LazyLoad();
        Id(x => x.Id).Column("AnimeSeriesID");

        Map(x => x.AniDB_ID).Column("AniDB_ID").Not.Nullable();
        Map(x => x.ParentGroupId).Column("AnimeGroupID").Not.Nullable();
        Map(x => x.DateTimeCreated).Column("DateTimeCreated").Not.Nullable();
        Map(x => x.DateTimeUpdated).Column("DateTimeUpdated").Not.Nullable();
        Map(x => x.LatestLocalEpisodeNumber).Column("LatestLocalEpisodeNumber").Not.Nullable();
        Map(x => x.EpisodeAddedDate).Column("EpisodeAddedDate");
        Map(x => x.LatestEpisodeAirDate).Column("LatestEpisodeAirDate");
        Map(x => x.MissingEpisodeCount).Column("MissingEpisodeCount").Not.Nullable();
        Map(x => x.MissingEpisodeCountGroups).Column("MissingEpisodeCountGroups").Not.Nullable();
        Map(x => x.HiddenMissingEpisodeCount).Column("HiddenMissingEpisodeCount").Not.Nullable();
        Map(x => x.HiddenMissingEpisodeCountGroups).Column("HiddenMissingEpisodeCountGroups").Not.Nullable();
        Map(x => x.SeriesNameOverride).Column("SeriesNameOverride");
        Map(x => x.ContractVersion).Column("ContractVersion").Not.Nullable();
        Map(x => x.ContractBlob).Column("ContractBlob").Nullable().CustomType("BinaryBlob");
        Map(x => x.ContractSize).Column("ContractSize").Not.Nullable();
        Map(x => x.AirsOn).Column("AirsOn");
        Map(x => x.UpdatedAt).Column("UpdatedAt").Not.Nullable();
        Map(x => x.DisableAutoMatchFlags).Column("DisableAutoMatchFlags").Not.Nullable().CustomType<DataSource>();
    }
}
