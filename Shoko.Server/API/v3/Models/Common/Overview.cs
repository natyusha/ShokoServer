using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// Overview information.
/// </summary>
public class Overview
{
    /// <summary>
    /// the title
    /// </summary>
    [Required]
    public string Value { get; set; }

    /// <summary>
    /// convert to AniDB style (x-jat is the special one, but most are standard 3-digit short names)
    /// </summary>
    [Required]
    public string Language { get; set; }

    /// <summary>
    /// Indicates this is the default title for the entity.
    /// </summary>
    public bool Default { get; set; }

    /// <summary>
    /// Indicates this is the user preferred title.
    /// </summary>
    /// <value></value>
    public bool Preferred { get; set; }

    /// <summary>
    /// AniDB, TvDB, AniList, etc.
    /// </summary>
    [Required]
    public string Source { get; set; }

    public Overview(TMDB_Overview title, string mainDescription = null, TMDB_Overview preferredDescription = null)
    {
        Value = title.Value;
        Language = title.Language.GetString();
        Default = title.Language == TitleLanguage.English && !string.IsNullOrEmpty(mainDescription) && string.Equals(title.Value, mainDescription);
        Preferred = title.Equals(preferredDescription);
        Source = "TMDB";
    }
}
