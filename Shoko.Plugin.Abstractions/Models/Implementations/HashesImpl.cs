
namespace Shoko.Plugin.Abstractions.Models.Implementations;

public class HashesImpl : IHashes
{
    private string _ed2k { get; set; }

    /// <inheritdoc/>
    public string ED2K
    {
        get => _ed2k;
        set => _ed2k = value.ToUpperInvariant();
    }

    private string? _crc32 { get; set; }

    /// <inheritdoc/>
    public string? CRC32
    {
        get => _crc32;
        set => _crc32 = value?.ToLowerInvariant();
    }

    private string? _md5 { get; set; }

    /// <inheritdoc/>
    public string? MD5
    {
        get => _md5;
        set => _md5 = value?.ToUpperInvariant();
    }

    private string? _sha1 { get; set; }

    /// <inheritdoc/>
    public string? SHA1
    {
        get => _sha1;
        set => _sha1 = value?.ToUpperInvariant();
    }

    public HashesImpl()
    {
        _ed2k = string.Empty;
        _crc32 = null;
        _md5 = null;
        _sha1 = null;
    }

    public HashesImpl(string ed2k, string? crc32 = null, string? md5 = null, string? sha1 = null)
    {
        _ed2k = ed2k;
        _crc32 = crc32;
        _md5 = md5;
        _sha1 = sha1;
    }
}
