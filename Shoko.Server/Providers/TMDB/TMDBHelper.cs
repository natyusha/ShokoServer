using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Commands;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using TMDbLib.Client;

namespace Shoko.Server.Providers.TMDB;

public class TMDBHelper
{
    private readonly ILogger<TMDBHelper> _logger;
    private readonly ICommandRequestFactory _commandFactory;
    private readonly ISettingsProvider _settingsProvider;
    private readonly IConnectivityService _connectivityService;

    private const string APIKey = "8192e8032758f0ef4f7caa1ab7b32dd3";

    public TMDBHelper(ILogger<TMDBHelper> logger, ICommandRequestFactory commandFactory, ISettingsProvider settingsProvider, IConnectivityService connectivityService)
    {
        _logger = logger;
        _commandFactory = commandFactory;
        _settingsProvider = settingsProvider;
        _connectivityService = connectivityService;
    }

    public void ScanForMatches()
    {
        var settings = _settingsProvider.GetSettings();
        if (!settings.TvDB.AutoLink)
            return;

        var allSeries = RepoFactory.AnimeSeries.GetAll();
        foreach (var ser in allSeries)
        {
            if (ser.IsTMDBAutoMatchingDisabled)
                continue;

            var anime = ser.GetAnime();
            if (anime == null)
                continue;

            if (anime.Restricted > 0)
                continue;

            if (anime.GetCrossRefTmdbMovies().Count > 0)
                continue;

            if (anime.GetCrossRefTmdbShows().Count > 0)
                continue;

            _logger.LogTrace("Found anime without TMDB association: {MainTitle}", anime.MainTitle);

            _commandFactory.CreateAndSave<CommandRequest_TMDB_Search>(c => c.AnimeID = ser.AniDB_ID);
        }
    }

    #region Movies

    #region Search

    public List<TMDB_Movie_Result> SearchMovies(string query)
    {
        var results = new List<TMDB_Movie_Result>();

        try
        {
            var client = new TMDbClient(APIKey);
            var resultsTemp = client.SearchMovie(query);

            _logger.LogInformation("Got {Count} of {Results} results", resultsTemp.Results.Count,
                resultsTemp.TotalResults);
            foreach (var result in resultsTemp.Results)
            {
                var searchResult = new TMDB_Movie_Result();
                var movie = client.GetMovie(result.Id);
                var imgs = client.GetMovieImages(result.Id);
                searchResult.Populate(movie, imgs);
                results.Add(searchResult);
                SaveMovieToDatabase(searchResult, false, false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TMDB Movie Search");
        }

        return results;
    }

    #endregion

    #region Update

    public void UpdateAllMovies(bool force, bool saveImages)
    {
        using var session = DatabaseFactory.SessionFactory.OpenSession();
        var allXRefs = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetAll();
        foreach (var xref in allXRefs)
        {
            _commandFactory.CreateAndSave<CommandRequest_TMDB_Movie_Update>(
                c =>
                {
                    c.TmdbMovieID = xref.TmdbMovieID;
                    c.ForceRefresh = force;
                    c.DownloadImages = saveImages;
                }
            );
        }
    }

    public void UpdateMovie(int movieID, bool force = false, bool downloadImages = false)
    {
        var client = new TMDbClient(APIKey);
        var movie = client.GetMovie(movieID);
        var imgs = client.GetMovieImages(movieID);

        var searchResult = new TMDB_Movie_Result();
        searchResult.Populate(movie, imgs);

        SaveMovieToDatabase(searchResult, downloadImages, false);
    }

    private void SaveMovieToDatabase(TMDB_Movie_Result searchResult, bool saveImages, bool isTrakt)
    {
        // save to the DB
        var movie = RepoFactory.MovieDb_Movie.GetByOnlineID(searchResult.MovieID) ?? new MovieDB_Movie();
        movie.Populate(searchResult);

        // Only save movie info if source is not trakt, this presents adding tv shows as movies
        // Needs better fix later on

        if (!isTrakt)
        {
            RepoFactory.MovieDb_Movie.Save(movie);
        }

        if (!saveImages)
        {
            return;
        }

        var numFanartDownloaded = 0;
        var numPostersDownloaded = 0;

        // save data to the DB and determine the number of images we already have
        foreach (var img in searchResult.Images)
        {
            if (img.ImageType.Equals("poster", StringComparison.InvariantCultureIgnoreCase))
            {
                var poster = RepoFactory.MovieDB_Poster.GetByOnlineID(img.URL) ?? new MovieDB_Poster();
                poster.Populate(img, movie.MovieId);
                RepoFactory.MovieDB_Poster.Save(poster);

                if (!string.IsNullOrEmpty(poster.GetFullImagePath()) && File.Exists(poster.GetFullImagePath()))
                {
                    numPostersDownloaded++;
                }
            }
            else
            {
                // fanart (backdrop)
                var fanart = RepoFactory.MovieDB_Fanart.GetByOnlineID(img.URL) ?? new MovieDB_Fanart();
                fanart.Populate(img, movie.MovieId);
                RepoFactory.MovieDB_Fanart.Save(fanart);

                if (!string.IsNullOrEmpty(fanart.GetFullImagePath()) && File.Exists(fanart.GetFullImagePath()))
                {
                    numFanartDownloaded++;
                }
            }
        }

        // download the posters
        var settings = _settingsProvider.GetSettings();
        if (settings.TMDB.AutoPosters || isTrakt)
        {
            foreach (var poster in RepoFactory.MovieDB_Poster.GetByMovieID(movie.MovieId))
            {
                if (numPostersDownloaded < settings.TMDB.AutoPostersAmount)
                {
                    // download the image
                    if (string.IsNullOrEmpty(poster.GetFullImagePath()) || File.Exists(poster.GetFullImagePath()))
                    {
                        continue;
                    }

                    _commandFactory.CreateAndSave<CommandRequest_DownloadImage>(
                        c =>
                        {
                            c.EntityID = poster.MovieDB_PosterID;
                            c.EntityType = (int)ImageEntityType.MovieDB_Poster;
                        }
                    );
                    numPostersDownloaded++;
                }
                else
                {
                    //The MovieDB_AutoPostersAmount should prevent from saving image info without image
                    // we should clean those image that we didn't download because those dont exists in local repo
                    // first we check if file was downloaded
                    if (!File.Exists(poster.GetFullImagePath()))
                    {
                        RepoFactory.MovieDB_Poster.Delete(poster.MovieDB_PosterID);
                    }
                }
            }
        }

        // download the fanart
        if (settings.TMDB.AutoFanart || isTrakt)
        {
            foreach (var fanart in RepoFactory.MovieDB_Fanart.GetByMovieID(movie.MovieId))
            {
                if (numFanartDownloaded < settings.TMDB.AutoFanartAmount)
                {
                    // download the image
                    if (string.IsNullOrEmpty(fanart.GetFullImagePath()) || File.Exists(fanart.GetFullImagePath()))
                    {
                        continue;
                    }

                    _commandFactory.CreateAndSave<CommandRequest_DownloadImage>(
                        c =>
                        {
                            c.EntityID = fanart.MovieDB_FanartID;
                            c.EntityType = (int)ImageEntityType.MovieDB_FanArt;
                        }
                    );
                    numFanartDownloaded++;
                }
                else
                {
                    //The MovieDB_AutoFanartAmount should prevent from saving image info without image
                    // we should clean those image that we didn't download because those dont exists in local repo
                    // first we check if file was downloaded
                    if (!File.Exists(fanart.GetFullImagePath()))
                    {
                        RepoFactory.MovieDB_Fanart.Delete(fanart.MovieDB_FanartID);
                    }
                }
            }
        }
    }

    #endregion

    #region Links

    public void AddMovieLink(int animeId, int movieId, int? episodeId = null, bool additiveLink = false, bool isAutomatic = false)
    {
        // Remove all existing links.
        if (!additiveLink)
            RemoveAllMovieLinks(animeId);

        // Update movie info now if we have internet, otherwise schedule an
        // update for later.
        if (!_connectivityService.NetworkAvailability.HasInternet())
            _commandFactory.CreateAndSave<CommandRequest_TMDB_Movie_Update>(c =>
            {
                c.TmdbMovieID = movieId;
                c.ForceRefresh = true;
                c.DownloadImages = true;
            });
        else
            UpdateMovie(movieId, downloadImages: true);

        // Add or update the link.
        _logger.LogInformation("Adding TMDB Movie Link: AniDB (ID:{AnidbID}) → TvDB Movie (ID:{TmdbID})", animeId, movieId);
        var xref = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeAndTmdbMovieIDs(animeId, movieId) ??
            new(animeId, movieId);
        if (episodeId.HasValue)
            xref.AnidbEpisodeID = episodeId;
        xref.Source = isAutomatic ? CrossRefSource.Automatic : CrossRefSource.User;
        RepoFactory.CrossRef_AniDB_TMDB_Movie.Save(xref);
    }

    public void RemoveMovieLink(int animeId, int movieId, bool purge = false)
    {
        if (purge)
        {
            PurgeMovie(movieId);
            return;
        }

        var xref = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeAndTmdbMovieIDs(animeId, movieId);
        if (xref == null)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        var series = RepoFactory.AnimeSeries.GetByAnimeID(animeId);
        if (series != null)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            RepoFactory.AnimeSeries.Save(series, false, true, true);
        }

        RemoveMovieLink(xref);
    }

    public void RemoveAllMovieLinks(int animeId, bool purge = false)
    {
        _logger.LogInformation("Removing All TMDB Movie Links for: {AnimeID}", animeId);
        var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID(animeId);
        if (xrefs == null || xrefs.Count == 0)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        var series = RepoFactory.AnimeSeries.GetByAnimeID(animeId);
        if (series != null)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            RepoFactory.AnimeSeries.Save(series, false, true, true);
        }

        foreach (var xref in xrefs)
            if (purge)
                PurgeMovie(xref.TmdbMovieID);
            else
                RemoveMovieLink(xref);
    }

    private void RemoveMovieLink(CrossRef_AniDB_TMDB_Movie xref)
    {
        // TODO: Reset default image for the anime if it belongs to the movie.

        _logger.LogInformation("Removing TMDB Movie Link: AniDB ({AnidbID}) → TMDB Movie (ID:{TmdbID})", xref.AnidbAnimeID, xref.TmdbMovieID);
        RepoFactory.CrossRef_AniDB_TMDB_Movie.Delete(xref);
    }

    #endregion

    #region Purge

    /// <summary>
    /// Purge a TMDB movie from the local database.
    /// </summary>
    /// <param name="movieId">TMDB Movie ID.</param>
    public void PurgeMovie(int movieId)
    {
        // TODO: Add implementation to remove movie and clean up xrefs, images, etc.
    }

    #endregion

    #endregion

    #region Show

    public List<object> SearchShows(string query)
    {
        // TODO: Implement search after finalising the search model.
        return default;
    }

    #region Update

    public void UpdateAllShows(bool force = false, bool downloadImages = false)
    {
        using var session = DatabaseFactory.SessionFactory.OpenSession();
        var allXRefs = RepoFactory.CrossRef_AniDB_TMDB_Show.GetAll();
        foreach (var xref in allXRefs)
        {
            _commandFactory.CreateAndSave<CommandRequest_TMDB_Show_Update>(
                c =>
                {
                    c.TmdbShowID = xref.TmdbShowID;
                    c.ForceRefresh = force;
                    c.DownloadImages = downloadImages;
                }
            );
        }
    }

    public void UpdateShow(int showId, bool force = false, bool downloadImages = false)
    {
        // TODO: Update show.
    }

    #endregion

    #region Links

    public void AddShowLink(int animeId, int showId, string seasonId = null, bool additiveLink = true, bool isAutomatic = false)
    {
        // Remove all existing links.
        if (!additiveLink)
            RemoveAllShowLinks(animeId);

        // Update show info now if we have internet, otherwise schedule an
        // update for later.
        if (!_connectivityService.NetworkAvailability.HasInternet())
            _commandFactory.CreateAndSave<CommandRequest_TMDB_Show_Update>(c =>
            {
                c.TmdbShowID = showId;
                c.ForceRefresh = true;
                c.DownloadImages = true;
            });
        else
            UpdateShow(showId, downloadImages: true);

        // Add or update the link.
        _logger.LogInformation("Adding TMDB Show Link: AniDB (ID:{AnidbID}) → TvDB Show (ID:{TmdbID})", animeId, showId);
        var xref = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeAndTmdbShowIDs(animeId, showId) ??
            new(animeId, showId);
        if (!string.IsNullOrEmpty(seasonId))
            xref.TmdbSeasonID = seasonId;
        xref.Source = isAutomatic ? CrossRefSource.Automatic : CrossRefSource.User;
        RepoFactory.CrossRef_AniDB_TMDB_Show.Save(xref);
    }

    public void RemoveShowLink(int animeId, int showId, bool purge = false)
    {
        if (purge)
        {
            PurgeShow(showId);
            return;
        }

        var xref = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeAndTmdbShowIDs(animeId, showId);
        if (xref == null)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        var series = RepoFactory.AnimeSeries.GetByAnimeID(animeId);
        if (series != null)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            RepoFactory.AnimeSeries.Save(series, false, true, true);
        }

        RemoveShowLink(xref);
    }

    public void RemoveAllShowLinks(int animeId, bool purge = false)
    {
        _logger.LogInformation("Removing All TMDB Show Links for: {AnimeID}", animeId);
        var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID(animeId);
        if (xrefs == null || xrefs.Count == 0)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        var series = RepoFactory.AnimeSeries.GetByAnimeID(animeId);
        if (series != null)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            RepoFactory.AnimeSeries.Save(series, false, true, true);
        }

        foreach (var xref in xrefs)
            if (purge)
                PurgeShow(xref.TmdbShowID);
            else
                RemoveShowLink(xref);
    }

    private void RemoveShowLink(CrossRef_AniDB_TMDB_Show xref)
    {
        // TODO: Reset default images if any are set.

        _logger.LogInformation("Removing TMDB Show Link: AniDB ({AnidbID}) → TMDB Show (ID:{TmdbID})", xref.AnidbAnimeID, xref.TmdbShowID);
        RepoFactory.CrossRef_AniDB_TMDB_Show.Delete(xref);

        // TODO: Remove episode xrefs for anime episodes to show episodes.
    }

    #endregion

    #region Purge

    public bool PurgeShow(int showId)
    {

        RemoveAllShowLinks(showId, false);

        PurgeShowImages(showId);

        PurgeShowEpisodes(showId);

        PurgeShowSeasons(showId);

        // TODO: Remove show.

        return false;
    }

    private static void PurgeShowImages(int showId)
    {
        // TODO: Remove Images.
    }

    private static void PurgeShowEpisodes(int showId)
    {
        // TODO: Remove Episodes.
    }

    private static void PurgeShowSeasons(int showId)
    {
        // TODO: Remove Seasons.
    }

    #endregion

    #endregion
}
