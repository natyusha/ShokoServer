using FluentNHibernate.Mapping;
using Shoko.Server.Models.Internal;

namespace Shoko.Server.Mappings;

public class ImportFolderMap : ClassMap<ImportFolder>
{
    public ImportFolderMap()
    {
        Table("ImportFolder");
        Not.LazyLoad();
        Id(x => x.Id).Column("ImportFolderID");

        Map(x => x.Path).Column("ImportFolderLocation").Not.Nullable();
        Map(x => x.Name).Column("ImportFolderName").Not.Nullable();
        Map(x => x.IsDropDestination).Column("IsDropDestination").Not.Nullable();
        Map(x => x.IsDropSource).Column("IsDropSource").Not.Nullable();
        Map(x => x.IsWatched).Column("IsWatched").Not.Nullable();
    }
}
