using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Enums;

#nullable enable
namespace Shoko.Models.Server.TMDB;

public class TMDB_Image : IImageMetadata
{
    /// <summary>
    /// Local id for image.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    public string RemoteId { get; set; } = string.Empty;

    /// <summary>
    /// Related TMDB Show entry id, if applicable.
    /// </summary>
    /// <remarks>
    /// An image can be linked to multiple entries at once.
    /// </remarks>
    public string? ShowId { get; set; }

    /// <summary>
    /// Related TMDB Season entry id, if applicable.
    /// </summary>
    /// <remarks>
    /// An image can be linked to multiple entries at once.
    /// </remarks>
    public string? SeasonId { get; set; }

    /// <summary>
    /// Related TMDB Episode entry id, if applicable.
    /// </summary>
    /// <remarks>
    /// An image can be linked to multiple entries at once.
    /// </remarks>
    public string? EpisodeId { get; set; }

    /// <summary>
    /// Related TMDB Movie entry id, if applicable.
    /// </summary>
    /// <remarks>
    /// An image can be linked to multiple entries at once.
    /// </remarks>
    public string? MovieId { get; set; }

    /// <summary>
    /// Related TMDB Collection entry id, if applicable.
    /// </summary>
    /// <remarks>
    /// An image can be linked to multiple entries at once.
    /// </remarks>
    public string? CollectionId { get; set; }

    /// <summary>
    /// Foreign type. Determines if the data is for movies or tv shows, and if
    /// the tmdb id is for a show or movie.
    /// </summary>
    /// <value></value>
    public ForeignEntityType ForeignType { get; set; }

    /// <summary>
    /// Image type.
    /// </summary>
    public ImageEntityType ImageType { get; set; }

    /// <summary>
    /// Image size.
    /// </summary>
    public string ImageSize
        => $"{Width}x{Height}";

    /// <summary>
    /// Image aspect ratio.
    /// </summary>
    /// <value></value>
    public decimal AspectRatio { get; set; }

    /// <summary>
    /// Width of the image, in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Height of the image, in pixels.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Language code for the language used for the text in the image, if any.
    /// </summary>
    public string? LanguageCode { get; set; }

    /// <summary>
    /// The remote file name to fetch.
    /// </summary>
    public string RemoteFileName { get; set; } = string.Empty;

    /// <summary>
    /// Remote path relative a provided base to fetch the image.
    /// </summary>
    public string RemotePath
        => $"/{ImageSize}/{RemoteFileName}";

    /// <summary>
    /// Relative path to the image stored locally.
    /// </summary>
    public string LocalPath
        => $"/{ImageType}/{ImageSize}/{RemoteFileName}";

    /// <summary>
    /// Average user rating across all user votes.
    /// </summary>
    /// <remarks>
    /// May be used for ordering when aquiring and/or descarding images.
    /// </remarks>
    public decimal UserRating { get; set; }

    /// <summary>
    /// User votes.
    /// </summary>
    /// <remarks>
    /// May be used for ordering when aquiring and/or descarding images.
    /// </remarks>
    public int UserVotes { get; set; }

    public DataSource Source
        => DataSource.TMDB;
}
