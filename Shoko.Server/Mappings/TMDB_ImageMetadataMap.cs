using FluentNHibernate.Mapping;
using Shoko.Models.Server.TMDB;
using Shoko.Server.Databases.TypeConverters;
using Shoko.Server.Server;

namespace Shoko.Server.Mappings;

public class TMDB_ImageMetadataMap : ClassMap<TMDB_ImageMetadata>
{
    public TMDB_ImageMetadataMap()
    {
        Table("TMDB_ImageMetadata");

        Not.LazyLoad();
        Id(x => x.TMDB_ImageMetadataID);

        Map(x => x.TmdbMovieID);
        Map(x => x.TmdbEpisodeID);
        Map(x => x.TmdbSeasonID);
        Map(x => x.TmdbShowID);
        Map(x => x.TmdbCollectionID);
        Map(x => x.ForeignType).Not.Nullable().CustomType<ForeignEntityType>();
        Map(x => x.ImageType).Not.Nullable().CustomType<ImageEntityType_New>();
        Map(x => x.AspectRatio).Not.Nullable();
        Map(x => x.Width).Not.Nullable();
        Map(x => x.Height).Not.Nullable();
        Map(x => x.Language).Not.Nullable().CustomType<TitleLanguageConverter>();
        Map(x => x.RemoteFileName).Not.Nullable();
        Map(x => x.UserRating).Not.Nullable();
        Map(x => x.UserVotes).Not.Nullable();
    }
}
