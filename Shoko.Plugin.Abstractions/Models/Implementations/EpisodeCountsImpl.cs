
namespace Shoko.Plugin.Abstractions.Models.Implementations;

public class EpisodeCountsImpl : IEpisodeCounts
{
    /// <inheritdoc/>
    public int Unknown { get; set; }

    /// <inheritdoc/>
    public int Normal { get; set; }

    /// <inheritdoc/>
    public int ThemeSong { get; set; }

    /// <inheritdoc/>
    public int Special { get; set; }

    /// <inheritdoc/>
    public int Trailer { get; set; }

    /// <inheritdoc/>
    public int Parody { get; set; }

    /// <inheritdoc/>
    public int Other { get; set; }

    public EpisodeCountsImpl() { }
}
