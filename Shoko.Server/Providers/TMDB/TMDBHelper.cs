using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Models.Server.TMDB;
using Shoko.Server.Commands;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Providers.TMDB.Search;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using TMDbLib.Client;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.TvShows;
using TMDbLib.Objects.General;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Server;

namespace Shoko.Server.Providers.TMDB;

public class TMDBHelper
{
    private readonly ILogger<TMDBHelper> _logger;

    private readonly ICommandRequestFactory _commandFactory;

    private readonly ISettingsProvider _settingsProvider;

    public readonly TMDBOfflineSearch OfflineSearch;

    private readonly TMDbClient _client;

    private static string _imageServerUrl = null;

    public string ImageServerUrl =>
        _imageServerUrl;

    private const string APIKey = "8192e8032758f0ef4f7caa1ab7b32dd3";

    public TMDBHelper(ILoggerFactory loggerFactory, ICommandRequestFactory commandFactory, ISettingsProvider settingsProvider)
    {
        _logger = loggerFactory.CreateLogger<TMDBHelper>();
        _commandFactory = commandFactory;
        _settingsProvider = settingsProvider;
        _client = new(APIKey);
        OfflineSearch = new(loggerFactory);

        if (string.IsNullOrEmpty(_imageServerUrl))
        {
            var config = _client.GetAPIConfiguration().Result;
            _imageServerUrl = config.Images.SecureBaseUrl + "/{0}";
        }
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

    public List<Movie> SearchMovies(string query)
    {
        var results = _client.SearchMovie(query);

        _logger.LogInformation("Got {Count} of {Results} results", results.Results.Count, results.TotalResults);
        return results.Results
            .Select(result => _client.GetMovie(result.Id))
            .ToList();
    }

    #endregion

    #region Links

    public void AddMovieLink(int animeId, int movieId, int? episodeId = null, bool additiveLink = false, bool isAutomatic = false, bool forceRefresh = false)
    {
        // Remove all existing links.
        if (!additiveLink)
            RemoveAllMovieLinks(animeId);

        // Add or update the link.
        _logger.LogInformation("Adding TMDB Movie Link: AniDB (ID:{AnidbID}) → TvDB Movie (ID:{TmdbID})", animeId, movieId);
        var xref = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeAndTmdbMovieIDs(animeId, movieId) ??
            new(animeId, movieId);
        if (episodeId.HasValue)
            xref.AnidbEpisodeID = episodeId;
        xref.Source = isAutomatic ? CrossRefSource.Automatic : CrossRefSource.User;
        RepoFactory.CrossRef_AniDB_TMDB_Movie.Save(xref);

        // Schedule the movie info to be downloaded or updated.
        _commandFactory.CreateAndSave<CommandRequest_TMDB_Movie_Update>(c =>
        {
            c.TmdbMovieID = movieId;
            c.ForceRefresh = forceRefresh;
            c.DownloadImages = true;
        });
    }

    public void RemoveMovieLink(int animeId, int movieId, bool purge = false)
    {
        if (purge)
        {
            _commandFactory.CreateAndSave<CommandRequest_TMDB_Movie_Purge>(c => c.TmdbMovieID = movieId);
            return;
        }

        var xref = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeAndTmdbMovieIDs(animeId, movieId);
        if (xref == null)
            return;

        // Disable auto-matching when we remove an existing match for the series.
        var series = RepoFactory.AnimeSeries.GetByAnimeID(animeId);
        if (series != null && !series.IsTMDBAutoMatchingDisabled)
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
        if (series != null && !series.IsTMDBAutoMatchingDisabled)
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

    #region Update

    public void UpdateAllMovies(bool force, bool saveImages)
    {
        var allXRefs = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetAll();
        _logger.LogInformation("Scheduling {Count} movies to be updated.", allXRefs.Count);
        foreach (var xref in allXRefs)
            _commandFactory.CreateAndSave<CommandRequest_TMDB_Movie_Update>(
                c =>
                {
                    c.TmdbMovieID = xref.TmdbMovieID;
                    c.ForceRefresh = force;
                    c.DownloadImages = saveImages;
                }
            );
    }

    public async Task UpdateMovie(int movieId, bool forceRefresh = false, bool downloadImages = false)
    {
        // TODO: Abort if we're within a certain time frame as to not try and get us rate-limited.

        var tmdbMovie = await _client.GetMovieAsync(movieId, "en");
        // save to the DB
        var movie = RepoFactory.MovieDb_Movie.GetByOnlineID(tmdbMovie.Id) ?? new();
        movie.Populate(tmdbMovie);

        RepoFactory.MovieDb_Movie.Save(movie);

        await Task.WhenAll(
            UpdateMovieTitlesAndOverviews(tmdbMovie),
            downloadImages ? DownloadMovieImages(movieId) : Task.CompletedTask
        );
    }

    private async Task UpdateMovieTitlesAndOverviews(Movie movie)
    {
        var translations = await _client.GetMovieTranslationsAsync(movie.Id);

        // TODO: Add/update/remove titles and overviews.
    }

    public async Task DownloadMovieImages(int movieId, bool forceDownload = false)
    {
        var settings = _settingsProvider.GetSettings();
        var images = await _client.GetMovieImagesAsync(movieId);
        if (settings.TMDB.AutoDownloadPosters)
            DownloadImagesByType(images.Posters, ImageEntityType_New.Poster, ForeignEntityType.Movie, settings.TMDB.MaxAutoBackdrops, movieId, forceDownload);
        if (settings.TMDB.AutoDownloadLogos)
            DownloadImagesByType(images.Logos, ImageEntityType_New.Logo, ForeignEntityType.Movie, settings.TMDB.MaxAutoBackdrops, movieId, forceDownload);
        if (settings.TMDB.AutoDownloadBackdrops)
            DownloadImagesByType(images.Backdrops, ImageEntityType_New.Backdrop, ForeignEntityType.Movie, settings.TMDB.MaxAutoBackdrops, movieId, forceDownload);
    }

    #endregion

    #region Purge

    public void PurgeAllUnusedMovies()
    {
        var allMovies = RepoFactory.MovieDb_Movie.GetAll().Select(movie => movie.MovieId)
            .Concat(RepoFactory.TMDB_ImageMetadata.GetAll().Where(image => image.TmdbMovieID.HasValue).Select(image => image.TmdbMovieID.Value))
            .ToHashSet();
        var toKeep = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetAll()
            .Select(xref => xref.TmdbMovieID)
            .ToHashSet();
        var toBePurged = allMovies
            .Except(toKeep)
            .ToHashSet();

        _logger.LogInformation("Scheduling {Count} out of {AllCount} movies to be purged.", toBePurged.Count, allMovies.Count);
        foreach (var movieID in toBePurged)
            _commandFactory.CreateAndSave<CommandRequest_TMDB_Movie_Purge>(c => c.TmdbMovieID = movieID);
    }

    /// <summary>
    /// Purge a TMDB movie from the local database.
    /// </summary>
    /// <param name="movieId">TMDB Movie ID.</param>
    /// <param name="removeImageFiles">Remove image files.</param>
    public bool PurgeMovie(int movieId, bool removeImageFiles = true)
    {
        var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByTmdbMovieID(movieId);
        if (xrefs != null && xrefs.Count > 0)
        {
            foreach (var xref in xrefs)
                RemoveMovieLink(xref);
        }

        PurgeMovieImages(movieId, removeImageFiles);

        var movie = RepoFactory.MovieDb_Movie.GetByOnlineID(movieId);
        if (movie != null)
        {
            _logger.LogTrace("Removing movie {MovieName} ({MovieID})", movie.OriginalName, movie.MovieId);
            RepoFactory.MovieDb_Movie.Delete(movie);
        }

        return false;
    }

    private static void PurgeMovieImages(int movieId, bool removeFiles = true)
    {
        var images = RepoFactory.TMDB_ImageMetadata.GetByTmdbMovieID(movieId);
        if (images != null & images.Count > 0)
            foreach (var image in images)
                PurgeImage(image, ForeignEntityType.Movie, removeFiles);
    }

    #endregion

    #endregion

    #region Show

    #region Search

    public List<TvShow> SearchShows(string query)
    {
        // TODO: Implement search after finalising the search model.
        return default;
    }

    #endregion

    #region Links

    public void AddShowLink(int animeId, int showId, string seasonId = null, bool additiveLink = true, bool isAutomatic = false, bool forceRefresh = false)
    {
        // Remove all existing links.
        if (!additiveLink)
            RemoveAllShowLinks(animeId);

        // Add or update the link.
        _logger.LogInformation("Adding TMDB Show Link: AniDB (ID:{AnidbID}) → TvDB Show (ID:{TmdbID})", animeId, showId);
        var xref = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeAndTmdbShowIDs(animeId, showId) ??
            new(animeId, showId);
        if (!string.IsNullOrEmpty(seasonId))
            xref.TmdbSeasonID = seasonId;
        xref.Source = isAutomatic ? CrossRefSource.Automatic : CrossRefSource.User;
        RepoFactory.CrossRef_AniDB_TMDB_Show.Save(xref);

        // Schedule the movie info to be downloaded or updated.
        _commandFactory.CreateAndSave<CommandRequest_TMDB_Show_Update>(c =>
        {
            c.TmdbShowID = showId;
            c.ForceRefresh = true;
            c.DownloadImages = true;
        });
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
        if (series != null && !series.IsTMDBAutoMatchingDisabled)
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
        if (series != null && !series.IsTMDBAutoMatchingDisabled)
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

    #region Update

    public void UpdateAllShows(bool force = false, bool downloadImages = false)
    {
        var allXRefs = RepoFactory.CrossRef_AniDB_TMDB_Show.GetAll();
        _logger.LogInformation("Scheduling {Count} shows to be updated.", allXRefs.Count);
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

    public async Task UpdateShow(int showId, bool force = false, bool downloadEpisodeGroups = false, bool downloadImages = false)
    {
        // TODO: Abort if we're within a certain time frame as to not try and get us rate-limited.

        var show = await _client.GetTvShowAsync(showId);

        // TODO: Update show.

        await Task.WhenAll(
            UpdateShowTitlesAndOverviews(show),
            UpdateShowEpisodes(show),
            UpdateShowSeasons(show),
            downloadEpisodeGroups ? UpdateShowEpisodeGroups(show) : Task.CompletedTask,
            downloadImages ? DownloadShowImages(showId) : Task.CompletedTask
        );
    }

    private async Task UpdateShowTitlesAndOverviews(TvShow show)
    {
        var translations = await _client.GetTvShowTranslationsAsync(show.Id);

        // TODO: Add/update/remove show titles.
    }

    private async Task UpdateShowEpisodes(TvShow show)
    {
        // TODO: Update TMDB episodes, check for xrefs, auto-add xrefs that does not exist, etc.
    }

    private async Task UpdateShowSeasons(TvShow show)
    {
        // TODO: Update TMDB seasons.
    }

    private async Task UpdateShowEpisodeGroups(TvShow show)
    {
        // TODO: Update TMDB episode groups.
    }

    public async Task DownloadShowImages(int showId, bool forceDownload = false)
    {
        var settings = _settingsProvider.GetSettings();
        var images = await _client.GetTvShowImagesAsync(showId);
        if (settings.TMDB.AutoDownloadPosters)
            DownloadImagesByType(images.Posters, ImageEntityType_New.Poster, ForeignEntityType.Show, settings.TMDB.MaxAutoBackdrops, showId, forceDownload);
        if (settings.TMDB.AutoDownloadLogos)
            DownloadImagesByType(images.Logos, ImageEntityType_New.Logo, ForeignEntityType.Show, settings.TMDB.MaxAutoBackdrops, showId, forceDownload);
        if (settings.TMDB.AutoDownloadBackdrops)
            DownloadImagesByType(images.Backdrops, ImageEntityType_New.Backdrop, ForeignEntityType.Show, settings.TMDB.MaxAutoBackdrops, showId, forceDownload);
    }

    #endregion

    #region Purge

    public void PurgeAllUnusedShows()
    {
        // TODO: Implement this logic once the show tables are added and the repositories are set up.
        var allShows = new HashSet<int>();
        var toKeep = RepoFactory.CrossRef_AniDB_TMDB_Show.GetAll()
            .Select(xref => xref.TmdbShowID)
            .ToHashSet();
        var toBePurged = allShows
            .Except(toKeep)
            .ToHashSet();

        _logger.LogInformation("Scheduling {Count} out of {AllCount} shows to be purged.", toBePurged.Count, allShows.Count);
        foreach (var showID in toBePurged)
            _commandFactory.CreateAndSave<CommandRequest_TMDB_Show_Purge>(c => c.TmdbShowID = showID);
    }

    public bool PurgeShow(int showId, bool removeImageFiles = true)
    {
        var xrefs = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByTmdbShowID(showId);
        if (xrefs != null && xrefs.Count > 0)
        {
            foreach (var xref in xrefs)
                RemoveShowLink(xref);
        }

        PurgeShowImages(showId);

        PurgeShowEpisodes(showId);

        PurgeShowSeasons(showId);

        PurgeShowEpisodeGroups(showId);

        // TODO: Remove show.

        return false;
    }

    private static void PurgeShowImages(int showId, bool removeFiles = true)
    {
        var images = RepoFactory.TMDB_ImageMetadata.GetByTmdbShowID(showId);
        if (images != null & images.Count > 0)
            foreach (var image in images)
                PurgeImage(image, ForeignEntityType.Movie, removeFiles);
    }

    private static void PurgeShowEpisodes(int showId, bool removeImageFiles = true)
    {
        // TODO: Remove Episodes and their images.
    }

    private static void PurgeShowSeasons(int showId)
    {
        // TODO: Remove Seasons.
    }

    private static void PurgeShowEpisodeGroups(int showId)
    {
        // TODO: Remove all episode groups.
    }

    #endregion

    #endregion

    #region Shared

    #region Image

    private void DownloadImagesByType(IReadOnlyList<ImageData> images, ImageEntityType_New type, ForeignEntityType foreignType, int maxCount, int episodeId, bool forceDownload = false)
    {
        var count = 0;
        foreach (var imageData in images)
        {
            if (count >= maxCount)
                break;

            var image = RepoFactory.TMDB_ImageMetadata.GetByRemoteFileNameAndType(imageData.FilePath, type) ?? new(type);
            image.Populate(imageData, foreignType, episodeId);
            RepoFactory.TMDB_ImageMetadata.Save(image);

            var path = image.AbsolutePath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                count++;
        }

        foreach (var image in RepoFactory.TMDB_ImageMetadata.GetByForeignIDAndType(episodeId, foreignType, type))
        {
            var path = image.AbsolutePath;
            if (count < maxCount)
            {
                // Clean up outdated entries.
                if (string.IsNullOrEmpty(path))
                {
                    RepoFactory.TMDB_ImageMetadata.Delete(image.TMDB_ImageMetadataID);
                    continue;
                }

                // Skip downloading if it already exists.
                if (File.Exists(path))
                {
                    count++;
                    continue;
                }

                // Scheduled the image to be downloaded.
                _commandFactory.CreateAndSave<CommandRequest_DownloadImage>(c =>
                {
                    c.EntityID = image.TMDB_ImageMetadataID;
                    c.DataSourceEnum = DataSourceEnum.TMDB;
                    c.ForceDownload = forceDownload;
                });
                count++;
            }
            // Keep it if it's already downloaded, otherwise remove the metadata.
            else if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                RepoFactory.TMDB_ImageMetadata.Delete(image.TMDB_ImageMetadataID);
            }
        }
    }

    private static void PurgeImage(TMDB_ImageMetadata image, ForeignEntityType foreignType, bool removeFile)
    {
        // Skip the operation if th flag is not set.
        if (!image.ForeignType.HasFlag(foreignType))
            return;

        // Disable the flag.
        image.ForeignType &= ~foreignType;

        // Only delete the image metadata and/or file if all references were removed.
        if (image.ForeignType == ForeignEntityType.None)
        {
            if (removeFile && !string.IsNullOrEmpty(image.AbsolutePath) && File.Exists(image.AbsolutePath))
                File.Delete(image.AbsolutePath);

            RepoFactory.TMDB_ImageMetadata.Delete(image.TMDB_ImageMetadataID);
        }
        // Remove the ID since we're keeping the metadata a little bit longer.
        else
        {
            switch (foreignType)
            {
                case ForeignEntityType.Movie:
                    image.TmdbMovieID = null;
                    break;
                case ForeignEntityType.Episode:
                    image.TmdbEpisodeID = null;
                    break;
                case ForeignEntityType.Season:
                    image.TmdbSeasonID = null;
                    break;
                case ForeignEntityType.Show:
                    image.TmdbShowID = null;
                    break;
                case ForeignEntityType.Collection:
                    image.TmdbCollectionID = null;
                    break;
            }
        }
    }

    #endregion

    #endregion
}
