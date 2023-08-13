using System.IO;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Enums;

#nullable enable
namespace Shoko.Models.FanartTV;

public class FanartTV_Image : IImageMetadata
{
    /// <summary>
    /// Local id for image.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Related TMDB entry id for the given <see cref="ForeignType"/>.
    /// </summary>
    public string TmdbId { get; set; } = string.Empty;

    /// <summary>
    /// Foreign entry id for the given <see cref="ForeignType"/>.
    /// </summary>
    public string ForeignId { get; set; } = string.Empty;

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
    /// True if this is considered an High Defninition image.
    /// </summary>
    public bool IsHD { get; set; }
    
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
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// The remote file name to fetch.
    /// </summary>
    public string RemoteFileName { get; set; } = string.Empty;

    /// <summary>
    /// Remote main-type used to construct the remote url.
    /// </summary>
    private string RemoteMainType
        => ForeignType == ForeignEntityType.Movie ? "movies" : "tv";

    /// <summary>
    /// Remote sub-type used to construct the remote url.
    /// </summary>
    private string RemoteSubType
        => (IsHD ? "hd" : "") + ForeignType.ToString().ToLowerInvariant() + ImageType.ToString().ToLower();

    /// <summary>
    /// Remote URL to fetch the image.
    /// </summary>
    public string RemotePath
        => $"/{RemoteMainType}/{ForeignId}/{RemoteSubType}/{RemoteFileName}";

    /// <summary>
    /// Relative path to the image stored locally.
    /// </summary>
    public string LocalPath
        => $"/{ForeignType.ToString()}/{ImageType.ToString()}/{(IsHD ? "HD" : "SD")}/${ForeignId}{System.IO.Path.GetExtension(RemoteFileName) ?? ".bin"}";

    /// <summary>
    /// User likes. May be used for ordering when aquiring and/or descarding images.
    /// </summary>
    public int UserLikes { get; set; }

    /// <summary>
    /// Disc number if <see cref="FanartTV_Image.ImageType"/> is <see cref="ImageEntityType.Disc"/>.
    /// </summary>
    public int? DiscNumber { get; set; }

    /// <summary>
    /// Disc type if <see cref="FanartTV_Image.ImageType"/> is <see cref="ImageEntityType.Disc"/>.
    /// </summary>
    public string? DiscType { get; set; }


    public DataSource Source
        => DataSource.Fanart;
}
