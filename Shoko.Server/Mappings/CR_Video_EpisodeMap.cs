using FluentNHibernate.Mapping;
using Shoko.Server.Models;
using Shoko.Server.Models.CrossReferences;

namespace Shoko.Server.Mappings;

public class CR_Video_EpisodeMap : ClassMap<CR_Video_Episode>
{
    public CR_Video_EpisodeMap()
    {
        Table("CrossRef_File_Episode");

        Not.LazyLoad();
        Id(x => x.Id).Column("CrossRef_File_EpisodeID");

        Map(x => x.CrossReferenceSource).Column("CrossRefSource").Not.Nullable();
        Map(x => x.AnidbEpisodeId).Column("EpisodeID").Not.Nullable();
        Map(x => x.Order).Column("EpisodeOrder").Not.Nullable();
        Map(x => x.ED2K).Column("Hash").Not.Nullable();
        Map(x => x.Percentage).Column("Percentage").Not.Nullable();
        Map(x => x.FileName).Column("FileName").Not.Nullable();
        Map(x => x.FileSize).Column("FileSize").Not.Nullable();
        Map(x => x.AnidbAnimeId).Column("AnimeID").Not.Nullable();
    }
}
