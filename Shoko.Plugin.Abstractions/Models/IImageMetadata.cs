using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models;

public interface IImageMetadata : IMetadata<string>
{
    /// <summary>
    /// Image type.
    /// </summary>
    ImageEntityType ImageType { get; }

    /// <summary>
    /// Indicates the image is the preferred for the given
    /// <see cref="ImageType"/> for the linked entry.
    /// </summary>
    public bool IsPreferred { get; }

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
    /// Indicates the image is readily available.
    /// </summary>
    public bool IsAvailable { get; }

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
    /// Or "none" if the image doesn't contain any language spesifics.
    /// </summary>
    string LanguageCode { get; }

    /// <summary>
    /// The language used for any text in the image, if any.
    /// Or <see cref="TextLanguage.None"/> if the image doesn't contain any
    /// language spesifics.
    /// </summary>
    TextLanguage Language { get; }

    /// <summary>
    /// A full remote URL to fetch the image, if the provider uses remote
    /// images.
    /// </summary>
    string? RemoteURL { get; }

    /// <summary>
    /// Local absolute path to where the image is stored. Will be null if the
    /// image is currently not locally available.
    /// </summary>
    string? LocalPath { get; }
    
    /// <summary>
    /// Get a stream that reads the image contents from the local copy or remote
    /// copy of the image. Returns null if the image is currently unavailable.
    /// </summary>
    System.IO.Stream? GetStream();
}
