using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.DataModels;

public interface IMediaInfo
{
    /// <summary>
    /// General title for the media.
    /// </summary>
    string? Title { get; }

    /// <summary>
    /// Overall duration of the media.
    /// </summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// Overall bit-rate across all streams in the media container.
    /// </summary>
    int BitRate { get; }

    /// <summary>
    /// Average frame-rate across all the streams in the media container.
    /// </summary>
    decimal FrameRate { get; }

    /// <summary>
    /// Date when encoding took place, if known.
    /// </summary>
    DateTime? Encoded { get; }

    /// <summary>
    /// True if the media is streaming-friendly.
    /// </summary>
    bool IsStreamable { get; }

    /// <summary>
    /// Common file extension for the media container format.
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// The media container format.
    /// </summary>
    string MediaContainer { get; }

    /// <summary>
    /// The media container format version.
    /// </summary>
    int MediaContainerVersion { get; }

    /// <summary>
    /// Video streams in the media container.
    /// </summary>
    IReadOnlyList<IVideoStream> Video { get; }

    /// <summary>
    /// Audio streams in the media container.
    /// </summary>
    IReadOnlyList<IAudioStream> Audio { get; }

    /// <summary>
    /// Sub-title (text) streams in the media container.
    /// </summary>
    IReadOnlyList<ITextStream> Subtitles { get; }

    /// <summary>
    /// Chapter information present in the media container.
    /// </summary>
    IReadOnlyList<IChapterInfo> Chapters { get; }
}

public interface IStream
{
    /// <summary>
    /// Local id for the stream.
    /// </summary>
    int ID { get; }

    /// <summary>
    /// Unique id for the stream.
    /// </summary>
    string UID { get; }

    /// <summary>
    /// Stream title, if available.
    /// </summary>
    string? Title { get; }

    /// <summary>
    /// Stream order.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// True if this is the default stream of the given type.
    /// </summary>
    bool IsDefault { get; }

    /// <summary>
    /// True if the stream is forced to be used.
    /// </summary>
    bool IsForced { get; }

    /// <summary>
    /// <see cref="TextLanguage"/> name of the language of the stream.
    /// </summary>
    TextLanguage? Language { get; }

    /// <summary>
    /// 3 character language code of the language of the stream.
    /// </summary>
    string? LanguageCode { get; }

    /// <summary>
    /// Stream codec information.
    /// </summary>
    IStreamCodecInfo Codec { get; }

    /// <summary>
    /// Stream format information.
    /// </summary>
    IStreamFormatInfo Format { get; }
}

public interface IStreamCodecInfo
{
    /// <summary>
    /// Codec name, if available.
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// Simplified codec id.
    /// </summary>
    string Simplified { get; }

    /// <summary>
    /// Raw codec id.
    /// </summary>
    string? Raw { get; }
}

public interface IStreamFormatInfo
{
    /// <summary>
    /// Name of the format used.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Profile name of the format used, if available.
    /// </summary>
    string? Profile { get; }

    /// <summary>
    /// Compression level of the format used, if available.
    /// </summary>
    string? Level { get; }

    /// <summary>
    /// Format settings, if available.
    /// </summary>
    string? Settings { get; }

    /// <summary>
    /// Known additional features enabled for the format, if available.
    /// </summary>
    string? AdditionalFeatures { get; }

    /// <summary>
    /// Format edianness, if available.
    /// </summary>
    string? Endianness { get; }

    /// <summary>
    /// Format tier, if available.
    /// </summary>
    string? Tier { get; }

    /// <summary>
    /// Format commercial information, if available.
    /// </summary>
    string? Commercial { get; }

    /// <summary>
    /// HDR format information, if available.
    /// </summary>
    /// <remarks>
    /// Only available for <see cref="VideoStreamInfo"/>.
    /// </remarks>
    string? HDR { get; }

    /// <summary>
    /// HDR format compatibility informaiton, if available.
    /// </summary>
    /// <remarks>
    /// Only available for <see cref="VideoStreamInfo"/>.
    /// </remarks>
    string? HDRCompatibility { get; }

    /// <summary>
    /// Context-adaptive binary arithmetic coding (CABAC).
    /// </summary>
    /// <remarks>
    /// Only available for <see cref="VideoStreamInfo"/>.
    /// </remarks>
    bool? CABAC { get; }

    /// <summary>
    /// Bi-direcitonal video object planes (BVOP).
    /// </summary>
    /// <remarks>
    /// Only available for <see cref="VideoStreamInfo"/>.
    /// </remarks>
    bool? BVOP { get; }

    /// <summary>
    /// Quarter-pixel motion (Qpel).
    /// </summary>
    /// <remarks>
    /// Only available for <see cref="VideoStreamInfo"/>.
    /// </remarks>
    bool? QPel { get; }

    /// <summary>
    /// Global Motion Compensation (GMC) mode, if available.
    /// </summary>
    /// <remarks>
    /// Only available for <see cref="VideoStreamInfo"/>.
    /// </remarks>
    string? GMC { get; }

    /// <summary>
    /// Reference frames count, if known.
    /// </summary>
    /// <remarks>
    /// Only available for <see cref="VideoStreamInfo"/>.
    /// </remarks>
    int? ReferenceFrames { get; }
}

public interface IVideoStream : IStream
{
    /// <summary>
    /// Width of the video stream.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Height of the video stream.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Standarized resolution.
    /// </summary>
    string Resolution { get; }

    /// <summary>
    /// Pixel aspect-ratio.
    /// </summary>
    decimal PixelAspectRatio { get; }

    /// <summary>
    /// Frame-rate.
    /// </summary>
    decimal FrameRate { get; }

    /// <summary>
    /// Frame-rate mode.
    /// </summary>
    string FrameRateMode { get; }

    /// <summary>
    /// Total number of frames in the video stream.
    /// </summary>
    int FrameCount { get; }

    /// <summary>
    /// Scan-type. Interlaced or progressive.
    /// </summary>
    string ScanType { get; }

    /// <summary>
    /// Color-space.
    /// </summary>
    string ColorSpace { get; }

    /// <summary>
    /// Chroma sub-sampling.
    /// </summary>
    string ChromaSubsampling { get; }

    /// <summary>
    /// Matrix co-efficients.
    /// </summary>
    string? MatrixCoefficients { get; }

    /// <summary>
    /// Bit-rate of the video stream.
    /// </summary>
    int BitRate { get; }

    /// <summary>
    /// Bit-depth of the video stream.
    /// </summary>
    int BitDepth { get; }
}

public interface IAudioStream : IStream
{
    /// <summary>
    /// Number of total channels in the audio stream.
    /// </summary>
    int Channels { get; }

    /// <summary>
    /// A text representation of the layout of the channels available in the
    /// audio stream.
    /// </summary>
    string ChannelLayout { get; }

    /// <summary>
    /// Samples per frame.
    /// </summary>
    int SamplesPerFrame { get; }

    /// <summary>
    /// Sampling rate of the audio.
    /// </summary>
    int SamplingRate { get; }

    /// <summary>
    /// Compression mode used.
    /// </summary>
    string CompressionMode { get; }

    /// <summary>
    /// Bit-rate of the audio-stream.
    /// </summary>
    int BitRate { get; }

    /// <summary>
    /// Bit-rate mode of the audio stream.
    /// </summary>
    string BitRateMode { get; }

    /// <summary>
    /// Bit-depth of the audio stream.
    /// </summary>
    int BitDepth { get; }
}

public interface ITextStream : IStream
{
    /// <summary>
    /// Sub-title of the text stream.
    /// </summary>
    /// <value></value>
    string? SubTitle { get; }

    /// <summary>
    /// Not From MediaInfo. Is this an external sub file
    /// </summary>
    bool IsExternal { get; }

    /// <summary>
    /// The name of the external subtitle file if this is stream is from an
    /// external source. This field is only sent if <see cref="IsExternal"/>
    /// is set to <code>true</code>.
    /// </summary>
    string? ExternalFilename { get; }
}

public interface IChapterInfo
{
    /// <summary>
    /// Chapter title.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Chapter timestamp.
    /// </summary>
    TimeSpan Timestamp { get; }
}
