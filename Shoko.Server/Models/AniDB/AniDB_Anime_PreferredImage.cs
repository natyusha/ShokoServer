
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_Anime_PreferredImage
{
    public int AniDB_Anime_PreferredImageID { get; set; }

    public int AnidbAnimeID { get; set; }

    public int ImageID { get; set; }

    public ImageEntityType ImageType { get; set; }

    public DataSourceType ImageSource { get; set; }

    public IImageEntity? GetImageEntity()
        => ImageSource switch
        {
            DataSourceType.TMDB => ImageType switch
            {
                ImageEntityType.Backdrop =>
                    RepoFactory.TMDB_Image.GetByID(ImageID)?.ToClientFanart(),
                ImageEntityType.Poster =>
                    RepoFactory.TMDB_Image.GetByID(ImageID)?.ToClientPoster(),
                _ => null,
            },
            DataSourceType.TvDB => ImageType switch
            {
                ImageEntityType.Backdrop =>
                    RepoFactory.TvDB_ImageFanart.GetByID(ImageID),
                ImageEntityType.Banner =>
                    RepoFactory.TvDB_ImageWideBanner.GetByID(ImageID),
                ImageEntityType.Poster =>
                    RepoFactory.TvDB_ImagePoster.GetByID(ImageID),
                _ => null,
            },
            _ => null,
        };
}
