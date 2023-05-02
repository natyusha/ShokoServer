using System.Collections.Generic;

#nullable enable
namespace Shoko.Plugin.Abstractions.DataModels;

public interface ITitleContainer
{
    /// <summary>
    /// The preferred title of the episode, according to the language preference
    /// settings.
    /// </summary>
    string PreferredTitle { get; }

    /// <summary>
    /// The original main title of the episode.
    /// </summary>
    string MainTitle { get; }

    /// <summary>
    /// All available titles for the episode.
    /// </summary>
    IReadOnlyList<ITitle> Titles { get; }
}
