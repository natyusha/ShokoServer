using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.Enums;

#nullable enable
namespace Shoko.Plugin.Abstractions.DataModels;

public class AniDBMediaData
{
    /// <summary>
    /// Audio languages.
    /// </summary>
    public IReadOnlyList<TextLanguage> AudioLanguages { get; set; }

    /// <summary>
    /// Subtitle languages.
    /// </summary>
    public IReadOnlyList<TextLanguage> SubLanguages { get; set; }

    public AniDBMediaData(IEnumerable<TextLanguage> audio, IEnumerable<TextLanguage> sub)
    {
        AudioLanguages = audio is IReadOnlyList<TextLanguage> audioList ? audioList : audio.ToList();
        SubLanguages = sub is IReadOnlyList<TextLanguage> subList ? subList : sub.ToList();
    }
}

