using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models;

public class AniDBMediaData
{
    /// <summary>
    /// Audio languages.
    /// </summary>
    public IReadOnlyList<TextLanguage> AudioLanguages { get; set; }

    /// <summary>
    /// Subtitle languages.
    /// </summary>
    public IReadOnlyList<TextLanguage> SubtitleLanguages { get; set; }

    public AniDBMediaData(IEnumerable<TextLanguage> audio, IEnumerable<TextLanguage> sub)
    {
        AudioLanguages = audio is IReadOnlyList<TextLanguage> audioList ? audioList : audio.ToList();
        SubtitleLanguages = sub is IReadOnlyList<TextLanguage> subList ? subList : sub.ToList();
    }
}

