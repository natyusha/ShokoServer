using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Server;
using TMDbLib.Objects.Movies;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_Movie
{
    #region Properties

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_MovieID { get; set; }

    /// <summary>
    /// TMDB Movie ID.
    /// </summary>
    public int TmdbMovieID { get; set; }

    /// <summary>
    /// TMDB Collection ID, if the movie is part of a collection.
    /// </summary>
    public int? TmdbCollectionID { get; set; }

    /// <summary>
    /// The english title of the movie, used as a fallback for when no title
    /// is available in the preferred language.
    /// </summary>
    public string EnglishTitle { get; set; } = string.Empty;

    /// <summary>
    /// The english overview, used as a fallback for when no overview is
    /// available in the preferred language.
    /// </summary>
    public string EnglishOverview { get; set; } = string.Empty;

    /// <summary>
    /// Original title in the original language.
    /// </summary>
    public string OriginalTitle { get; set; } = string.Empty;

    /// <summary>
    /// The original language this show was shot in.
    /// </summary>
    public TitleLanguage OriginalLanguage { get; set; }

    /// <summary>
    /// Same as <seealso cref="OriginalLanguage"/>, just in text form.
    /// </summary>
    public string OriginalLanguageCode
    {
        get => OriginalLanguage.GetString();
        private set => OriginalLanguage = value.GetTitleLanguage();
    }

    /// <summary>
    /// Indicates the movie is restricted to an age group above the legal age,
    /// because it's a pornography.
    /// </summary>
    public bool IsRestricted { get; set; }

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
    public bool IsVideo { get; set; }

    /// <summary>
    /// Genres.
    /// </summary>
    public List<string> Genres { get; set; } = new();

    /// <summary>
    /// Content ratings for different countries for this show.
    /// </summary>
    public List<TMDB_ContentRating> ContentRatings { get; set; } = new();

    /// <summary>
    /// Movie run-time in minutes.
    /// </summary>
    public int? RuntimeMintues
    {
        get => Runtime.HasValue ? (int)Math.Floor(Runtime.Value.TotalMinutes) : null;
        set => Runtime = value.HasValue ? TimeSpan.FromMinutes(value.Value) : null;
    }
    /// <summary>
    /// Movie run-time.
    /// </summary>
    public TimeSpan? Runtime { get; set; }

    /// <summary>
    /// Average user rating across all <see cref="UserVotes"/>.
    /// </summary>
    public double UserRating { get; set; }

    /// <summary>
    /// Number of users that cast a vote for a rating of this show.
    /// </summary>
    /// <value></value>
    public int UserVotes { get; set; }

    /// <summary>
    /// When the movie aired, or when it will air in the future if it's known.
    /// </summary>
    public DateOnly? ReleasedAt { get; set; }

    /// <summary>
    /// When the metadata was first downloaded.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the metadata was last syncronized with the remote.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region Constructors

    public TMDB_Movie() { }

    public TMDB_Movie(int movieId)
    {
        TmdbMovieID = movieId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion

    #region Methods

    public void Populate(Movie movie)
    {
        var translation = movie.Translations.Translations.FirstOrDefault(translation => translation.Iso_639_1 == "en");
        EnglishTitle = translation?.Data.Name ?? movie.Title;
        EnglishOverview = translation?.Data.Overview ?? movie.Overview;
        OriginalTitle = movie.OriginalTitle;
        OriginalLanguageCode = movie.OriginalLanguage;
        IsRestricted = movie.Adult;
        IsVideo = movie.Video;
        Genres = movie.Genres.SelectMany(genre => genre.Name.Split('&', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)).ToList();
        ContentRatings = movie.ReleaseDates.Results.Select(releaseDate => new TMDB_ContentRating(releaseDate.Iso_3166_1.FromIso3166ToIso639().GetTitleLanguage(), releaseDate.ReleaseDates.First().Certification)).ToList();
        Runtime = movie.Runtime.HasValue ? TimeSpan.FromMinutes(movie.Runtime.Value) : null;
        UserRating = movie.VoteAverage;
        UserVotes = movie.VoteCount;
        ReleasedAt = movie.ReleaseDate.HasValue ? DateOnly.FromDateTime(movie.ReleaseDate.Value) : null;
        LastUpdatedAt = DateTime.Now;
    }

    public TMDB_Title? GetPreferredTitle(bool useFallback = false)
    {
        // TODO: Implement this logic once the repositories are added.

        // Fallback.
        return useFallback ? new(ForeignEntityType.Movie, TmdbMovieID, EnglishTitle, TitleLanguage.English) : null;
    }

    public IReadOnlyList<TMDB_Title> GetAllTitles()
    {
        // TODO: Implement this logic once the repositories are added.

        return new List<TMDB_Title>();
    }

    public TMDB_Overview? GetPreferredOverview(bool useFallback = false)
    {
        // TODO: Implement this logic once the repositories are added.

        return useFallback ? new(ForeignEntityType.Movie, TmdbMovieID, EnglishOverview, TitleLanguage.English) : null;
    }

    public IReadOnlyList<TMDB_Overview> GetAllOverviews()
    {
        // TODO: Implement this logic once the repositories are added.

        return new List<TMDB_Overview>();
    }

    #endregion
}
