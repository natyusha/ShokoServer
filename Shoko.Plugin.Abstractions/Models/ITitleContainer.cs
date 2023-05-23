using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.Models;

public interface ITitleContainer
{
    /// <summary>
    /// The preferred title of the episode, according to the language preference
    /// settings.
    /// </summary>
    ITitle PreferredTitle { get; }

    /// <summary>
    /// The original main title of the episode.
    /// </summary>
    ITitle MainTitle { get; }

    /// <summary>
    /// All available titles for the episode.
    /// </summary>
    IReadOnlyList<ITitle> Titles { get; }
}
