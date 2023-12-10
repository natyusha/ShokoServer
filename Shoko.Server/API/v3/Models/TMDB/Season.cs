
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Shoko.Models.Enums;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.API.v3.Models.TMDB;

/// <summary>
/// APIv3 The Movie DataBase (TMDB) Season Data Transfer Object (DTO).
/// </summary>
public class Season
{
    /// <summary>
    /// TMDB Season ID.
    /// </summary>
    public string ID;

    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    public int ShowID;

    /// <summary>
    /// The alternate ordering this season is assosiated with. Will be null
    /// for main series seasons.
    /// </summary>
    public string? AlternateOrderingID;

    /// <summary>
    /// Preferred title based upon series title preference.
    /// </summary>
    public string Title;

    /// <summary>
    /// All available titles for the show, if they should be included.
    /// /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Title>? Titles;

    /// <summary>
    /// Preferred overview based upon series title preference.
    /// </summary>
    public string Overview;

    /// <summary>
    /// All available overviews for the show, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Overview>? Overviews;

    /// <summary>
    /// Images assosiated with the season, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Images? Images;

    /// <summary>
    /// The season number for the main ordering or alternate ordering in use.
    /// </summary>
    public int SeasonNumber;

    /// <summary>
    /// Count of episodes assosiated with the season.
    /// </summary>
    public int EpisodeCount;

    /// <summary>
    /// Indicates the alternate ordering season is locked. Will not be set if
    /// <seealso cref="AlternateOrderingID"/> is not set.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsLocked;

    /// <summary>
    /// When the local metadata was first created.
    /// </summary>
    public DateTime CreatedAt;

    /// <summary>
    /// When the local metadata was last updated with new changes from the
    /// remote.
    /// </summary>
    public DateTime LastUpdatedAt;

    public Season(TMDB_Season season, bool includeTitles = false, bool includeOverviews = false, bool includeImages = false)
    {
        var preferredOverview = season.GetPreferredOverview(true);
        var preferredTitle = season.GetPreferredTitle(true);

        ID = season.TmdbSeasonID.ToString();
        ShowID = season.TmdbShowID;
        AlternateOrderingID = null;
        Title = preferredTitle!.Value;
        if (includeTitles)
            Titles = season.GetAllTitles()
                .Select(title => new Title(title, season.EnglishTitle, preferredTitle))
                .OrderByDescending(title => title.Preferred)
                .ThenByDescending(title => title.Default)
                .ThenBy(title => title.Language)
                .ToList();
        Overview = preferredOverview!.Value;
        if (includeOverviews)
            Overviews = season.GetAllOverviews()
                .Select(title => new Overview(title, season.EnglishOverview, preferredOverview))
                .OrderByDescending(title => title.Preferred)
                .ThenByDescending(title => title.Default)
                .ThenBy(title => title.Language)
                .ToList();
        if (includeImages)
            Images = GetImages(season);
        SeasonNumber = season.SeasonNumber;
        EpisodeCount = season.EpisodeCount;
        IsLocked = null;
        CreatedAt = season.CreatedAt.ToUniversalTime();
        LastUpdatedAt = season.LastUpdatedAt.ToUniversalTime();
    }

    public Season(TMDB_AlternateOrdering_Season season, bool includeTitles = false, bool includeOverviews = false, bool includeImages = false)
    {
        ID = season.TmdbEpisodeGroupID;
        ShowID = season.TmdbShowID;
        AlternateOrderingID = season.TmdbEpisodeGroupCollectionID;
        Title = season.EnglishTitle;
        if (includeTitles)
            Titles = Array.Empty<Title>();
        Overview = string.Empty;
        if (includeOverviews)
            Overviews = Array.Empty<Overview>();
        if (includeImages)
            Images = new();
        SeasonNumber = season.SeasonNumber;
        EpisodeCount = season.EpisodeCount;
        IsLocked = season.IsLocked;
        CreatedAt = season.CreatedAt.ToUniversalTime();
        LastUpdatedAt = season.LastUpdatedAt.ToUniversalTime();
    }

    public static Images GetImages(TMDB_Season season)
    {
        var images = new Images();
        foreach (var image in season.GetImages())
        {
            var dto = new Image(image.TMDB_ImageID, image.ImageType, DataSourceType.TMDB, false, !image.IsEnabled);
            switch (image.ImageType)
            {
                case Server.ImageEntityType.Poster:
                    images.Posters.Add(dto);
                    break;
                case Server.ImageEntityType.Banner:
                    images.Banners.Add(dto);
                    break;
                case Server.ImageEntityType.Backdrop:
                    images.Fanarts.Add(dto);
                    break;
                case Server.ImageEntityType.Logo:
                    images.Logos.Add(dto);
                    break;
                default:
                    break;
            }
        }
        return images;
    }
}
