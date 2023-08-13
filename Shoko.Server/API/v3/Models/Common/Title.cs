using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// Title object, stores the title, type, language, and source
/// if using a TvDB title, assume "eng:official". If using AniList, assume "x-jat:main"
/// AniDB's MainTitle is "x-jat:main"
/// </summary>
public class Title
{
    /// <summary>
    /// the title
    /// </summary>
    [Required]
    public string Name { get; }

    /// <summary>
    /// convert to AniDB style (x-jat is the special one, but most are standard 3-digit short names)
    /// </summary>
    [Required]
    public string Language { get; }

    /// <summary>
    /// AniDB type
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public TitleType Type { get; }

    /// <summary>
    /// If this is the default title
    /// </summary>
    public bool Default { get; }

    /// <summary>
    /// AniDB, TvDB, AniList, etc
    /// </summary>
    [Required]
    public string Source { get; }
    
    public Title(ITitle title)
    {
        Name = title.Value;
        Language = title.LanguageCode;
        Type = title.Type;
        Default = title.IsPreferred;
        Source = title.DataSource.ToString();
    }
}
