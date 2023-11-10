using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Models.Server;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Commands;
using Shoko.Server.Models;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using DataSource = Shoko.Server.API.v3.Models.Common.DataSource;

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
    /// <param name="query"></param>
    /// <param name="fuzzy"></param>
    /// <param name="includeTitles"></param>
    /// <param name="isRestricted"></param>
    /// <param name="pageSize"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    [HttpGet("Movie")]
    public ActionResult<ListResult<object>> GetTmdbMovies(
        [FromRoute] string? query = null,
        [FromQuery] bool fuzzy = true,
        [FromQuery] bool includeTitles = true,
        [FromQuery] bool? isRestricted = null,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        // TODO: Implement this once the v3 model is finalised.

        return new ListResult<object>();
    }

    /// <summary>
    /// Get the local metadata for a TMDB movie.
    /// </summary>
    /// <param name="movieID">TMDB Movie ID.</param>
    /// <returns></returns>
    [HttpGet("Movie/{movieID}")]
    public ActionResult<object> GetTmdbMovieByMovieID(
        [FromRoute] int movieID
    )
    {
        var movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movieID);
        if (movie == null)
            return NotFound(MovieNotFound);

        // TODO: convert this to the v3 model once finalised.
        return movie;
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
    public ActionResult<List<object>> GetMoviesForMovieCollectionByCollectionID(
        [FromRoute] int collectionID
    )
    {
        var collection = RepoFactory.TMDB_Collection.GetByTmdbCollectionID(collectionID);
        if (collection == null)
            return NotFound(MovieCollectionNotFound);

        var movies = collection.GetTmdbMovies();

        // TODO: convert this to the v3 model once finalised.
        return (List<object>)movies;
    }

    #endregion

    #endregion

    #region Shows

    #region Constants

    internal const string ShowNotFound = "A TMDB.Show by the given `showID` was not found.";

    internal const string ShowNotFoundBySeasonID = "A TMDB.Show by the given `seasonID` was not found";

    internal const string ShowNotFoundByOrderingID = "A TMDB.Show by the given `orderingID` was not found";

    internal const string ShowNotFoundByEpisodeID = "A TMDB.Show by the given `seasonID` was not found";

    #endregion

    #region Basics

    [HttpGet("Show")]
    public ActionResult<ListResult<object>> GetTmdbShows(
        [FromRoute] string? query = null,
        [FromQuery] bool fuzzy = true,
        [FromQuery] bool includeTitles = true,
        [FromQuery] bool? isRestricted = null,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        // TODO: Implement this once the v3 model is finalised.

        return new ListResult<object>();
    }

    /// <summary>
    /// Get the local metadata for a TMDB show.
    /// </summary>
    /// <param name="showID">TMDB Show ID</param>
    /// <returns></returns>
    [HttpGet("Show/{showID}")]
    public ActionResult<object> GetTmdbShowByShowID(
        [FromRoute] int showID
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show == null)
            return NotFound(ShowNotFound);

        // TODO: convert this to the v3 model once finalised.
        return show;
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

    [HttpGet("Show/{showID}/AlternateOrdering")]
    public ActionResult<List<object>> GetTmdbAlternateOrderingByTmdbShowID(
        [FromRoute] int showID
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show == null)
            return NotFound(ShowNotFound);

        var alternateOrderings = show.GetTmdbAlternateOrdering();

        // TODO: convert this to the v3 model once finalised.
        return (List<object>)alternateOrderings;
    }

    [HttpGet("Show/{showID}/Season")]
    public ActionResult<List<object>> GetTmdbSeasonsByTmdbShowID(
        [FromRoute] int showID
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show == null)
            return NotFound(ShowNotFound);

        var seasons = show.GetTmdbSeasons();

        // TODO: convert this to the v3 model once finalised.
        return (List<object>)seasons;
    }

    [HttpGet("Show/{showID}/Episodes")]
    public ActionResult<ListResult<object>> GetTmdbEpisodesByTmdbShowID(
        [FromRoute] int showID,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var show = RepoFactory.TMDB_Show.GetByTmdbShowID(showID);
        if (show == null)
            return NotFound(ShowNotFound);

        // TODO: convert this to the v3 model once finalised.
        return show.GetTmdbEpisodes()
            .ToListResult(e => e as object, page, pageSize);
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

    #region Alternate Ordering

    #region Constants

    internal const string OrderingNotFound = "A TMDB.AlternateOrdering by the given `orderingID` was not found.";

    internal const string OrderingNotFoundBySeasonID = "A TMDB.AlternateOrdering by the given `seasonID` was not found.";

    #endregion

    #region Basics

    [HttpGet("AlternateOrdering/{orderingID}")]
    public ActionResult<object> GetTmdbAlternateOrderingByOrderingID(
        [FromRoute, RegularExpression(@"^[0-9a-fA-F]{32}$")] string orderingID
    )
    {
        var ordering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(orderingID);
        if (ordering == null)
            return NotFound(OrderingNotFound);

        // TODO: convert this to the v3 model once finalised.
        return ordering;
    }

    #endregion

    #region Same-Source Linked Entries

    [HttpGet("AlternateOrdering/{orderingID}/Show")]
    public ActionResult<object> GetTmdbShowByOrderingID(
        [FromRoute, RegularExpression(@"^[0-9a-fA-F]{32}$")] string orderingID
    )
    {
        var ordering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(orderingID);
        if (ordering == null)
            return NotFound(OrderingNotFound);

        var show = ordering.GetTmdbShow();
        if (show == null)
            return NotFound(ShowNotFoundByOrderingID);

        // TODO: convert this to the v3 model once finalised.
        return show;
    }

    [HttpGet("AlternateOrdering/{orderingID}/Season")]
    public ActionResult<List<object>> GetTmdbSeasonsByOrderingID(
        [FromRoute, RegularExpression(@"^[0-9a-fA-F]{32}$")] string orderingID
    )
    {
        var ordering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(orderingID);
        if (ordering == null)
            return NotFound(OrderingNotFound);

        // TODO: convert this to the v3 model once finalised.
        return ordering.GetTmdbAlternateOrderingSeasons()
            .Select(s => s as object)
            .ToList();
    }

    [HttpGet("AlternateOrdering/{orderingID}/Episode")]
    public ActionResult<ListResult<object>> GetTmdbEpisodesByOrderingID(
        [FromRoute, RegularExpression(@"^[0-9a-fA-F]{32}$")] string orderingID,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var ordering = RepoFactory.TMDB_AlternateOrdering.GetByTmdbEpisodeGroupCollectionID(orderingID);
        if (ordering == null)
            return NotFound(OrderingNotFound);

        // TODO: convert this to the v3 model once finalised.
        return ordering.GetTmdbAlternateOrderingEpisodes()
            .Select(e => (CrossRef: e, Episode: e.GetTmdbEpisode()))
            .Where(e => e.Episode != null)
            .ToListResult(e => e.Episode! as object, page, pageSize);
    }

    #endregion

    #endregion Alternate Ordering

    #region Alternate Ordering Season

    #region Constants

    internal const string OrderingSeasonNotFound = "A TMDB.AlternateOrderingSeason by the given `seasonID` was not found.";

    #endregion

    #region Basics

    [HttpGet("AlternateOrdering/Season/{seasonID}")]
    public ActionResult<object> GetAlternateOrderingSeasonBySeasonID(
        [FromRoute, RegularExpression(@"^[0-9a-fA-F]{32}$")] string seasonID
    )
    {
        var orderingSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
        if (orderingSeason == null)
            return NotFound(OrderingNotFound);

        // TODO: convert this to the v3 model once finalised.
        return orderingSeason;
    }

    #endregion

    #region Same-Source Linked Entries

    [HttpGet("AlternateOrdering/Season/{seasonID}/Show")]
    public ActionResult<object> GetTmdbShowByAlternateOrderingSeasonID(
        [FromRoute, RegularExpression(@"^[0-9a-fA-F]{32}$")] string seasonID
    )
    {
        var orderingSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
        if (orderingSeason == null)
            return NotFound(OrderingNotFound);

        var show = orderingSeason.GetTmdbShow();
        if (show == null)
            return NotFound(ShowNotFoundBySeasonID);

        // TODO: convert this to the v3 model once finalised.
        return show;
    }

    [HttpGet("AlternateOrdering/Season/{seasonID}/AlternateOrdering")]
    public ActionResult<object> GetTmdbAlternateOrderingBySeasonID(
        [FromRoute, RegularExpression(@"^[0-9a-fA-F]{32}$")] string seasonID
    )
    {
        var orderingSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
        if (orderingSeason == null)
            return NotFound(OrderingNotFound);

        var ordering = orderingSeason.GetTmdbAlternateOrdering();
        if (ordering == null)
            return NotFound(OrderingNotFoundBySeasonID);

        // TODO: convert this to the v3 model once finalised.
        return ordering;
    }

    [HttpGet("AlternateOrdering/Season/{seasonID}/Episode")]
    public ActionResult<ListResult<object>> GetTmdbEpisodesByAlternateOrderingSeasonID(
        [FromRoute, RegularExpression(@"^[0-9a-fA-F]{32}$")] string seasonID,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var orderingSeason = RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(seasonID);
        if (orderingSeason == null)
            return NotFound(OrderingNotFound);

        // TODO: convert this to the v3 model once finalised.
        return orderingSeason.GetTmdbAlternateOrderingEpisodes()
            .Select(e => (CrossRef: e, Episode: e.GetTmdbEpisode()))
            .Where(e => e.Episode != null)
            .ToListResult(e => e.Episode! as object, page, pageSize);
    }

    #endregion

    #endregion Alternate Ordering Season

    #region Seasons

    #region Constants

    internal const string SeasonNotFound = "A TMDB.Season by the given `seasonID` was not found.";

    internal const string SeasonNotFoundByEpisodeID = "A TMDB.Season by the given `episodeID` was not found.";

    #endregion

    #region Basics

    [HttpGet("Season/{seasonID}")]
    public ActionResult<object> GetTmdbSeasonBySeasonID(
        [FromRoute] int seasonID
    )
    {
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonID);
        if (season == null)
            return NotFound(SeasonNotFound);

        // TODO: convert this to the v3 model once finalised.
        return season;
    }

    #endregion

    #region Same-Source Linked Entries

    [HttpGet("Season/{seasonID}/Show")]
    public ActionResult<object> GetTmdbShowBySeasonID(
        [FromRoute] int seasonID
    )
    {
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonID);
        if (season == null)
            return NotFound(SeasonNotFound);

        var show = season.GetTmdbShow();
        if (show == null)
            return NotFound(ShowNotFoundBySeasonID);

        // TODO: convert this to the v3 model once finalised.
        return show;
    }

    [HttpGet("Season/{seasonID}/Episode")]
    public ActionResult<ListResult<object>> GetTmdbEpisodesBySeasonID(
        [FromRoute] int seasonID,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonID);
        if (season == null)
            return NotFound(SeasonNotFound);

        // TODO: convert this to the v3 model once finalised.
        return season.GetTmdbEpisodes()
            .ToListResult(e => e as object, page, pageSize);
    }

    #endregion

    #region Cross-Source Linked Entries

    [HttpGet("Season/{seasonID}/AniDB/Anime")]
    public ActionResult<List<Series.AniDB>> GetAniDBAnimeBySeasonID(
        [FromRoute] int seasonID
    )
    {
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonID);
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
        [FromRoute] int seasonID,
        [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null
    )
    {
        var season = RepoFactory.TMDB_Season.GetByTmdbSeasonID(seasonID);
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
    public ActionResult<object> GetTmdbEpisodeByEpisodeID(
        [FromRoute] int episodeID
    )
    {
        var episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFound);

        // TODO: convert this to the v3 model once finalised.
        return episode;
    }

    #endregion

    #region Same-Source Linked Entries

    [HttpGet("Episode/{episodeID}/Show")]

    [HttpGet("Episode/{episodeID}/AlternateOrdering")]

    [HttpGet("Episode/{episodeID}/Season")]

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
