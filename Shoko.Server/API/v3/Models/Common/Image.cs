using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using ImageMagick;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// Image container
/// </summary>
public class Image
{
    /// <summary>
    /// AniDB, TvDB, TMDB, etc.
    /// </summary>
    [Required]
    public ImageSource Source { get; set; }

    /// <summary>
    /// text representation of type of image. fanart, poster, etc. Mainly so clients know what they are getting
    /// </summary>
    [Required]
    public ImageType Type { get; set; }

    /// <summary>
    /// The image's ID.
    /// </summary>
    [Required]
    public int ID { get; set; }

    /// <summary>
    /// The relative path from the base image directory. A client with access to the server's filesystem can map
    /// these for quick access and no need for caching
    /// </summary>
    public string? RelativeFilepath { get; set; }

    /// <summary>
    /// Is it marked as default. Only one default is possible for a given <see cref="Image.Type"/>.
    /// </summary>
    public bool Preferred { get; set; }

    /// <summary>
    /// Width of the image.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Height of the image.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Is it marked as disabled. You must explicitly ask for these, for obvious reasons.
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// Series info for the image, currently only set when sending a random
    /// image.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ImageSeriesInfo? Series { get; set; } = null;

    public Image(int id, ImageEntityType imageEntityType, DataSourceType dataSource, bool preferred = false, bool disabled = false)
    {
        ID = id;
        Type = imageEntityType.ToV3Dto();
        Source = dataSource.ToV3Dto();

        Preferred = preferred;
        Disabled = disabled;
        switch (dataSource)
        {
            case DataSourceType.User:
                if (imageEntityType == ImageEntityType.Art)
                {
                    var user = RepoFactory.JMMUser.GetByID(id);
                    if (user != null && user.HasAvatarImage)
                    {
                        var imageMetadata = user.AvatarImageMetadata;
                        // we need to set _something_ for the clients that determine
                        // if an image exists by checking if a relative path is set,
                        // so we set the id.
                        RelativeFilepath = $"/{id}";
                        Width = imageMetadata.Width;
                        Height = imageMetadata.Height;
                    }
                }
                break;

            // We can now grab the metadata from the database(!)
            case DataSourceType.TMDB:
                var tmdbImage = RepoFactory.TMDB_Image.GetByID(id);
                if (tmdbImage != null)
                {
                    var relativePath = tmdbImage.RelativePath;
                    if (!string.IsNullOrEmpty(relativePath))
                    {
                        RelativeFilepath = relativePath.Replace("\\", "/");
                        if (!RelativeFilepath.StartsWith("/"))
                            RelativeFilepath = "/" + RelativeFilepath;
                    }
                    Width = tmdbImage.Width;
                    Height = tmdbImage.Height;
                }
                break;

            default:
                var imagePath = GetImagePath(imageEntityType, dataSource, id);
                if (!string.IsNullOrEmpty(imagePath))
                {
                    RelativeFilepath = imagePath.Replace(ImageUtils.GetBaseImagesPath(), "").Replace("\\", "/");
                    if (!RelativeFilepath.StartsWith("/"))
                        RelativeFilepath = "/" + RelativeFilepath;
                    // This causes serious IO lag on some systems. Enable at own risk.
                    if (Utils.SettingsProvider.GetSettings().LoadImageMetadata)
                    {
                        var info = new MagickImageInfo(imagePath);
                        Width = info.Width;
                        Height = info.Height;
                    }
                }
                break;
        }
    }

    public static string? GetImagePath(ImageEntityType imageType, DataSourceType dataSource, int id)
    {
        string path;
        switch (imageType.ToClient(dataSource))
        {
            // 1
            case CL_ImageEntityType.AniDB_Cover:
                var anime = RepoFactory.AniDB_Anime.GetByAnimeID(id);
                if (anime == null)
                {
                    return null;
                }

                path = anime.PosterPath;
                if (File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                }

                break;
            // 4
            case CL_ImageEntityType.TvDB_Banner:
                var wideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(id);
                if (wideBanner == null)
                {
                    return null;
                }

                path = wideBanner.GetFullImagePath();
                if (File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                }

                break;

            // 5
            case CL_ImageEntityType.TvDB_Cover:
                var poster = RepoFactory.TvDB_ImagePoster.GetByID(id);
                if (poster == null)
                {
                    return null;
                }

                path = poster.GetFullImagePath();
                if (File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                }

                break;

            // 6
            case CL_ImageEntityType.TvDB_Episode:
                var ep = RepoFactory.TvDB_Episode.GetByTvDBID(id);
                if (ep == null)
                {
                    return null;
                }

                path = ep.GetFullImagePath();
                if (File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                }

                break;

            // 7
            case CL_ImageEntityType.TvDB_FanArt:
                var fanart = RepoFactory.TvDB_ImageFanart.GetByID(id);
                if (fanart == null)
                {
                    return null;
                }

                path = fanart.GetFullImagePath();
                if (File.Exists(path))
                {
                    return path;
                }

                path = string.Empty;
                break;

            case CL_ImageEntityType.Character:
                var character = RepoFactory.AnimeCharacter.GetByID(id);
                if (character == null)
                {
                    return null;
                }

                path = ImageUtils.GetBaseAniDBCharacterImagesPath() + Path.DirectorySeparatorChar + character.ImagePath;
                if (File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                }

                break;

            case CL_ImageEntityType.Staff:
                var staff = RepoFactory.AnimeStaff.GetByID(id);
                if (staff == null)
                {
                    return null;
                }

                path = ImageUtils.GetBaseAniDBCreatorImagesPath() + Path.DirectorySeparatorChar + staff.ImagePath;
                if (File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = string.Empty;
                }

                break;

            default:
                path = string.Empty;
                break;
        }

        return path;
    }

    private static readonly List<DataSourceType> BannerImageSources = new() { DataSourceType.TvDB };

    private static readonly List<DataSourceType> PosterImageSources = new()
    {
        DataSourceType.AniDB,
        DataSourceType.TMDB,
        DataSourceType.TvDB,
    };

    // There is only one thumbnail provider atm.
    private static readonly List<DataSourceType> ThumbImageSources = new() { DataSourceType.TvDB };

    // TMDB is too unreliable atm, so we will only use TvDB for now.
    private static readonly List<DataSourceType> FanartImageSources = new()
    {
        DataSourceType.TMDB,
        DataSourceType.TvDB,
    };

    private static readonly List<DataSourceType> CharacterImageSources = new()
    {
        // DataSourceType.AniDB,
        DataSourceType.Shoko
    };

    private static readonly List<DataSourceType> StaffImageSources = new()
    {
        // DataSourceType.AniDB,
        DataSourceType.Shoko
    };

    private static readonly List<DataSourceType> StaticImageSources = new() { DataSourceType.Shoko };

    internal static DataSourceType GetRandomImageSource(ImageType imageType)
    {
        var sourceList = imageType switch
        {
            ImageType.Poster => PosterImageSources,
            ImageType.Banner => BannerImageSources,
            ImageType.Thumb => ThumbImageSources,
            ImageType.Fanart => FanartImageSources,
            ImageType.Character => CharacterImageSources,
            ImageType.Staff => StaffImageSources,
            _ => StaticImageSources
        };

        return sourceList.GetRandomElement();
    }

    internal static int? GetRandomImageID(ImageEntityType imageType, DataSourceType dataSource)
    {
        return dataSource switch
        {
            DataSourceType.AniDB => imageType switch
            {
                ImageEntityType.Poster => RepoFactory.AniDB_Anime.GetAll()
                    .Where(a => a?.PosterPath != null && !a.GetAllTags().Contains("18 restricted"))
                    .GetRandomElement()?.AnimeID,
                ImageEntityType.Character => RepoFactory.AniDB_Anime.GetAll()
                    .Where(a => a != null && !a.GetAllTags().Contains("18 restricted"))
                    .SelectMany(a => a.GetAnimeCharacters()).Select(a => a.GetCharacter()).Where(a => a != null)
                    .GetRandomElement()?.AniDB_CharacterID,
                ImageEntityType.Person => RepoFactory.AniDB_Anime.GetAll()
                    .Where(a => a != null && !a.GetAllTags().Contains("18 restricted"))
                    .SelectMany(a => a.GetAnimeCharacters())
                    .SelectMany(a => RepoFactory.AniDB_Character_Seiyuu.GetByCharID(a.CharID))
                    .Select(a => RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(a.SeiyuuID)).Where(a => a != null)
                    .GetRandomElement()?.AniDB_SeiyuuID,
                _ => null,
            },
            DataSourceType.Shoko => imageType switch
            {
                ImageEntityType.Character => RepoFactory.AniDB_Anime.GetAll()
                    .Where(a => a != null && !a.GetAllTags().Contains("18 restricted"))
                    .SelectMany(a => RepoFactory.CrossRef_Anime_Staff.GetByAnimeID(a.AnimeID))
                    .Where(a => a.RoleType == (int)StaffRoleType.Seiyuu && a.RoleID.HasValue)
                    .Select(a => RepoFactory.AnimeCharacter.GetByID(a.RoleID!.Value))
                    .GetRandomElement()?.CharacterID,
                ImageEntityType.Person => RepoFactory.AniDB_Anime.GetAll()
                    .Where(a => a != null && !a.GetAllTags().Contains("18 restricted"))
                    .SelectMany(a => RepoFactory.CrossRef_Anime_Staff.GetByAnimeID(a.AnimeID))
                    .Select(a => RepoFactory.AnimeStaff.GetByID(a.StaffID))
                    .GetRandomElement()?.StaffID,
                _ => null,
            },
            DataSourceType.TMDB => RepoFactory.TMDB_Image.GetByType(imageType)
                .GetRandomElement()?.TMDB_ImageID,
            // TvDB doesn't allow H content, so we get to skip the check!
            DataSourceType.TvDB => imageType switch
            {
                ImageEntityType.Backdrop => RepoFactory.TvDB_ImageFanart.GetAll()
                    .GetRandomElement()?.TvDB_ImageFanartID,
                ImageEntityType.Banner => RepoFactory.TvDB_ImageWideBanner.GetAll()
                    .GetRandomElement()?.TvDB_ImageWideBannerID,
                ImageEntityType.Poster => RepoFactory.TvDB_ImagePoster.GetAll()
                    .GetRandomElement()?.TvDB_ImagePosterID,
                ImageEntityType.Thumbnail => RepoFactory.TvDB_Episode.GetAll()
                    .GetRandomElement()?.Id,
                _ => null,
            },
            _ => null,
        };
    }

    internal static SVR_AnimeSeries? GetFirstSeriesForImage(ImageEntityType imageType, DataSourceType imageSource, int imageID)
    {
        switch (imageType.ToClient(imageSource))
        {
            case CL_ImageEntityType.AniDB_Cover:
                return RepoFactory.AnimeSeries.GetByAnimeID(imageID);

            case CL_ImageEntityType.TvDB_Banner:
                var tvdbWideBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(imageID);
                if (tvdbWideBanner == null)
                    return null;

                var bannerXRef = RepoFactory.CrossRef_AniDB_TvDB.GetByTvDBID(tvdbWideBanner.SeriesID).FirstOrDefault();
                if (bannerXRef == null)
                    return null;

                return RepoFactory.AnimeSeries.GetByAnimeID(bannerXRef.AniDBID);

            case CL_ImageEntityType.TvDB_Cover:
                var tvdbPoster = RepoFactory.TvDB_ImagePoster.GetByID(imageID);
                if (tvdbPoster == null)
                    return null;

                var coverXRef = RepoFactory.CrossRef_AniDB_TvDB.GetByTvDBID(tvdbPoster.SeriesID).FirstOrDefault();
                if (coverXRef == null)
                    return null;

                return RepoFactory.AnimeSeries.GetByAnimeID(coverXRef.AniDBID);

            case CL_ImageEntityType.TvDB_FanArt:
                var tvdbFanart = RepoFactory.TvDB_ImageFanart.GetByID(imageID);
                if (tvdbFanart == null)
                    return null;

                var fanartXRef = RepoFactory.CrossRef_AniDB_TvDB.GetByTvDBID(tvdbFanart.SeriesID).FirstOrDefault();
                if (fanartXRef == null)
                    return null;

                return RepoFactory.AnimeSeries.GetByAnimeID(fanartXRef.AniDBID);

            case CL_ImageEntityType.TvDB_Episode:
                var tvdbEpisode = RepoFactory.TvDB_Episode.GetByID(imageID);
                if (tvdbEpisode == null)
                    return null;

                var episodeXRef = RepoFactory.CrossRef_AniDB_TvDB.GetByTvDBID(tvdbEpisode.SeriesID).FirstOrDefault();
                if (episodeXRef == null)
                    return null;

                return RepoFactory.AnimeSeries.GetByAnimeID(episodeXRef.AniDBID);

            case CL_ImageEntityType.MovieDB_Poster:
            case CL_ImageEntityType.MovieDB_FanArt:
                var tmdbImage = RepoFactory.TMDB_Image.GetByID(imageID);
                if (tmdbImage == null || !tmdbImage.TmdbMovieID.HasValue)
                    return null;

                if (tmdbImage.TmdbMovieID.HasValue)
                {
                    var movieXref = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByTmdbMovieID(tmdbImage.TmdbMovieID.Value).FirstOrDefault();
                    if (movieXref == null)
                        return null;

                    return RepoFactory.AnimeSeries.GetByAnimeID(movieXref.AnidbAnimeID);
                }

                if (tmdbImage.TmdbShowID.HasValue)
                {
                    var showXref = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByTmdbShowID(tmdbImage.TmdbShowID.Value).FirstOrDefault();
                    if (showXref == null)
                        return null;

                    return RepoFactory.AnimeSeries.GetByAnimeID(showXref.AnidbAnimeID);
                }

                return null;

            default:
                return null;
        };
    }

    /// <summary>
    /// Image source.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ImageSource
    {
        /// <summary>
        ///
        /// </summary>
        AniDB = 1,

        /// <summary>
        ///
        /// </summary>
        TvDB = 2,

        /// <summary>
        ///
        /// </summary>
        TMDB = 3,

        /// <summary>
        /// User provided data.
        /// </summary>
        User = 99,

        /// <summary>
        ///
        /// </summary>
        Shoko = 100
    }

    /// <summary>
    /// Image type.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ImageType
    {
        /// <summary>
        ///
        /// </summary>
        Poster = 1,

        /// <summary>
        ///
        /// </summary>
        Banner = 2,

        /// <summary>
        ///
        /// </summary>
        Thumb = 3,

        /// <summary>
        ///
        /// </summary>
        Fanart = 4,

        /// <summary>
        ///
        /// </summary>
        Character = 5,

        /// <summary>
        ///
        /// </summary>
        Staff = 6,

        /// <summary>
        /// User avatar.
        /// </summary>
        Avatar = 99,
    }

    public class ImageSeriesInfo
    {
        /// <summary>
        /// The shoko series id.
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// The preferred series name for the user.
        /// </summary>
        public string Name { get; set; }

        public ImageSeriesInfo(int id, string name)
        {
            ID = id;
            Name = name;
        }
    }

    /// <summary>
    /// Input models.
    /// </summary>
    public class Input
    {
        public class DefaultImageBody
        {
            /// <summary>
            /// The ID. A stringified int since we send the ID as a string
            /// from the API. Also see <seealso cref="Image.ID"/>.
            /// </summary>
            /// <value></value>
            [Required]
            public int ID { get; set; }

            /// <summary>
            /// The image source.
            /// </summary>
            /// <value></value>
            [Required]
            public ImageSource Source { get; set; }
        }
    }
}

public static class ImageExtensions
{
    public static ImageEntityType ToServer(this Image.ImageType type)
        => type switch
        {
            Image.ImageType.Avatar => ImageEntityType.Art,
            Image.ImageType.Banner => ImageEntityType.Banner,
            Image.ImageType.Character => ImageEntityType.Character,
            Image.ImageType.Fanart => ImageEntityType.Backdrop,
            Image.ImageType.Poster => ImageEntityType.Poster,
            Image.ImageType.Staff => ImageEntityType.Person,
            Image.ImageType.Thumb => ImageEntityType.Thumbnail,
            _ => ImageEntityType.None,
        };

    public static Image.ImageType ToV3Dto(this ImageEntityType type)
        => type switch
        {
            ImageEntityType.Art => Image.ImageType.Avatar,
            ImageEntityType.Banner => Image.ImageType.Banner,
            ImageEntityType.Character => Image.ImageType.Character,
            ImageEntityType.Backdrop => Image.ImageType.Fanart,
            ImageEntityType.Poster => Image.ImageType.Poster,
            ImageEntityType.Person => Image.ImageType.Staff,
            ImageEntityType.Thumbnail => Image.ImageType.Thumb,
            _ => Image.ImageType.Staff,
        };

    public static DataSourceType ToServer(this Image.ImageSource source)
        => Enum.Parse<DataSourceType>(source.ToString());

    public static Image.ImageSource ToV3Dto(this DataSourceType source)
        => Enum.Parse<Image.ImageSource>(source.ToString());
}
