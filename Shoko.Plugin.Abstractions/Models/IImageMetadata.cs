using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models;

public interface IImageMetadata : IMetadata
{
    /// <summary>
    /// Image type.
    /// </summary>
    ImageEntityType ImageType { get; }

    /// <summary>
    /// Indicates the image is the default for the given <see cref="ImageType"/>
    /// for the linked entry.
    /// </summary>
    public bool IsDefault { get; }

    /// <summary>
    /// Indicates the image is enabled for use. Disabled images should not be
    /// used except for administritive purposes.
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// Indicates the image is locked and cannot be removed by the user. It can
    /// still be disabled though.
    /// </summary>
    public bool IsLocked { get; }

    /// <summary>
    /// Image aspect ratio.
    /// </summary>
    /// <value></value>
    decimal AspectRatio { get; }

    /// <summary>
    /// Width of the image, in pixels.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Height of the image, in pixels.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Language code for the language used for the text in the image, if any.
    /// </summary>
    string? LanguageCode { get; }

    /// <summary>
    /// The language used for any text in the image, if any.
    /// </summary>
    TextLanguage? Language { get; }

    /// <summary>
    /// Remote path relative a provided base to fetch the image.
    /// </summary>
    string RemotePath { get; }

    /// <summary>
    /// Local path relative to the image directory for the provider.
    /// </summary>
    string LocalPath { get; }
}
