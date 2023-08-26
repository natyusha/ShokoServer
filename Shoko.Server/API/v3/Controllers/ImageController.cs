using System.IO;
using Microsoft.AspNetCore.Mvc;
using Shoko.Models.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Properties;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Mime = MimeMapping.MimeUtility;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
public class ImageController : BaseController
{
    private const string ImageNotFound = "The requested resource does not exist.";

    /// <summary>
    /// Returns the image for the given <paramref name="source"/>, <paramref name="type"/> and <paramref name="value"/>.
    /// </summary>
    /// <param name="source">AniDB, TvDB, TMDB, Shoko, etc.</param>
    /// <param name="type">Poster, Fanart, Banner, Thumbnail, etc.</param>
    /// <param name="value">The image ID.</param>
    /// <returns>200 on found, 400/404 if the type or source are invalid, and 404 if the id is not found</returns>
    [HttpGet("{source}/{type}/{value}")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(404)]
    public ActionResult GetImage([FromRoute] Image.ImageSource source, [FromRoute] Image.ImageType type,
        [FromRoute] int value)
    {
        // Unrecognised combination of source, type and/or value.
        var dataSource = source.ToServer();
        var imageEntityType = type.ToServer();
        if (imageEntityType == ImageEntityType.None || dataSource == DataSourceType.None || value <= 0)
            return NotFound(ImageNotFound);

        // User avatars are stored in the database.
        if (imageEntityType == ImageEntityType.Art && dataSource == DataSourceType.User)
        {
            var user = RepoFactory.JMMUser.GetByID(value);
            if (!user.HasAvatarImage)
                return NotFound(ImageNotFound);

            return File(user.AvatarImageBlob, user.AvatarImageMetadata.ContentType);
        }

        // All other type/source combinations
        var path = Image.GetImagePath(imageEntityType, dataSource, value);
        if (string.IsNullOrEmpty(path))
            return NotFound(ImageNotFound);

        return File(System.IO.File.OpenRead(path), Mime.GetMimeMapping(path));
    }

    /// <summary>
    /// Returns a random image for the <paramref name="imageType"/>.
    /// </summary>
    /// <param name="imageType">Poster, Fanart, Banner, Thumb, Static</param>
    /// <returns>200 on found, 400/404 if the type or source are invalid, and 404 if the id is not found</returns>
    [HttpGet("Random/{imageType}")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public ActionResult GetRandomImageForType([FromRoute] Image.ImageType imageType)
    {
        if (imageType == Image.ImageType.Avatar)
            return ValidationProblem("Unsupported image type for random image.", "imageType");

        var dataSource = Image.GetRandomImageSource(imageType);
        var imageEntityType = imageType.ToServer();
        if (imageEntityType == ImageEntityType.None)
            return InternalError("Could not generate a valid image type to fetch.");

        var id = Image.GetRandomImageID(imageEntityType, dataSource);
        if (!id.HasValue)
            return InternalError("Unable to find a random image to send.");

        var path = Image.GetImagePath(imageEntityType, dataSource, id.Value);
        if (string.IsNullOrEmpty(path))
            return InternalError("Unable to load image from disk.");

        return File(System.IO.File.OpenRead(path), Mime.GetMimeMapping(path));
    }

    /// <summary>
    /// Returns the metadata for a random image for the <paramref name="imageType"/>.
    /// </summary>
    /// <param name="imageType">Poster, Fanart, Banner, Thumb</param>
    /// <returns>200 on found, 400 if the type or source are invalid</returns>
    [HttpGet("Random/{imageType}/Metadata")]
    [ProducesResponseType(typeof(Image), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public ActionResult<Image> GetRandomImageMetadataForType([FromRoute] Image.ImageType imageType)
    {
        if (imageType == Image.ImageType.Avatar)
            return ValidationProblem("Unsupported image type for random image.", "imageType");

        var dataSource = Image.GetRandomImageSource(imageType);
        var imageEntityType = imageType.ToServer();
        if (imageEntityType == ImageEntityType.None)
            return InternalError("Could not generate a valid image type to fetch.");

        // Try 5 times to get a valid image.
        var tries = 0;
        do
        {
            var id = Image.GetRandomImageID(imageEntityType, dataSource);
            if (!id.HasValue)
                break;

            var path = Image.GetImagePath(imageEntityType, dataSource, id.Value);
            if (string.IsNullOrEmpty(path))
                continue;

            var image = new Image(id.Value, imageEntityType, dataSource, false, false);
            var series = Image.GetFirstSeriesForImage(imageEntityType, dataSource, id.Value);
            if (series == null)
                continue;
            image.Series = new(series.AnimeSeriesID, series.GetSeriesName());

            return image;
        } while (tries++ < 5);

        return InternalError("Unable to find a random image to send.");
    }

    public ImageController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
