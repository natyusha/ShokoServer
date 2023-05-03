
namespace Shoko.Plugin.Abstractions.Models;

/// <summary>
///
/// </summary>
public interface IEpisodeCounts
{
    /// <summary>
    ///
    /// </summary>
    int Unknown { get; }

    /// <summary>
    ///
    /// </summary>
    int Normal { get; }

    /// <summary>
    ///
    /// </summary>
    int ThemeSong { get; }

    /// <summary>
    ///
    /// </summary>
    int Special { get; }

    /// <summary>
    ///
    /// </summary>
    int Trailer { get; }

    /// <summary>
    ///
    /// </summary>
    int Parody { get; }

    /// <summary>
    ///
    /// </summary>
    int Other { get; }
}
