using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;

#nullable enable
namespace Shoko.Server.API.v3.Models.TMDB;

/// <summary>
/// APIv3 The Movie DataBase (TMDB) Show Data Transfer Object (DTO)
/// </summary>
public class Show
{
    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    public int ID;

    /// <summary>
    /// Preferred title based upon series title preference.
    /// </summary>
    public string Title;

    /// <summary>
    /// All available titles, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Title>? Titles;

    /// <summary>
    /// Preferred overview based upon episode title preference.
    /// </summary>
    public string Overview;

    /// <summary>
    /// All available overviews for the series, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Overview>? Overviews;

    [JsonConverter(typeof(StringEnumConverter))]
    public TitleLanguage OriginalLanguage;

    /// <summary>
    /// Indicates the show is restricted to an age group above the legal age,
    /// because it's a pornography.
    /// </summary>
    public bool IsRestricted;

    /// <summary>
    /// User rating of the show from TMDB users.
    /// </summary>
    public Rating UserRating;

    /// <summary>
    /// Genres.
    /// </summary>
    public IReadOnlyList<string> Genres;

    /// <summary>
    /// Content ratings for different countries for this show.
    /// </summary>
    public IReadOnlyList<ContentRating> ContentRatings;

    /// <summary>
    /// The production companies (studios) that produced the show.
    /// </summary>
    public IReadOnlyList<Studio> Studios;

    /// <summary>
    /// Images assosiated with the show, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Images? Images;

    /// <summary>
    /// Count of episodes assosiated with the show.
    /// </summary>
    public int EpisodeCount;

    /// <summary>
    /// Count of seasons assosiated with the show.
    /// </summary>
    public int SeasonCount;

    /// <summary>
    /// Count of locally alternate ordering schemes assosiated with the show.
    /// </summary>
    public int AlternateOrderingCount;

    /// <summary>
    /// All available ordering for the show, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<OrderingInformation>? Ordering;

    /// <summary>
    /// The date the first episode aired at, if it is known.
    /// </summary>
    public DateOnly? FirstAiredAt;

    /// <summary>
    /// The date the last episode aired at, if it is known.
    /// </summary>
    public DateOnly? LastAiredAt;

    /// <summary>
    /// When the local metadata was first created.
    /// </summary>
    public DateTime CreatedAt;

    /// <summary>
    /// When the local metadata was last updated with new changes from the
    /// remote.
    /// </summary>
    public DateTime LastUpdatedAt;

    public Show(TMDB_Show show, bool includeTitles = true, bool includeOverviews = true, bool includeOrdering = false, bool includeImages = false) :
        this(show, null, includeTitles, includeOverviews, includeOrdering, includeImages)
    { }

    public Show(TMDB_Show show, TMDB_AlternateOrdering? alternateOrdering, bool includeTitles = true, bool includeOverviews = true, bool includeOrdering = false, bool includeImages = false)
    {
        var preferredOverview = show.GetPreferredOverview(true);
        var preferredTitle = show.GetPreferredTitle(true);

        ID = show.TmdbShowID;
        Title = preferredTitle!.Value;
        if (includeTitles)
            Titles = show.GetAllTitles()
                .Select(title => new Title(title, show.EnglishTitle, preferredTitle))
                .OrderByDescending(title => title.Preferred)
                .ThenByDescending(title => title.Default)
                .ThenBy(title => title.Language)
                .ToList();

        Overview = preferredOverview!.Value;
        if (includeOverviews)
            Overviews = show.GetAllOverviews()
                .Select(title => new Overview(title, show.EnglishOverview, preferredOverview))
                .OrderByDescending(title => title.Preferred)
                .ThenByDescending(title => title.Default)
                .ThenBy(title => title.Language)
                .ToList();
        OriginalLanguage = show.OriginalLanguage;
        IsRestricted = show.IsRestricted;
        UserRating = new()
        {
            Value = (decimal)show.UserRating,
            MaxValue = 10,
            Votes = show.UserVotes,
            Source = "TMDB",
        };
        Genres = show.Genres;
        ContentRatings = show.ContentRatings
            .Select(contentRating => new ContentRating(contentRating))
            .ToList();
        Studios = show.GetTmdbCompanies()
            .Select(company => new Studio(company))
            .ToList();
        if (includeImages)
            Images = GetImages(show);
        if (alternateOrdering != null)
        {
            EpisodeCount = alternateOrdering.EpisodeCount;
            SeasonCount = alternateOrdering.SeasonCount;
        }
        else
        {
            EpisodeCount = show.EpisodeCount;
            SeasonCount = show.SeasonCount;
        }
        AlternateOrderingCount = show.AlternateOrderingCount;
        if (includeOrdering)
        {
            var ordering = new List<OrderingInformation>
            {
                new(show, alternateOrdering),
            };
            foreach (var altOrder in show.GetTmdbAlternateOrdering())
                ordering.Add(new(altOrder, alternateOrdering));
            Ordering = ordering
                .OrderByDescending(o => o.InUse)
                .ThenByDescending(o => string.IsNullOrEmpty(o.OrderingID))
                .ThenBy(o => o.OrderingName)
                .ToList();
        }
        FirstAiredAt = show.FirstAiredAt;
        LastAiredAt = show.LastAiredAt;
        CreatedAt = show.CreatedAt.ToUniversalTime();
        LastUpdatedAt = show.LastUpdatedAt.ToUniversalTime();

    }

    public class OrderingInformation
    {
        public string? OrderingID;

        public AlternateOrderingType? OrderingType;

        public string OrderingName;

        public int EpisodeCount;

        public int SeasonCount;

        public bool InUse;

        public OrderingInformation(TMDB_Show show, TMDB_AlternateOrdering? alternateOrderingInUse)
        {
            OrderingID = null;
            OrderingName = "Seasons";
            OrderingType = null;
            EpisodeCount = show.EpisodeCount;
            SeasonCount = show.SeasonCount;
            InUse = alternateOrderingInUse == null;
        }

        public OrderingInformation(TMDB_AlternateOrdering ordering, TMDB_AlternateOrdering? alternateOrderingInUse)
        {
            OrderingID = ordering.TmdbEpisodeGroupCollectionID;
            OrderingName = ordering.EnglishTitle;
            OrderingType = ordering.Type;
            EpisodeCount = ordering.EpisodeCount;
            SeasonCount = ordering.SeasonCount;
            InUse = alternateOrderingInUse != null &&
                string.Equals(ordering.TmdbEpisodeGroupCollectionID, alternateOrderingInUse.TmdbEpisodeGroupCollectionID);
        }
    }

    public static Images GetImages(TMDB_Show show)
    {
        var images = new Images();
        foreach (var image in show.GetImages())
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
