using System.IO;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Server;
using TMDbLib.Objects.General;

#nullable enable
namespace Shoko.Models.Server.TMDB;

public class TMDB_ImageMetadata
{
    /// <summary>
    /// Local id for image.
    /// </summary>
    public int TMDB_ImageMetadataID { get; set; }

    /// <summary>
    /// Related TMDB Movie entry id, if applicable.
    /// </summary>
    /// <remarks>
    /// An image can be linked to multiple entries at once.
    /// </remarks>
    public int? TmdbMovieID { get; set; }

    /// <summary>
    /// Related TMDB Episode entry id, if applicable.
    /// </summary>
    /// <remarks>
    /// An image can be linked to multiple entries at once.
    /// </remarks>
    public int? TmdbEpisodeID { get; set; }

    /// <summary>
    /// Related TMDB Season entry id, if applicable.
    /// </summary>
    /// <remarks>
    /// An image can be linked to multiple entries at once.
    /// </remarks>
    public string? TmdbSeasonID { get; set; }

    /// <summary>
    /// Related TMDB Show entry id, if applicable.
    /// </summary>
    /// <remarks>
    /// An image can be linked to multiple entries at once.
    /// </remarks>
    public int? TmdbShowID { get; set; }

    /// <summary>
    /// Related TMDB Collection entry id, if applicable.
    /// </summary>
    /// <remarks>
    /// An image can be linked to multiple entries at once.
    /// </remarks>
    public int? TmdbCollectionID { get; set; }

    /// <summary>
    /// Foreign type. Determines if the data is for movies or tv shows, and if
    /// the tmdb id is for a show or movie.
    /// </summary>
    public ForeignEntityType ForeignType { get; set; }

    /// <summary>
    /// Image type.
    /// </summary>
    public ImageEntityType_New ImageType { get; set; }

    /// <summary>
    /// Indicates that the image is enabled for use.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Image size.
    /// </summary>
    public string ImageSize
        => $"{Width}x{Height}";

    /// <summary>
    /// Image aspect ratio.
    /// </summary>
    public double AspectRatio { get; set; }

    /// <summary>
    /// Width of the image, in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Height of the image, in pixels.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// The title language detrived from the <see cref="LanguageCode"/> below.
    /// </summary>
    public TitleLanguage Language { get; set; }

    /// <summary>
    /// Language code for the language used for the text in the image, if any.
    /// </summary>
    public string? LanguageCode
    {
        get => Language == TitleLanguage.None ? null : Language.GetString();
        set => Language = string.IsNullOrEmpty(value) ? TitleLanguage.None : value.GetTitleLanguage();
    }

    /// <summary>
    /// The remote file name to fetch.
    /// </summary>
    public string RemoteFileName { get; set; } = string.Empty;

    /// <summary>
    /// Remote path relative a provided base to fetch the image.
    /// </summary>
    public string RemoteURL
        => string.IsNullOrEmpty(RemoteFileName) ? string.Empty : $"/original/{RemoteFileName}";

    /// <summary>
    /// Absolute path to the image stored locally.
    /// </summary>
    public string RelativePath
        => string.IsNullOrEmpty(RemoteFileName) ? string.Empty : Path.Join("TMDB", ImageType.ToString(), RemoteFileName);

    public string AbsolutePath
        => ImageUtils.ResolvePath(RelativePath);

    /// <summary>
    /// Average user rating across all user votes.
    /// </summary>
    /// <remarks>
    /// May be used for ordering when aquiring and/or descarding images.
    /// </remarks>
    public double UserRating { get; set; }

    /// <summary>
    /// User votes.
    /// </summary>
    /// <remarks>
    /// May be used for ordering when aquiring and/or descarding images.
    /// </remarks>
    public int UserVotes { get; set; }

    public TMDB_ImageMetadata() { }

    public TMDB_ImageMetadata(ImageEntityType_New type)
    {
        ImageType = type;
    }

    public void Populate(ImageData data, ForeignEntityType foreignType, int foreignId)
    {
        Populate(data);
        switch (foreignType)
        {
            case ForeignEntityType.Collection:
                TmdbCollectionID = foreignId;
                ForeignType |= foreignType;
                break;
            case ForeignEntityType.Episode:
                TmdbEpisodeID = foreignId;
                ForeignType |= foreignType;
                break;
            case ForeignEntityType.Movie:
                TmdbMovieID = foreignId;
                ForeignType |= foreignType;
                break;
            case ForeignEntityType.Show:
                TmdbShowID = foreignId;
                ForeignType |= foreignType;
                break;
        }
    }

    public void Populate(ImageData data, ForeignEntityType foreignType, string foreignId)
    {
        Populate(data);
        switch (foreignType)
        {
            case ForeignEntityType.Season:
                TmdbSeasonID = foreignId;
                ForeignType |= foreignType;
                break;
        }
    }

    private void Populate(ImageData data)
    {
        RemoteFileName = data.FilePath;
        AspectRatio = data.AspectRatio;
        Width = data.Width;
        Height = data.Height;
        LanguageCode = data.Iso_639_1;
        UserRating = data.VoteAverage;
        UserVotes = data.VoteCount;
    }
}
