
#nullable enable
namespace Shoko.Plugin.Abstractions.DataModels.Implementations;

public class HashesImpl : IHashes
{
    /// <inheritdoc/>
    public string ED2K { get; set; }

    /// <inheritdoc/>
    public string? CRC32 { get; set; }

    /// <inheritdoc/>
    public string? MD5 { get; set; }

    /// <inheritdoc/>
    public string? SHA1 { get; set; }

    public HashesImpl()
    {
        ED2K = string.Empty;
        CRC32 = null;
        MD5 = null;
        SHA1 = null;
    }

    public HashesImpl(string ed2k, string? crc32 = null, string? md5 = null, string? sha1 = null)
    {
        ED2K = ed2k;
        CRC32 = crc32;
        MD5 = md5;
        SHA1 = sha1;
    }
}
