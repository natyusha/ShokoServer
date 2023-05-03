using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models.AniDB;

namespace Shoko.Plugin.Abstractions.Models.Implementations;

public class AniDBMediaInfoImpl : IAniDBMediaInfo
{
    /// <summary>
    /// Audio languages.
    /// </summary>
    public IReadOnlyList<TextLanguage> AudioLanguages { get; set; }

    /// <summary>
    /// Subtitle languages.
    /// </summary>
    public IReadOnlyList<TextLanguage> SubtitleLanguages { get; set; }

    public AniDBMediaInfoImpl(IEnumerable<TextLanguage> audio, IEnumerable<TextLanguage> sub)
    {
        AudioLanguages = audio is IReadOnlyList<TextLanguage> audioList ? audioList : audio.ToList();
        SubtitleLanguages = sub is IReadOnlyList<TextLanguage> subList ? subList : sub.ToList();
    }
}

