using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models.AniDB;

public interface IAniDBMediaInfo
{
    /// <summary>
    /// Audio languages.
    /// </summary>
    IReadOnlyList<TextLanguage> AudioLanguages { get; set; }

    /// <summary>
    /// Subtitle languages.
    /// </summary>
    IReadOnlyList<TextLanguage> SubtitleLanguages { get; set; }
}

