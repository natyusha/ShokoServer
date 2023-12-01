using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.AniDB.Titles;

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
    public string Name { get; set; }

    /// <summary>
    /// convert to AniDB style (x-jat is the special one, but most are standard 3-digit short names)
    /// </summary>
    [Required]
    public string Language { get; set; }

    /// <summary>
    /// Title Type
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public TitleType Type { get; set; }

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

    public Title(SVR_AniDB_Anime_Title title, string mainTitle = null, string preferredTitle = null)
    {
        Name = title.Title;
        Language = title.LanguageCode;
        Type = title.TitleType;
        Default = !string.IsNullOrEmpty(mainTitle) && string.Equals(title.Title, mainTitle);
        Preferred = !string.IsNullOrEmpty(preferredTitle) && string.Equals(title.Title, preferredTitle);
        Source = "AniDB";
    }

    public Title(ResponseAniDBTitles.Anime.AnimeTitle title, string mainTitle = null, string preferredTitle = null)
    {
        Name = title.Title;
        Language = title.LanguageCode;
        Type = title.TitleType;
        Default = !string.IsNullOrEmpty(mainTitle) && string.Equals(title.Title, mainTitle);
        Preferred = !string.IsNullOrEmpty(preferredTitle) && string.Equals(title.Title, preferredTitle);
        Source = "AniDB";
    }

    public Title(SVR_AniDB_Episode_Title title, SVR_AniDB_Episode_Title preferredTitle = null)
    {
        Name = title.Title;
        Language = title.LanguageCode;
        Type = TitleType.None;
        Default = false;
        Preferred = preferredTitle != null && title.AniDB_Episode_TitleID == preferredTitle.AniDB_Episode_TitleID;
        Source = "AniDB";
    }

    public Title(TMDB_Title title, TMDB_Title mainTitle = null, TMDB_Title preferredTitle = null)
    {
        Name = title.Value;
        Language = title.Language.GetString();
        Type = TitleType.Official;
        Default = mainTitle != null && title.TMDB_TitleID == mainTitle.TMDB_TitleID;
        Preferred = preferredTitle != null && title.TMDB_TitleID == preferredTitle.TMDB_TitleID;
        Source = "TMDB";
    }
}
