
namespace Shoko.Plugin.Abstractions.DataModels;

public interface IHashes
{
    string ED2K { get; }
    string? CRC32 { get; }
    string? MD5 { get; }
    string? SHA1 { get; }
}
