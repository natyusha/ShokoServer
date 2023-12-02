using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
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
    /// Preferred title based upon episode title preference.
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
    /// All available overviews for the episode, if they should be included.
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
    /// Genres.
    /// </summary>
    public IReadOnlyList<string> Genres;

    /// <summary>
    /// Content ratings for different countries for this show.
    /// </summary>
    public IReadOnlyList<ContentRating> ContentRatings;

    /// <summary>
    /// User rating of the episode from TMDB users.
    /// </summary>
    public Rating UserRating;

    /// <summary>
    /// The episode run-time, if it is known.
    /// </summary>
    public TimeSpan? Runtime;

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

    public Movie(TMDB_Movie movie, bool includeTitles = true, bool includeOverviews = true)
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

        Genres = movie.Genres;

        ContentRatings = movie.ContentRatings
            .Select(contentRating => new ContentRating(contentRating))
            .ToList();

        UserRating = new()
        {
            Value = (decimal)movie.UserRating,
            MaxValue = 10,
            Votes = movie.UserVotes,
            Source = "TMDB",
        };

        Runtime = movie.Runtime;
        ReleasedAt = movie.ReleasedAt;
        CreatedAt = movie.CreatedAt.ToUniversalTime();
        LastUpdatedAt = movie.LastUpdatedAt.ToUniversalTime();
    }
}
