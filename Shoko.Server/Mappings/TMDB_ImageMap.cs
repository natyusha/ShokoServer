using FluentNHibernate.Mapping;
using Shoko.Models.Server.TMDB;
using Shoko.Server.Databases.TypeConverters;
using Shoko.Server.Server;

namespace Shoko.Server.Mappings;

public class TMDB_ImageMap : ClassMap<TMDB_Image>
{
    public TMDB_ImageMap()
    {
        Table("TMDB_Image");

        Not.LazyLoad();
        Id(x => x.TMDB_ImageID);

        Map(x => x.TmdbMovieID);
        Map(x => x.TmdbEpisodeID);
        Map(x => x.TmdbSeasonID);
        Map(x => x.TmdbShowID);
        Map(x => x.TmdbCollectionID);
        Map(x => x.ForeignType).Not.Nullable().CustomType<ForeignEntityType>();
        Map(x => x.ImageType).Not.Nullable().CustomType<ImageEntityType>();
        Map(x => x.AspectRatio).Not.Nullable();
        Map(x => x.Width).Not.Nullable();
        Map(x => x.Height).Not.Nullable();
        Map(x => x.Language).Not.Nullable().CustomType<TitleLanguageConverter>();
        Map(x => x.RemoteFileName).Not.Nullable();
        Map(x => x.UserRating).Not.Nullable();
        Map(x => x.UserVotes).Not.Nullable();
    }
}
