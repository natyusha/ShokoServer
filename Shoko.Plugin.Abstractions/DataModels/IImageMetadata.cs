using System;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;

namespace Shoko.Plugin.Abstractions.DataModels;

public interface IImageMetadata
{
    /// <summary>
    /// Local id for image.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Image type.
    /// </summary>
    ImageEntityType ImageType { get; }

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
    /// Title language.
    /// </summary>
    TextLanguage? Language
      => LanguageCode?.ToTextLanguage();

    /// <summary>
    /// The remote file name to fetch.
    /// </summary>
    string RemoteFileName { get; }

    /// <summary>
    /// Remote path relative a provided base to fetch the image.
    /// </summary>
    string RemotePath { get; }

    /// <summary>
    /// Local path relative to the image directory for the provider.
    /// </summary>
    string LocalPath { get; }

    DataSource Source { get; }
}

public enum ImageEntityType {
    Backdrop = 0,
    Banner = 1,
    Logo = 2,
    Art = 3,
    Disc = 4,
    Poster = 5,
    Thumbnail = 6,
}

[Flags]
public enum ForeignEntityType {
    None = 0,
    Collection = 1,
    Movie = 2,
    Show = 4,
    Season = 8,
    Episode = 16,
    Company = 32,
    Studio = 64,
    Network = 128,
    Person = 256,
    Character = 512,
}
