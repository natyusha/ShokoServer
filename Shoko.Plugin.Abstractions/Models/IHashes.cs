
namespace Shoko.Plugin.Abstractions.Models;

/// <summary>
/// 
/// </summary>
public interface IHashes
{
    /// <summary>
    /// 
    /// </summary>
    string ED2K { get; }

    /// <summary>
    /// 
    /// </summary>
    string? CRC32 { get; }

    /// <summary>
    /// 
    /// </summary>
    string? MD5 { get; }

    /// <summary>
    /// 
    /// </summary>
    string? SHA1 { get; }
}
