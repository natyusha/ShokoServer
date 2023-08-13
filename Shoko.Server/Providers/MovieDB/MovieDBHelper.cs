using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;
using TMDbLib.Client;

#nullable enable
namespace Shoko.Server.Providers.MovieDB;

public class MovieDBHelper : IMovieMetadataProvider<MovieDB_Movie, MovieDB_Movie_Result, int>, IShowMetadataProvider<object, object, int>
{
    private readonly ILogger<MovieDBHelper> _logger;
    private readonly ICommandRequestFactory _commandFactory;
    private const string APIKey = "8192e8032758f0ef4f7caa1ab7b32dd3";

    private TMDbClient _tmdbClient = new TMDbClient(APIKey);

    public MovieDBHelper(ILogger<MovieDBHelper> logger, ICommandRequestFactory commandFactory)
    {
        _logger = logger;
        _commandFactory = commandFactory;
    }
    
    #region Movies

    public MovieDB_Movie? GetMovieMetadata(int tmdbMovieID)
    {
        return RepoFactory.TMDB_Movie.GetByMovieId(tmdbMovieID);
    }

    public IReadOnlyList<MovieDB_Movie> GetMovieMetadataForAnime(int anidbAnimeID)
    {
        return RepoFactory.CR_AniDB_Other.GetByAnimeIDAndType(anidbAnimeID, CrossRefType.MovieDB)
            .Select(xref => RepoFactory.TMDB_Movie.GetByMovieId(int.Parse(xref.CrossRefID)))
            .Where(tmdbMovie => tmdbMovie != null)
            .ToList();
    }

    public IReadOnlyList<MovieDB_Movie_Result> SearchMovieMetadata(string criteria)
    {
        var results = new List<MovieDB_Movie_Result>();

        try
        {
            var resultsTemp = _tmdbClient.SearchMovie(HttpUtility.UrlDecode(criteria));

            _logger.LogInformation("Got {Count} of {Results} results", resultsTemp.Results.Count,
                resultsTemp.TotalResults);
            foreach (var result in resultsTemp.Results)
            {
                var searchResult = new MovieDB_Movie_Result();
                var movie = _tmdbClient.GetMovie(result.Id);
                var imgs = _tmdbClient.GetMovieImages(result.Id);
                searchResult.Populate(movie, imgs);
                results.Add(searchResult);
                SaveMovieToDatabase(searchResult, false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MovieDB Search");
        }

        return results;
    }
    private void SaveMovieToDatabase(MovieDB_Movie_Result searchResult, bool saveImages)
    {
        // save to the DB
        var movie = RepoFactory.TMDB_Movie.GetByOnlineID(searchResult.MovieId) ?? new MovieDB_Movie();
        movie.Populate(searchResult);

        RepoFactory.TMDB_Movie.Save(movie);

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
                var poster = RepoFactory.TMDB_Movie_Poster.GetByOnlineID(img.URL) ?? new MovieDB_Poster();
                poster.Populate(img, movie.MovieId);
                RepoFactory.TMDB_Movie_Poster.Save(poster);

                if (!string.IsNullOrEmpty(poster.GetFullImagePath()) && File.Exists(poster.GetFullImagePath()))
                {
                    numPostersDownloaded++;
                }
            }
            else
            {
                // fanart (backdrop)
                var fanart = RepoFactory.TMDB_Fanart.GetByOnlineID(img.URL) ?? new MovieDB_Fanart();
                fanart.Populate(img, movie.MovieId);
                RepoFactory.TMDB_Fanart.Save(fanart);

                if (!string.IsNullOrEmpty(fanart.GetFullImagePath()) && File.Exists(fanart.GetFullImagePath()))
                {
                    numFanartDownloaded++;
                }
            }
        }

        // download the posters
        var settings = Utils.SettingsProvider.GetSettings();
        if (settings.TMDB.AutoPosters)
        {
            foreach (var poster in RepoFactory.TMDB_Movie_Poster.GetByMovieID( movie.MovieId))
            {
                if (numPostersDownloaded < settings.TMDB.AutoPostersAmount)
                {
                    // download the image
                    if (string.IsNullOrEmpty(poster.GetFullImagePath()) || File.Exists(poster.GetFullImagePath()))
                    {
                        continue;
                    }

                    var cmd = _commandFactory.Create<CommandRequest_DownloadImage>(
                        c =>
                        {
                            c.EntityID = poster.MovieDB_PosterID;
                            c.EntityType = (int)ImageEntityType.MovieDB_Poster;
                        }
                    );
                    cmd.Save();
                    numPostersDownloaded++;
                }
                else
                {
                    //The MovieDB_AutoPostersAmount should prevent from saving image info without image
                    // we should clean those image that we didn't download because those dont exists in local repo
                    // first we check if file was downloaded
                    if (!File.Exists(poster.GetFullImagePath()))
                    {
                        RepoFactory.TMDB_Movie_Poster.Delete(poster.MovieDB_PosterID);
                    }
                }
            }
        }

        // download the fanart
        if (settings.TMDB.AutoFanart)
        {
            foreach (var fanart in RepoFactory.TMDB_Fanart.GetByMovieID(movie.MovieId))
            {
                if (numFanartDownloaded < settings.TMDB.AutoFanartAmount)
                {
                    // download the image
                    if (string.IsNullOrEmpty(fanart.GetFullImagePath()) || File.Exists(fanart.GetFullImagePath()))
                    {
                        continue;
                    }

                    var cmd = _commandFactory.Create<CommandRequest_DownloadImage>(
                        c =>
                        {
                            c.EntityID = fanart.MovieDB_FanartID;
                            c.EntityType = (int)ImageEntityType.MovieDB_FanArt;
                        }
                    );
                    cmd.Save();
                    numFanartDownloaded++;
                }
                else
                {
                    //The MovieDB_AutoFanartAmount should prevent from saving image info without image
                    // we should clean those image that we didn't download because those dont exists in local repo
                    // first we check if file was downloaded
                    if (!File.Exists(fanart.GetFullImagePath()))
                    {
                        RepoFactory.TMDB_Fanart.Delete(fanart.MovieDB_FanartID);
                    }
                }
            }
        }
    }

    public void UpdateAllMovieInfo()
    {
        using var session = DatabaseFactory.SessionFactory.OpenSession();
        var all = RepoFactory.TMDB_Movie.GetAll();
        var max = all.Count;
        var i = 0;
        foreach (var movie in all)
        {
            try
            {
                i++;
                _logger.LogInformation("Updating MovieDB Movie {I}/{Max}", i, max);
                RefreshMovieMetadata(movie.MovieId);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to Update MovieDB Movie ID: {Id}", movie.MovieId);
            }
        }
    }
    
    public void RefreshMovieMetadata(int tmdbMovieID)
        => UpdateMovieInfo(tmdbMovieID, true);

    private void UpdateMovieInfo(int movieID, bool saveImages)
    {
        try
        {
            var client = new TMDbClient(APIKey);
            var movie = client.GetMovie(movieID);
            var imgs = client.GetMovieImages(movieID);

            var searchResult = new MovieDB_Movie_Result();
            searchResult.Populate(movie, imgs);

            // save to the DB
            SaveMovieToDatabase(searchResult, saveImages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UpdateMovieInfo");
        }
    }

    public bool AddMovieLink(int anidbAnimeID, int providerMovieID, bool replaceExisting = false)
        => AddMovieLinks(anidbAnimeID, new int[] { providerMovieID }, replaceExisting) == 1;

    public int AddMovieLinks(int anidbAnimeID, IReadOnlyList<int> tmdbMovieIDs, bool replaceExisting = false)
    {
        // Remove the existing cross-references now if we're replacing them.
        if (replaceExisting)
            RemoveMovieLinks(anidbAnimeID);

        // Disable auto-matching when we adding a match for the series.
        var series = RepoFactory.Shoko_Series.GetByAnidbAnimeId(anidbAnimeID);
        if (series != null && !series.IsTMDBAutoMatchingDisabled)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            RepoFactory.Shoko_Series.Save(series, false, true, true);
        }

        // Add the movies.
        var moviesAdded = 0;
        foreach (var tmdbMovieID in tmdbMovieIDs)
        {
            // Check if we have this information locally, and if not download it
            // now.
            var movie = RepoFactory.TMDB_Movie.GetByMovieId(tmdbMovieID);
            if (movie == null)
            {
                // We download the series info here just so that we have the basic
                // info in the database before the queued task runs later.
                UpdateMovieInfo(tmdbMovieID, false);
                movie = RepoFactory.TMDB_Movie.GetByMovieId(tmdbMovieID);
                // Abort now if we for some reason weren't able to create the movie.
                if (movie == null)
                {
                    _logger.LogWarning($"Unable to aquire TMDB movie with id {tmdbMovieID}. Aborting cross-reference linking.");
                    continue;
                }
            }

            // Download and update series info (again), but this time also with
            // images.
            UpdateMovieInfo(tmdbMovieID, true);

            // Return early if we already have an assosiation between the anidb and
            // tmdb movie set.
            var allXRefs = RepoFactory.CR_AniDB_Other.GetByAnimeIDAndType(anidbAnimeID, CrossRefType.MovieDB);

            var currentXRef = allXRefs.FirstOrDefault(xref => string.Equals(xref.CrossRefID, tmdbMovieID.ToString()));
            if (currentXRef != null)
                continue;

            _logger.LogTrace("Adding tmdb movie association: {AnimeID} → {TMDBMovieID}", anidbAnimeID, tmdbMovieID);
            // Create a new reference, save it, and update the series stats.
            currentXRef = new()
            {
                AnimeID = anidbAnimeID,
                CrossRefSource = (int)CrossRefSource.User,
                CrossRefType = (int)CrossRefType.MovieDB,
                CrossRefID = tmdbMovieID.ToString(),
            };
            RepoFactory.CR_AniDB_Other.Save(currentXRef);
            moviesAdded++;
        }

        AniDB_Anime.UpdateStatsByAnimeID(anidbAnimeID);

        return moviesAdded;
    }

    public bool RemoveMovieLink(int anidbAnimeID, int tmdbMovieID)
        => RemoveMovieLinks(anidbAnimeID, new int[] { tmdbMovieID }) == 1;

    public int RemoveMovieLinks(int anidbAnimeID, IReadOnlyList<int>? tmdbMovieIDs = null)
    {
        // Disable auto-matching when we remove an existing match for the series.
        var series = RepoFactory.Shoko_Series.GetByAnidbAnimeId(anidbAnimeID);
        if (series != null && !series.IsTMDBAutoMatchingDisabled)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            RepoFactory.Shoko_Series.Save(series, false, true, true);
        }

        // Fetch every cross-reference related to the anidb entry.
        var tmdbMovieIDSet = tmdbMovieIDs?.ToHashSet() ?? new();
        var xrefs = RepoFactory.CR_AniDB_Other.GetByAnimeIDAndType(anidbAnimeID, CrossRefType.MovieDB);

        // Filter the entries based on the input.
        if (tmdbMovieIDs != null && tmdbMovieIDs.Count > 0)
        {
            xrefs = xrefs
                .Where(xref => int.TryParse(xref.CrossRefID, out var intID) ? tmdbMovieIDSet.Contains(intID) : false)
                .ToList();
        }

        // Readjust the id set.
        tmdbMovieIDSet = xrefs.Select(xref => int.Parse(xref.CrossRefID)).ToHashSet();

        // Remove the default image override if it was linked to one of the tmdb movies.
        var images = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(anidbAnimeID);
        foreach (var defaultImage in images)
        {
            switch ((ImageEntityType)defaultImage.ImageParentType)
            {
                case ImageEntityType.MovieDB_FanArt:
                {
                    var fanart = RepoFactory.TMDB_Fanart.GetByID(defaultImage.ImageParentID);
                    if (fanart == null || tmdbMovieIDSet.Contains(fanart.MovieId))
                        RepoFactory.AniDB_Anime_DefaultImage.Delete(defaultImage);
                    break;
                }
                case ImageEntityType.MovieDB_Poster:
                {
                    var poster = RepoFactory.TMDB_Movie_Poster.GetByID(defaultImage.ImageParentID);
                    if (poster == null || tmdbMovieIDSet.Contains(poster.MovieId))
                        RepoFactory.AniDB_Anime_DefaultImage.Delete(defaultImage);
                    break;
                }
            }
        }

        // Remove the cross-references.
        foreach (var xref in xrefs)
            RepoFactory.CR_AniDB_Other.Delete(xref);

        // Update the series stats.
        AniDB_Anime.UpdateStatsByAnimeID(anidbAnimeID);

        return tmdbMovieIDSet.Count;
    }

    public void ScanForMovieMatches()
    {
        var allSeries = RepoFactory.Shoko_Series.GetAll();

        foreach (var ser in allSeries)
        {
            if (ser.IsTMDBAutoMatchingDisabled)
                continue;

            var anime = ser.GetAnime();
            if (anime == null)
                continue;

            // don't scan if it is associated on the TvDB
            if (anime.GetCrossRefTvDB().Count > 0)
                continue;

            // don't scan if it is associated on the MovieDB
            if (anime.GetCrossRefMovieDB().Count > 0)
                continue;

            // don't scan if it is not a movie
            if (!anime.GetSearchOnMovieDB())
                continue;

            _logger.LogTrace("Found anime movie without MovieDB association: {MainTitle}", anime.MainTitle);

            var cmd = _commandFactory.Create<CommandRequest_MovieDBSearchAnime>(c => c.AnimeID = ser.AniDB_ID);
            cmd.Save();
        }
    }

    #endregion
    
    #region Shows

    public bool AddShowLink(int anidbAnimeID, int tmdbShowID, bool replaceExisting = false)
        => AddShowLinks(anidbAnimeID, new int[] { tmdbShowID }, replaceExisting) == 1;

    public int AddShowLinks(int anidbAnimeID, IReadOnlyList<int> tmdbShowIDs, bool replaceExisting = false)
    {
        // Remove the existing cross-references now if we're replacing them.
        if (replaceExisting)
            RemoveShowLinks(anidbAnimeID);
        
        // Disable auto-matching when we adding a match for the series.
        var series = RepoFactory.Shoko_Series.GetByAnidbAnimeId(anidbAnimeID);
        if (series != null && !series.IsTMDBAutoMatchingDisabled)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            RepoFactory.Shoko_Series.Save(series, false, true, true);
        }

        // Add the shows.
        var showsAdded = 0;
        foreach (var tmdbMovieID in tmdbShowIDs)
        {
            // TODO: ADD THIS LATER!
        }

        AniDB_Anime.UpdateStatsByAnimeID(anidbAnimeID);
        
        return showsAdded;
    }

    public bool RemoveShowLink(int anidbAnimeID, int tmdbShowID)
        => RemoveShowLinks(anidbAnimeID, new int[] { tmdbShowID }) == 1;

    public int RemoveShowLinks(int anidbAnimeID, IReadOnlyList<int>? tmdbShowIDs = null)
    {
        // Disable auto-matching when we remove an existing match for the series.
        var series = RepoFactory.Shoko_Series.GetByAnidbAnimeId(anidbAnimeID);
        if (series != null && !series.IsTMDBAutoMatchingDisabled)
        {
            series.IsTMDBAutoMatchingDisabled = true;
            RepoFactory.Shoko_Series.Save(series, false, true, true);
        }

        // Fetch every cross-reference related to the anidb entry.
        var tmdbShowIDSet = tmdbShowIDs?.ToHashSet() ?? new();
        var xrefs = new List<object>(); // TODO: FIX THIS LINE.

        // Filter the entries based on the input.
        if (tmdbShowIDs != null && tmdbShowIDs.Count > 0)
        {
            xrefs = xrefs
                .Where(xref => false) // TODO: FIX THIS LINE.
                .ToList();
        }

        // Readjust the id set.
        tmdbShowIDSet = xrefs.Select(xref => 1).ToHashSet(); // TODO: FIX THIS LINE.

        // Remove the default image override if it was linked to one of the tmdb movies.
        var images = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(anidbAnimeID);
        foreach (var defaultImage in images)
        {
            switch ((ImageEntityType)defaultImage.ImageParentType)
            {
                case ImageEntityType.MovieDB_FanArt:
                {
                    var fanart = RepoFactory.TMDB_Fanart.GetByID(defaultImage.ImageParentID);
                    if (fanart == null || tmdbShowIDSet.Contains(fanart.MovieId))
                        RepoFactory.AniDB_Anime_DefaultImage.Delete(defaultImage);
                    break;
                }
                case ImageEntityType.MovieDB_Poster:
                {
                    var poster = RepoFactory.TMDB_Movie_Poster.GetByID(defaultImage.ImageParentID);
                    if (poster == null || tmdbShowIDSet.Contains(poster.MovieId))
                        RepoFactory.AniDB_Anime_DefaultImage.Delete(defaultImage);
                    break;
                }
            }
        }

        // Remove the cross-references.
        foreach (var xref in xrefs)
            ; // TODO: FIX THIS LINE.

        // Update the series stats.
        AniDB_Anime.UpdateStatsByAnimeID(anidbAnimeID);

        return tmdbShowIDSet.Count;
    }
    #endregion
}
