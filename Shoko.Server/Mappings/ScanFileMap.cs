using FluentNHibernate.Mapping;
using Shoko.Server.Models.Internal;

namespace Shoko.Server.Mappings;

public class ScanFileMap : ClassMap<ScanFile>
{
    public ScanFileMap()
    {
        Table("ScanFile");

        Not.LazyLoad();
        Id(x => x.Id);
        Map(x => x.ScanId).Not.Nullable();
        Map(x => x.ImportFolderId).Not.Nullable();
        Map(x => x.VideoLocationId).Not.Nullable();
        Map(x => x.AbsolutePath).Not.Nullable();
        Map(x => x.FileSize).Not.Nullable();
        Map(x => x.Status).Not.Nullable();
        Map(x => x.CheckedAt).Nullable();
        Map(x => x.ED2K).Not.Nullable();
        Map(x => x.CheckedED2K).Nullable();
    }
}
