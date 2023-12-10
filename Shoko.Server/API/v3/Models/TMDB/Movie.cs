using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.API.v3.Models.TMDB;

/// <summary>
/// APIv3 The Movie DataBase (TMDB) Movie Data Transfer Object (DTO).
/// </summary>
public class Movie
{
    /// <summary>
    /// TMDB Movie ID.
    /// </summary>
    public int ID;

    /// <summary>
    /// TMDB Movie Collection ID, if the movie is in a movie collection on TMDB.
    /// </summary>
    public int? CollectionID;

    /// <summary>
    /// Preferred title based upon series title preference.
    /// </summary>
    public string Title;

    /// <summary>
    /// All available titles for the movie, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Title>? Titles;

    /// <summary>
    /// Preferred overview based upon episode title preference.
    /// </summary>
    public string Overview;

    /// <summary>
    /// All available overviews for the movie, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Overview>? Overviews;

    [JsonConverter(typeof(StringEnumConverter))]
    public TitleLanguage OriginalLanguage;

    /// <summary>
    /// Indicates the movie is restricted to an age group above the legal age,
    /// because it's a pornography.
    /// </summary>
    public bool IsRestricted;

    /// <summary>
    /// Indicates the entry is not truly a movie, including but not limited to
    /// the types:
    ///
    /// - official compilations,
    /// - best of,
    /// - filmed sport events,
    /// - music concerts,
    /// - plays or stand-up show,
    /// - fitness video,
    /// - health video,
    /// - live movie theater events (art, music),
    /// - and how-to DVDs,
    ///
    /// among others.
    /// </summary>
    public bool IsVideo;

    /// <summary>
    /// User rating of the episode from TMDB users.
    /// </summary>
    public Rating UserRating;

    /// <summary>
    /// The episode run-time, if it is known.
    /// </summary>
    public TimeSpan? Runtime;

    /// <summary>
    /// Genres.
    /// </summary>
    public IReadOnlyList<string> Genres;

    /// <summary>
    /// Content ratings for different countries for this show.
    /// </summary>
    public IReadOnlyList<ContentRating> ContentRatings;

    /// <summary>
    /// The production companies (studios) that produced the movie.
    /// </summary>
    public IReadOnlyList<Studio> Studios;

    /// <summary>
    /// Images assosiated with the movie, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Images? Images;

    /// <summary>
    /// The date the episode first released, if it is known.
    /// </summary>
    public DateOnly? ReleasedAt;

    /// <summary>
    /// When the local metadata was first created.
    /// </summary>
    public DateTime CreatedAt;

    /// <summary>
    /// When the local metadata was last updated with new changes from the
    /// remote.
    /// </summary>
    public DateTime LastUpdatedAt;

    public Movie(TMDB_Movie movie, bool includeTitles = true, bool includeOverviews = true, bool includeImages = false)
    {
        var preferredTitle = movie.GetPreferredTitle(true);
        var preferredOverview = movie.GetPreferredOverview(true);

        ID = movie.TmdbMovieID;
        CollectionID = movie.TmdbCollectionID;
        Title = preferredTitle!.Value;
        if (includeTitles)
            Titles = movie.GetAllTitles()
                .Select(title => new Title(title, movie.EnglishTitle, preferredTitle))
                .OrderByDescending(title => title.Preferred)
                .ThenBy(title => title.Default)
                .ThenBy(title => title.Language)
                .ToList();
        Overview = preferredOverview!.Value;
        if (includeOverviews)
            Overviews = movie.GetAllOverviews()
                .Select(title => new Overview(title, movie.EnglishOverview, preferredOverview))
                .OrderByDescending(title => title.Preferred)
                .ThenBy(title => title.Default)
                .ThenBy(title => title.Language)
                .ToList();
        OriginalLanguage = movie.OriginalLanguage;
        IsRestricted = movie.IsRestricted;
        IsVideo = movie.IsVideo;
        UserRating = new()
        {
            Value = (decimal)movie.UserRating,
            MaxValue = 10,
            Votes = movie.UserVotes,
            Source = "TMDB",
        };
        Runtime = movie.Runtime;
        Genres = movie.Genres;
        ContentRatings = movie.ContentRatings
            .Select(contentRating => new ContentRating(contentRating))
            .ToList();
        Studios = movie.GetTmdbCompanies()
            .Select(company => new Studio(company))
            .ToList();
        if (includeImages)
            Images = GetImages(movie);
        ReleasedAt = movie.ReleasedAt;
        CreatedAt = movie.CreatedAt.ToUniversalTime();
        LastUpdatedAt = movie.LastUpdatedAt.ToUniversalTime();
    }

    /// <summary>
    /// APIv3 The Movie DataBase (TMDB) Movie Collection Data Transfer Object (DTO).
    /// </summary>
    public class Collection
    {
        /// <summary>
        /// TMDB Movie Collection ID.
        /// </summary>
        public int ID;

        /// <summary>
        /// Preferred title based upon series title preference.
        /// </summary>
        public string Title;

        /// <summary>
        /// All available titles for the movie collection, if they should be included.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyList<Title>? Titles;

        /// <summary>
        /// Preferred overview based upon episode title preference.
        /// </summary>
        public string Overview;

        /// <summary>
        /// All available overviews for the movie collection, if they should be included.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyList<Overview>? Overviews;

        public int MovieCount;

        /// <summary>
        /// Images assosiated with the movie collection, if they should be included.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Images? Images;

        /// <summary>
        /// When the local metadata was first created.
        /// </summary>
        public DateTime CreatedAt;

        /// <summary>
        /// When the local metadata was last updated with new changes from the
        /// remote.
        /// </summary>
        public DateTime LastUpdatedAt;

        public Collection(TMDB_Collection collection, bool includeTitles = false, bool includeOverviews = false, bool includeImages = false)
        {
            var preferredTitle = collection.GetPreferredTitle(true);
            var preferredOverview = collection.GetPreferredOverview(true);

            ID = collection.TmdbCollectionID;
            Title = preferredTitle!.Value;
            if (includeTitles)
                Titles = collection.GetAllTitles()
                    .Select(title => new Title(title, collection.EnglishTitle, preferredTitle))
                    .OrderByDescending(title => title.Preferred)
                    .ThenBy(title => title.Default)
                    .ThenBy(title => title.Language)
                    .ToList();
            Overview = preferredOverview!.Value;
            if (includeOverviews)
                Overviews = collection.GetAllOverviews()
                    .Select(title => new Overview(title, collection.EnglishOverview, preferredOverview))
                    .OrderByDescending(title => title.Preferred)
                    .ThenBy(title => title.Default)
                    .ThenBy(title => title.Language)
                    .ToList();
            MovieCount = collection.MovieCount;
            if (includeImages)
                Images = GetImages(collection);
            CreatedAt = collection.CreatedAt.ToUniversalTime();
            LastUpdatedAt = collection.LastUpdatedAt.ToUniversalTime();
        }
    }

    private static Images GetImages(TMDB_Movie movie)
    {
        var images = new Images();
        foreach (var image in movie.GetImages())
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

    private static Images GetImages(TMDB_Collection collection)
    {
        var images = new Images();
        foreach (var image in collection.GetImages())
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
