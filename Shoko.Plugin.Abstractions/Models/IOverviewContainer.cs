using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.Models;

public interface IOverviewContainer
{
    
    /// <summary>
    /// The preferred overview/description of the episode, according to the
    /// language preference settings.
    /// </summary>
    string PreferredOverview { get; }

    /// <summary>
    /// The original main overview/description for the episode.
    /// </summary>
    string MainOverview { get; }

    /// <summary>
    /// All available overviews/descriptions for the episode.
    /// </summary>
    IReadOnlyList<IText> Overviews { get; }

}
