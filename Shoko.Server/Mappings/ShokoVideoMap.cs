using FluentNHibernate.Mapping;
using Shoko.Server.Models.Internal;

#nullable enable
#pragma warning disable 0618
namespace Shoko.Server.Mappings;

public class ShokoVideoMap : ClassMap<Shoko_Video>
{
    public ShokoVideoMap()
    {
        Table("VideoLocal");
        Not.LazyLoad();
        Id(x => x.Id).Column("VideoLocalID");

        Map(x => x.LastUpdatedAt).Column("DateTimeUpdated").Not.Nullable();
        Map(x => x.CreatedAt).Column("DateTimeCreated").Not.Nullable();
        Map(x => x.LastImportedAt).Column("DateTimeImported");
        Map(x => x.FileName).Column("FileName").Not.Nullable();
        Map(x => x.Size).Column("FileSize").Not.Nullable();
        Map(x => x.ED2K).Column("Hash").Not.Nullable();
        Map(x => x.CRC32).Column("CRC32");
        Map(x => x.MD5).Column("MD5");
        Map(x => x.SHA1).Column("SHA1");
        Map(x => x.IsIgnored).Column("IsIgnored").Not.Nullable();
        Map(x => x.IsVariation).Column("IsVariation").Not.Nullable();
        Map(x => x.MediaVersion).Column("MediaVersion").Not.Nullable();
        Map(x => x.MediaBlob).Column("MediaBlob").Nullable().CustomType("BinaryBlob");
        Map(x => x.MediaSize).Column("MediaSize").Not.Nullable();
        Map(x => x.AniDBMyListId).Column("MyListID").Not.Nullable();
    }
}
