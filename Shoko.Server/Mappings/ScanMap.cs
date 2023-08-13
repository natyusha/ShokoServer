using FluentNHibernate.Mapping;
using Shoko.Server.Models.Internal;

namespace Shoko.Server.Mappings;

public class ScanMap : ClassMap<Scan>
{
    public ScanMap()
    {
        Table("Scan");

        Not.LazyLoad();
        Id(x => x.Id);
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.ImportFolders).Not.Nullable();
        Map(x => x.Status).Not.Nullable();
    }
}
