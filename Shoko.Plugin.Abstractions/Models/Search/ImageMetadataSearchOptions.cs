
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models.Search;

public class ImageMetadataSearchOptions : BaseSearchOptions
{
    /// <summary>
    /// The image entity type to search for.
    /// </summary>
    /// <remarks>
    /// See <seealso cref="IImageMetadata.ImageType"/> for more info.
    /// </remarks>
    ImageEntityType? ImageType { get; set; }

    /// <summary>
    /// Indicates the search should (or should not) only contain preferred
    /// images.
    /// </summary>
    /// <remarks>
    /// See <seealso cref="IImageMetadata.IsPreferred"/> for more info.
    /// </remarks>
    public bool? IsPreferred { get; set; }

    /// <summary>
    /// Indicates the search should (or should not) only contain enabled images.
    /// </summary>
    /// <remarks>
    /// See <seealso cref="IImageMetadata.IsEnabled"/> for more info.
    /// </remarks>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Indicates the search should (or should not) only contain locked images.
    /// </summary>
    /// <remarks>
    /// See <seealso cref="IImageMetadata.IsLocked"/> for more info.
    /// </remarks>
    public bool? IsLocked { get; set; }

    /// <summary>
    /// Indicates the search should (or should not) only contain images which
    /// is readily available.
    /// </summary>
    /// <remarks>
    /// See <seealso cref="IImageMetadata.IsAvailable"/> for more info.
    /// </remarks>
    public bool? IsAvailable { get; set; }

    /// <summary>
    /// Search only for images of a spesific aspect ratio.
    /// </summary>
    decimal? AspectRatio { get; set; }

    /// <summary>
    /// Search only for images of a spesific width.
    /// </summary>
    int? Width { get; set; }

    /// <summary>
    /// Search only for images of a spesific height.
    /// </summary>
    int? Height { get; set; }

    /// <summary>
    /// The language code to search for.
    /// </summary>
    /// <remarks>
    /// If both <see cref="LanguageCode"/> and <see cref="Language"/> is
    /// provided then <see cref="Language"/> takes precedence and
    /// <see cref="LanguageCode"/> is ignored.
    /// <br/>
    /// See <seealso cref="IImageMetadata.LanguageCode"/> for more info.
    /// </remarks>
    string? LanguageCode { get; set; }

    /// <summary>
    /// The language to search for.
    /// </summary>
    /// <remarks>
    /// If both <see cref="LanguageCode"/> and <see cref="Language"/> is
    /// provided then <see cref="Language"/> takes precedence and
    /// <see cref="LanguageCode"/> is ignored.
    /// <br/>
    /// See <seealso cref="IImageMetadata.Language"/> for more info.
    /// </remarks>
    TextLanguage? Language { get; set; }
}
