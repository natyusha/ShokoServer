using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Commands;
using Shoko.Server.Models;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using DataSource = Shoko.Server.API.v3.Models.Common.DataSource;
using TmdbEpisode = Shoko.Server.API.v3.Models.TMDB.Episode;
using TmdbMovie = Shoko.Server.API.v3.Models.TMDB.Movie;
using TmdbSeason = Shoko.Server.API.v3.Models.TMDB.Season;
using TmdbShow = Shoko.Server.API.v3.Models.TMDB.Show;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class TmdbController : BaseController
{
    private readonly ICommandRequestFactory CommandFactory;

    private readonly TMDBHelper TmdbHelper;

    public TmdbController(ICommandRequestFactory commandFactory, ISettingsProvider settingsProvider, TMDBHelper tmdbHelper) : base(settingsProvider)
    {
        CommandFactory = commandFactory;
        TmdbHelper = tmdbHelper;
    }

    #region Movies

    #region Constants

    internal const string MovieNotFound = "A TMDB.Movie by the given `movieID` was not found.";

    #endregion

    #region Basics

    /// <summary>
    /// List all locally available tmdb movies.
    /// </summary>
    /// <param name="search"></param>
    /// <param name="fuzzy"></param>
    /// <param name="includeTitles"></param>
    /// <param name="includeOverviews"></param>
    /// <param name="includeImages"></param>
    /// <param name="isRestricted"></param>
    /// <param name="isVideo"></param>
    /// <param name="pageSize"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    [HttpGet("Movie")]
    public ActionResult<ListResult<TmdbMovie>> GetTmdbMovies(
        [FromRoute] string? search = null,
        [FromQuery] bool fuzzy = true,
        [FromQuery] bool includeTitles = false,
        [FromQuery] bool includeOverviews = false,
        [FromQuery] bool includeImages = false,
        [FromQuery] bool? isRestricted = null,
        [FromQuery] bool? isVideo = null,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var hasSearch = string.IsNullOrWhiteSpace(search);
        var movies = RepoFactory.TMDB_Movie.GetAll()
            .AsParallel()
            .Where(movie =>
            {
                if (isRestricted.HasValue && isRestricted.Value != movie.IsRestricted)
                    return false;

                if (isVideo.HasValue && isVideo.Value != movie.IsVideo)
                    return false;

                return true;
            });
        if (hasSearch)
        {
            var languages = SettingsProvider.GetSettings()
                .LanguagePreference
                .Select(lang => lang.GetTitleLanguage())
                .Concat(new TitleLanguage[] { TitleLanguage.English })
                .ToHashSet();
            return movies
                .Search(
                    search,
                    movie => movie.GetAllTitles()
                        .Where(title => languages.Contains(title.Language))
                        .Select(title => title.Value)
                        .Distinct()
                        .ToList(),
                    fuzzy
                )
                .ToListResult(a => new TmdbMovie(a.Result, includeTitles, includeOverviews, includeImages), page, pageSize);
        }

        return movies
            .OrderBy(movie => movie.EnglishTitle)
            .ThenBy(movie => movie.TmdbMovieID)
            .ToListResult(m => new TmdbMovie(m, includeTitles, includeOverviews, includeImages), page, pageSize);
    }

    /// <summary>
    /// Get the local metadata for a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <param name="includeTitles"></param>
    /// <param name="includeOverviews"></param>
    /// <param name="includeImages"></param>
    /// <returns></returns>
    [HttpGet("Movie/{movieID}")]
    public ActionResult<TmdbMovie> GetTmdbMovieByMovieID(
        [FromRoute] int movieID,
        [FromQuery] bool includeTitles = true,
        [FromQuery] bool includeOverviews = true,
        [FromQuery] bool includeImages = true
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie == null)
            return NotFound(MovieNotFound);

        return new TmdbMovie(movie, includeTitles, includeOverviews, includeImages);
    }

    /// <summary>
    /// Remove the local copy of the metadata for a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("Movie/{movieID}")]
    public ActionResult RemoveTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie == null)
            return NotFound(MovieNotFound);

        CommandFactory.CreateAndSave<CommandRequest_TMDB_Movie_Purge>(c => c.TmdbMovieID = movieID);

        return NoContent();
    }

    #endregion

    #region Same-Source Linked Entries

    [HttpGet("Movie/{movieID}/Collection")]
    public ActionResult<object> GetTmdbMovieCollectionByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie == null)
            return NotFound(MovieNotFound);

        var movieCollection = movie.GetTmdbCollection();
        if (movieCollection == null)
            return NotFound(MovieCollectionByMovieIDNotFound);

        // TODO: convert this to the v3 model once finalised.
        return movieCollection;
    }

    #endregion

    #region Cross-Source Linked Entries

    /// <summary>
    /// Get all AniDB series linked to a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <returns></returns>
    [HttpGet("Movie/{movieID}/AniDB/Anime")]
    public ActionResult<List<Series.AniDB>> GetAniDBAnimeByTmdbMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie == null)
            return NotFound(MovieNotFound);

        return movie.GetCrossReferences()
            .Select(xref => xref.GetAnidbAnime())
            .OfType<SVR_AniDB_Anime>()
            .Select(anime => new Series.AniDB(anime))
            .ToList();
    }

    /// <summary>
    /// Get all AniDB episodes linked to a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <returns></returns>
    [HttpGet("Movie/{movieID}/AniDB/Episodes")]
    public ActionResult<List<Episode.AniDB>> GetAniDBEpisodesByTmdbMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie == null)
            return NotFound(MovieNotFound);

        return movie.GetCrossReferences()
            .Where(xref => xref.AnidbEpisodeID.HasValue)
            .Select(xref => xref.GetAnidbEpisode())
            .OfType<AniDB_Episode>()
            .Select(episode => new Episode.AniDB(episode))
            .ToList();
    }

    /// <summary>
    /// Get all Shoko series linked to a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <param name="randomImages">Randomise images shown for the <see cref="Series"/>.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("Movie/{movieID}/Shoko/Series")]
    public ActionResult<List<Series>> GetShokoSeriesByTmdbMovieID(
        [FromRoute] int movieID,
        [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie == null)
            return NotFound(MovieNotFound);

        return movie.GetCrossReferences()
            .Select(xref => xref.GetShokoSeries())
            .OfType<SVR_AnimeSeries>()
            .Select(series => new Series(HttpContext, series, randomImages, includeDataFrom))
            .ToList();
    }

    /// <summary>
    /// Get all Shoko episodes linked to a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("Movie/{movieID}/Shoko/Episodes")]
    public ActionResult<List<Episode>> GetShokoEpisodesByTmdbMovieID(
        [FromRoute] int movieID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie == null)
            return NotFound(MovieNotFound);

        return movie.GetCrossReferences()
            .Where(xref => xref.AnidbEpisodeID.HasValue)
            .Select(xref => xref.GetShokoEpisode())
            .OfType<SVR_AnimeEpisode>()
            .Select(episode => new Episode(HttpContext, episode, includeDataFrom))
            .ToList();
    }

    #endregion

    #region Actions

    /// <summary>
    /// Refresh or download  the metadata for a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <param name="force">Forcefully download an update even if we updated recently.</param>
    /// <param name="downloadImages">Also download images.</param>
    /// <param name="downloadCollections"></param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("Movie/{movieID}/Action/Refresh")]
    public ActionResult RefreshTmdbMovieByMovieID(
        [FromRoute] int movieID,
        [FromQuery] bool force = false,
        [FromQuery] bool downloadImages = true,
        [FromQuery] bool? downloadCollections = null
    )
    {
        CommandFactory.CreateAndSave<CommandRequest_TMDB_Movie_Update>(c =>
        {
            c.TmdbMovieID = movieID;
            c.ForceRefresh = force;
            c.DownloadImages = downloadImages;
            c.DownloadCollections = downloadCollections;
        });

        return Ok();
    }

    #endregion

    #region Search

    /// <summary>
    /// Search TMDB for movies using the offline or online search.
    /// </summary>
    /// <param name="query">Query to search for.</param>
    /// <param name="fuzzy">Indicates fuzzy-matching should be used for the search.</param>
    /// <param name="local">Only search for results in the local collection if it's true and only search for results not in the local collection if false. Omit to include both.</param>
    /// <param name="restricted">Only search for results which are or are not restriced if set, otherwise will include both restricted and not restriced movies.</param>
    /// <param name="includeTitles">Include titles in the results.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("Movie/Search")]
    public ListResult<object> SearchForTmdbMovies(
        [FromRoute] string query,
        [FromQuery] bool fuzzy = true,
        [FromQuery] bool? local = null,
        [FromQuery] bool? restricted = null,
        [FromQuery] bool includeTitles = true,
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        // TODO: Modify this once the tmdb movie search model is finalised. Also maybe switch to using online search, maybe utilising the offline search if we're offline.

        return TmdbHelper.OfflineSearch.SearchMovies(query, fuzzy)
            .Where(movie =>
            {
                return true;
            })
            .ToListResult(a => a as object, page, pageSize);
    }

    #endregion

    #endregion

    #region Movie Collection

    #region Constants

    internal const string MovieCollectionNotFound = "A TMDB.MovieCollection by the given `collectionID` was not found.";

    internal const string MovieCollectionByMovieIDNotFound = "A TMDB.MovieCollection by the given `movieID` was not found.";

    #endregion

    #region Basics

    [HttpGet("Movie/Collection")]
    public ActionResult<ListResult<object>> GetMovieCollections(
        [FromRoute] string query,
        [FromQuery] bool fuzzy = true,
        [FromQuery] bool includeTitles = true,
        [FromQuery] bool? isRestricted = null,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var enumable = RepoFactory.TMDB_Collection.GetAll()
            .Where(collection =>
            {
                // TODO: Implement filtering.
                return true;
            });

        if (!string.IsNullOrWhiteSpace(query))
            enumable = enumable
                .Search(query, t => t.GetAllTitles().Select(u => u.Value), fuzzy)
                .Select(e => e.Result);

        return enumable
            .ToListResult(a => a as object, page, pageSize);
    }

    [HttpGet("Movie/Collection/{collectionID}")]
    public ActionResult<object> GetMovieCollectionByCollectionID(
        [FromRoute] int collectionID
    )
    {
        var collection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(collectionID);
        if (collection == null)
            return NotFound(MovieCollectionNotFound);

        // TODO: convert this to the v3 model once finalised.
        return collection;
    }

    #endregion

    #region Same-Source Linked Entries

    [HttpGet("Movie/Collection/{collecitonID}/Movie")]
    public ActionResult<List<TmdbMovie>> GetMoviesForMovieCollectionByCollectionID(
        [FromRoute] int collectionID,
        [FromQuery] bool includeTitles = false,
        [FromQuery] bool includeOverviews = false,
        [FromQuery] bool includeImages = false
    )
    {
        var collection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(collectionID);
        if (collection == null)
            return NotFound(MovieCollectionNotFound);

        return collection.GetTmdbMovies()
            .Select(movie => new TmdbMovie(movie, includeTitles, includeOverviews, includeImages))
            .ToList();
    }

    #endregion

    #endregion

    #region Shows

    #region Constants

    internal const string AlternateOrderingIdRegex = @"^[a-f0-9]{24}$";

    internal const string ShowNotFound = "A TMDB.Show by the given `showID` was not found.";

    internal const string ShowNotFoundBySeasonID = "A TMDB.Show by the given `seasonID` was not found";

    internal const string ShowNotFoundByOrderingID = "A TMDB.Show by the given `orderingID` was not found";

    internal const string ShowNotFoundByEpisodeID = "A TMDB.Show by the given `seasonID` was not found";

    #endregion

    #region Basics

    /// <summary>
    /// List all locally available tmdb shows.
    /// </summary>
    /// <param name="search"></param>
    /// <param name="fuzzy"></param>
    /// <param name="includeTitles"></param>
    /// <param name="includeOverviews"></param>
    /// <param name="includeOrdering"></param>
    /// <param name="includeImages"></param>
    /// <param name="isRestricted"></param>
    /// <param name="pageSize"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    [HttpGet("Show")]
    public ActionResult<ListResult<TmdbShow>> GetTmdbShows(
        [FromRoute] string? search = null,
        [FromQuery] bool fuzzy = true,
        [FromQuery] bool includeTitles = false,
        [FromQuery] bool includeOverviews = false,
        [FromQuery] bool includeOrdering = false,
        [FromQuery] bool includeImages = false,
        [FromQuery] bool? isRestricted = null,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var hasSearch = string.IsNullOrWhiteSpace(search);
        var shows = RepoFactory.TMDB_Show.GetAll()
            .AsParallel()
            .Where(show =>
            {
                if (isRestricted.HasValue && isRestricted.Value != show.IsRestricted)
                    return false;

                return true;
            });
        if (hasSearch)
        {
            var languages = SettingsProvider.GetSettings()
                .LanguagePreference
                .Select(lang => lang.GetTitleLanguage())
                .Concat(new TitleLanguage[] { TitleLanguage.English })
                .ToHashSet();
            return shows
                .Search(
                    search,
                    show => show.GetAllTitles()
                        .Where(title => languages.Contains(title.Language))
                        .Select(title => title.Value)
                        .Distinct()
                        .ToList(),
                    fuzzy
                )
                .ToListResult(a => new TmdbShow(a.Result, includeTitles, includeOverviews, includeOrdering, includeImages), page, pageSize);
        }

        return shows
            .OrderBy(show => show.EnglishTitle)
            .ThenBy(show => show.TmdbShowID)
            .ToListResult(m => new TmdbShow(m, includeTitles, includeOverviews, includeOrdering, includeImages), page, pageSize);
    }

    /// <summary>
    /// Get the local metadata for a TMDB show.
    /// </summary>
    /// <returns></returns>
    [HttpGet("Show/{showID}")]
    public ActionResult<TmdbShow> GetTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery] bool includeTitles = true,
        [FromQuery] bool includeOverviews = true,
        [FromQuery] bool includeOrdering = false,
        [FromQuery] bool includeImages = false,
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show == null)
            return NotFound(ShowNotFound);

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
            if (alternateOrdering == null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

            return new TmdbShow(show, alternateOrdering, includeTitles, includeOverviews, includeOrdering, includeImages);
        }

        return new TmdbShow(show, includeTitles, includeOverviews, includeOrdering, includeImages);
    }

    /// <summary>
    /// Remove the local copy of the metadata for a TMDB show.
    /// </summary>
    /// <param name="showID">TMDB Movie ID.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("Show/{showID}")]
    public ActionResult RemoveTmdbShowByShowID(
        [FromRoute] int showID
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show == null)
            return NotFound(ShowNotFound);

        CommandFactory.CreateAndSave<CommandRequest_TMDB_Show_Purge>(c => c.TmdbShowID = showID);

        return NoContent();
    }

    #endregion

    #region Same-Source Linked Entries

    [HttpGet("Show/{showID}/Season")]
    public ActionResult<ListResult<TmdbSeason>> GetTmdbSeasonsByTmdbShowID(
        [FromRoute] int showID,
        [FromQuery] bool includeTitles = true,
        [FromQuery] bool includeOverviews = true,
        [FromQuery] bool includeImages = false,
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null,
        [FromQuery, Range(0, 100)] int pageSize = 25,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show == null)
            return NotFound(ShowNotFound);

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
            if (alternateOrdering == null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

            return alternateOrdering.GetTmdbAlternateOrderingSeasons()
                .ToListResult(season => new TmdbSeason(season, includeTitles, includeOverviews, includeImages), page, pageSize);
        }

        return show.GetTmdbSeasons()
            .ToListResult(season => new TmdbSeason(season, includeTitles, includeOverviews, includeImages), page, pageSize);
    }

    [HttpGet("Show/{showID}/Episodes")]
    public ActionResult<ListResult<TmdbEpisode>> GetTmdbEpisodesByTmdbShowID(
        [FromRoute] int showID,
        [FromQuery] bool includeTitles = true,
        [FromQuery] bool includeOverviews = true,
        [FromQuery] bool includeOrdering = false,
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show == null)
            return NotFound(ShowNotFound);

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
            if (alternateOrdering == null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

            return alternateOrdering.GetTmdbAlternateOrderingEpisodes()
                .ToListResult(e => new TmdbEpisode(e.GetTmdbEpisode()!, e, includeTitles, includeOverviews, includeOrdering), page, pageSize);
        }

        return show.GetTmdbEpisodes()
            .ToListResult(e => new TmdbEpisode(e, includeTitles, includeOverviews, includeOrdering), page, pageSize);
    }

    #endregion

    #region Cross-Source Linked Entries

    /// <summary>
    /// Get all AniDB series linked to a TMDB show.
    /// </summary>
    /// <param name="showID">TMDB Show ID.</param>
    /// <returns></returns>
    [HttpGet("Show/{showID}/AniDB/Anime")]
    public ActionResult<List<Series.AniDB>> GetAnidbAnimeByTmdbShowID(
        [FromRoute] int showID
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show == null)
            return NotFound(ShowNotFound);

        return show.GetCrossReferences()
            .Select(xref => xref.GetAnidbAnime())
            .OfType<SVR_AniDB_Anime>()
            .Select(anime => new Series.AniDB(anime))
            .ToList();
    }

    /// <summary>
    /// Get all Shoko series linked to a TMDB show.
    /// </summary>
    /// <param name="showID">TMDB Show ID.</param>
    /// <param name="randomImages">Randomise images shown for the <see cref="Series"/>.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("Show/{showID}/Shoko/Series")]
    public ActionResult<List<Series>> GetShokoSeriesByTmdbShowID(
        [FromRoute] int showID,
        [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show == null)
            return NotFound(ShowNotFound);

        return show.GetCrossReferences()
            .Select(xref => xref.GetShokoSeries())
            .OfType<SVR_AnimeSeries>()
            .Select(series => new Series(HttpContext, series, randomImages, includeDataFrom))
            .ToList();
    }

    #endregion

    #region Actions

    /// <summary>
    /// Refresh or download the metadata for a TMDB show.
    /// </summary>
    /// <param name="showID">TMDB Show ID.</param>
    /// <param name="force">Forcefully download an update even if we updated recently.</param>
    /// <param name="downloadImages">Also download images.</param>
    /// <param name="downloadAlternateOrdering"></param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("Show/{showID}/Action/Refresh")]
    public ActionResult RefreshTmdbShowByShowID(
        [FromRoute] int showID,
        [FromQuery] bool force = false,
        [FromQuery] bool downloadImages = true,
        [FromQuery] bool? downloadAlternateOrdering = null
    )
    {
        CommandFactory.CreateAndSave<CommandRequest_TMDB_Show_Update>(c =>
        {
            c.TmdbShowID = showID;
            c.ForceRefresh = force;
            c.DownloadImages = downloadImages;
            c.DownloadAlternateOrdering = downloadAlternateOrdering;
        });

        return Ok();
    }

    #endregion

    #region Search

    /// <summary>
    /// Search TMDB for shows using the offline or online search.
    /// </summary>
    /// <param name="query">Query to search for.</param>
    /// <param name="fuzzy">Indicates fuzzy-matching should be used for the search.</param>
    /// <param name="local">Only search for results in the local collection if it's true and only search for results not in the local collection if false. Omit to include both.</param>
    /// <param name="restricted">Only search for results which are or are not restriced if set, otherwise will include both restricted and not restriced shows.</param>
    /// <param name="includeTitles">Include titles in the results.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("Show/Search")]
    public ListResult<object> SearchForTmdbShows(
        [FromRoute] string query,
        [FromQuery] bool fuzzy = true,
        [FromQuery] bool? local = null,
        [FromQuery] bool? restricted = null,
        [FromQuery] bool includeTitles = true,
        [FromQuery, Range(0, 100)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        // TODO: Modify this once the tmdb show search model is finalised. Also maybe switch to using online search, maybe utilising the offline search if we're offline.

        return TmdbHelper.OfflineSearch.SearchShows(query, fuzzy)
            .Where(show =>
            {
                return true;
            })
            .ToListResult(a => a as object, page, pageSize);
    }

    #endregion

    #endregion

    #region Seasons

    #region Constants

    internal const int SeasonIdHexLength = 24;

    internal const string SeasonIdRegex = @"^(?:[0-9]{1,23}|[a-f0-9]{24})$";

    internal const string SeasonNotFound = "A TMDB.Season by the given `seasonID` was not found.";

    internal const string SeasonNotFoundByEpisodeID = "A TMDB.Season by the given `episodeID` was not found.";

    #endregion

    #region Basics

    [HttpGet("Season/{seasonID}")]
    public ActionResult<TmdbSeason> GetTmdbSeasonBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID,
        [FromQuery] bool includeTitles = true,
        [FromQuery] bool includeOverviews = true,
        [FromQuery] bool includeImages = true
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason == null)
                return NotFound(SeasonNotFound);

            return new TmdbSeason(altOrderSeason, includeTitles, includeOverviews, includeImages);
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season == null)
            return NotFound(SeasonNotFound);

        return new TmdbSeason(season, includeTitles, includeOverviews, includeImages);
    }

    #endregion

    #region Same-Source Linked Entries

    [HttpGet("Season/{seasonID}/Show")]
    public ActionResult<TmdbShow> GetTmdbShowBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID,
        [FromQuery] bool includeTitles = true,
        [FromQuery] bool includeOverviews = true,
        [FromQuery] bool includeOrdering = false,
        [FromQuery] bool includeImages = false
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason == null)
                return NotFound(SeasonNotFound);
            var altOrder = altOrderSeason.GetTmdbAlternateOrdering();
            var altShow = altOrder?.GetTmdbShow();
            if (altShow == null)
                return NotFound(ShowNotFoundBySeasonID);

            return new TmdbShow(altShow, altOrder, includeTitles, includeOverviews, includeOrdering, includeImages);
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season == null)
            return NotFound(SeasonNotFound);

        var show = season.GetTmdbShow();
        if (show == null)
            return NotFound(ShowNotFoundBySeasonID);

        return new TmdbShow(show, includeTitles, includeOverviews, includeOrdering, includeImages);
    }

    [HttpGet("Season/{seasonID}/Episode")]
    public ActionResult<ListResult<TmdbEpisode>> GetTmdbEpisodesBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID,
        [FromQuery] bool includeTitles = true,
        [FromQuery] bool includeOverviews = true,
        [FromQuery] bool includeOrdering = false,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason == null)
                return NotFound(SeasonNotFound);

            return altOrderSeason.GetTmdbAlternateOrderingEpisodes()
                .ToListResult(e => new TmdbEpisode(e.GetTmdbEpisode()!, e, includeTitles, includeOverviews, includeOrdering), page, pageSize);
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season == null)
            return NotFound(SeasonNotFound);

        // TODO: convert this to the v3 model once finalised.
        return season.GetTmdbEpisodes()
            .ToListResult(e => new TmdbEpisode(e, includeTitles, includeOverviews, includeOrdering), page, pageSize);
    }

    #endregion

    #region Cross-Source Linked Entries

    [HttpGet("Season/{seasonID}/AniDB/Anime")]
    public ActionResult<List<Series.AniDB>> GetAniDBAnimeBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason == null)
                return NotFound(SeasonNotFound);

            return new List<Series.AniDB>();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season == null)
            return NotFound(SeasonNotFound);

        return season.GetCrossReferences()
            .Select(xref => xref.GetAnidbAnime())
            .OfType<SVR_AniDB_Anime>()
            .Select(anime => new Series.AniDB(anime))
            .ToList();
    }

    [HttpGet("Season/{seasonID}/Shoko/Series")]
    public ActionResult<List<Series>> GetShokoSeriesBySeasonID(
        [FromRoute, RegularExpression(SeasonIdRegex)] string seasonID,
        [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null
    )
    {
        if (seasonID.Length == SeasonIdHexLength)
        {
            var altOrderSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
            if (altOrderSeason == null)
                return NotFound(SeasonNotFound);

            return new List<Series>();
        }

        var seasonId = int.Parse(seasonID);
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonId);
        if (season == null)
            return NotFound(SeasonNotFound);

        return season.GetCrossReferences()
            .Select(xref => xref.GetShokoSeries())
            .OfType<SVR_AnimeSeries>()
            .Select(series => new Series(HttpContext, series, randomImages, includeDataFrom))
            .ToList();
    }

    #endregion

    #endregion

    #region Episodes

    #region Constants

    internal const string EpisodeNotFound = "A TMDB.Episode by the given `episodeID` was not found.";

    #endregion

    #region Basics

    [HttpGet("Episode/{episodeID}")]
    public ActionResult<TmdbEpisode> GetTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery] bool includeTitles = true,
        [FromQuery] bool includeOverviews = true,
        [FromQuery] bool includeOrdering = false,
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFound);

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrderingEpisode = RepoFactory.TMDB_AlternateOrdering_Episode.GetByEpisodeGroupCollectionAndEpisodeIDs(alternateOrderingID, episodeID);
            if (alternateOrderingEpisode == null)
                return ValidationProblem("Invalid alternateOrderingID for episode.", "alternateOrderingID");

            return new TmdbEpisode(episode, alternateOrderingEpisode, includeTitles, includeOverviews, includeOrdering);
        }

        return new TmdbEpisode(episode, includeTitles, includeOverviews, includeOrdering);
    }

    #endregion

    #region Same-Source Linked Entries

    [HttpGet("Episode/{episodeID}/Show")]
    public ActionResult<TmdbShow> GetTmdbShowByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery] bool includeTitles = true,
        [FromQuery] bool includeOverviews = true,
        [FromQuery] bool includeOrdering = false,
        [FromQuery] bool includeImages = false,
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFound);

        var show = episode.GetTmdbShow();
        if (show == null)
            return NotFound(ShowNotFoundByEpisodeID);

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrdering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(alternateOrderingID);
            if (alternateOrdering == null || alternateOrdering.TmdbShowID != show.TmdbShowID)
                return ValidationProblem("Invalid alternateOrderingID for show.", "alternateOrderingID");

            return new TmdbShow(show, alternateOrdering, includeTitles, includeOverviews, includeOrdering, includeImages);
        }

        return new TmdbShow(show, includeTitles, includeOverviews, includeOrdering, includeImages);
    }

    [HttpGet("Episode/{episodeID}/Season")]
    public ActionResult<TmdbSeason> GetTmdbSeasonByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery] bool includeTitles = true,
        [FromQuery] bool includeOverviews = true,
        [FromQuery] bool includeImages = false,
        [FromQuery, RegularExpression(AlternateOrderingIdRegex)] string? alternateOrderingID = null
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFound);

        if (!string.IsNullOrWhiteSpace(alternateOrderingID))
        {
            var alternateOrderingEpisode = RepoFactory.TMDB_AlternateOrdering_Episode.GetByEpisodeGroupCollectionAndEpisodeIDs(alternateOrderingID, episodeID);
            var altOrderSeason = alternateOrderingEpisode?.GetTmdbAlternateOrderingSeason();
            if (altOrderSeason == null)
                return NotFound(SeasonNotFoundByEpisodeID);

            return new TmdbSeason(altOrderSeason, includeTitles, includeOverviews, includeImages);
        }

        var season = episode.GetTmdbSeason();
        if (season == null)
            return NotFound(SeasonNotFoundByEpisodeID);

        return new TmdbSeason(season, includeTitles, includeOverviews, includeImages);
    }

    #endregion

    #region Cross-Source Linked Entries

    [HttpGet("Episode/{episodeID}/AniDB/Anime")]
    public ActionResult<List<Series.AniDB>> GetAniDBAnimeByEpisodeID(
        [FromRoute] int episodeID
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFound);

        return episode.GetCrossReferences()
            .DistinctBy(xref => xref.AnidbAnimeID)
            .Select(xref => xref.GetAnidbAnime())
            .OfType<SVR_AniDB_Anime>()
            .Select(anime => new Series.AniDB(anime))
            .ToList();
    }

    [HttpGet("Episode/{episodeID}/Anidb/Episode")]
    public ActionResult<List<Episode.AniDB>> GetAniDBEpisodeByEpisodeID(
        [FromRoute] int episodeID
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFound);

        return episode.GetCrossReferences()
            .DistinctBy(xref => xref.AnidbAnimeID)
            .Select(xref => xref.GetAnidbEpisode())
            .OfType<AniDB_Episode>()
            .Select(anidbEpisode => new Episode.AniDB(anidbEpisode))
            .ToList();
    }

    [HttpGet("Episode/{episodeID}/Shoko/Series")]
    public ActionResult<List<Series>> GetShokoSeriesByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFound);

        return episode.GetCrossReferences()
            .DistinctBy(xref => xref.AnidbAnimeID)
            .Select(xref => xref.GetShokoSeries())
            .OfType<SVR_AnimeSeries>()
            .Select(shokoSeries => new Series(HttpContext, shokoSeries, randomImages, includeDataFrom))
            .ToList();
    }

    [HttpGet("Episode/{episodeID}/Shoko/Episode")]
    public ActionResult<List<Episode>> GetShokoEpisodesByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFound);

        return episode.GetCrossReferences()
            .DistinctBy(xref => xref.AnidbEpisodeID)
            .Select(xref => xref.GetShokoEpisode())
            .OfType<SVR_AnimeEpisode>()
            .Select(shokoEpisode => new Episode(HttpContext, shokoEpisode, includeDataFrom))
            .ToList();
    }

    #endregion

    #endregion
}
