
using System;
using System.IO;
using System.Net.Http;
using ImageMagick;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;

namespace Shoko.Plugin.Abstractions.Models.Implementations;

public class ImageMetadataImpl : IImageMetadata
{
    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public ImageEntityType ImageType { get; }

    /// <inheritdoc/>
    public bool IsDefault { get; set; }

    /// <inheritdoc/>
    public bool IsEnabled { get; set; }

    /// <inheritdoc/>
    public bool IsLocked
    {
        get => true;
    }

    /// <inheritdoc/>
    public bool IsAvailable
    {
        get => !string.IsNullOrEmpty(RemoteURL) || !string.IsNullOrEmpty(Path);
    }

    private decimal? _aspectRatio { get; set; }

    /// <inheritdoc/>
    public decimal AspectRatio
    {
        get
        {
            if (_aspectRatio.HasValue)
                return _aspectRatio.Value;

            RefreshMetadata();

            return _aspectRatio ?? 0;
        }
    }

    private int? _width { get; set; }

    /// <inheritdoc/>
    public int Width
    {
        get
        {
            if (_width.HasValue)
                return _width.Value;

            RefreshMetadata();

            return _width ?? 0;
        }
    }

    private int? _height { get; set; }

    /// <inheritdoc/>
    public int Height
    {
        get
        {
            if (_height.HasValue)
                return _height.Value;

            RefreshMetadata();

            return _height ?? 0;
        }
    }

    /// <inheritdoc/>
    public string? LanguageCode
    {
        get => Language?.ToLanguageCode();
        set => Language = value?.ToTextLanguage();
    }

    /// <inheritdoc/>
    public TextLanguage? Language { get; set; }

    private string? _remoteURL { get; set; }

    /// <inheritdoc/>
    public string? RemoteURL
    {
        get => _remoteURL;
        set
        {
            _aspectRatio = null;
            _width = null;
            _height = null;
            _remoteURL = value;
        }
    }

    private string? _localPath { get; set; }

    /// <inheritdoc/>
    public string? Path
    {
        get => _localPath;
        set
        {
            _aspectRatio = null;
            _width = null;
            _height = null;
            _localPath = value;
        }
    }

    /// <inheritdoc/>
    public DataSource DataSource { get; }

    public ImageMetadataImpl(DataSource source, ImageEntityType type, string id, string? localPath = null, string? remoteURL = null)
    {

        Id = id;
        ImageType = type;
        IsDefault = false;
        IsEnabled = false;
        RemoteURL = remoteURL;
        Path = localPath;
        DataSource = source;
    }

    private void RefreshMetadata()
    {
        try
        {
            var stream = GetStream();
            if (stream == null)
            {
                _width = 0;
                _height = 0;
                _aspectRatio = 0;
                return;
            }

            var info = new MagickImageInfo(stream);
            if (info == null)
            {
                _width = 0;
                _height = 0;
                _aspectRatio = 0;
                return;
            }

            _width = info.Width;
            _height = info.Height;
            _aspectRatio = Math.Round((decimal)(info.Height / info.Width), 2);
        }
        catch
        {
            _width = 0;
            _height = 0;
            _aspectRatio = 0;
            return;
        }
    }

    public Stream? GetStream()
    {
        if (!string.IsNullOrEmpty(_localPath) && File.Exists(_localPath))
            return new FileStream(_localPath, FileMode.Open, FileAccess.Read);

        if (!string.IsNullOrEmpty(_remoteURL))
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", $"ShokoServer/v4");
                return client.GetStreamAsync(_remoteURL).ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }

        return null;
    }
}
