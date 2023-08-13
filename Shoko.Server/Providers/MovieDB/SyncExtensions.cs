using System.Threading.Tasks;
using TMDbLib.Client;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.TvShows;

namespace Shoko.Server.Providers.MovieDB;

public static class SyncExtensions
{
    public static SearchContainer<SearchMovie> SearchMovie(this TMDbClient client, string criteria)
    {
        return Task.Run(async () => await client.SearchMovieAsync(criteria)).Result;
    }

    public static Movie GetMovie(this TMDbClient client, int movieID, MovieMethods methods = MovieMethods.Undefined)
    {
        return Task.Run(async () => await client.GetMovieAsync(movieID, methods)).Result;
    }

    public static ImagesWithId GetMovieImages(this TMDbClient client, int movieID)
    {
        return Task.Run(async () => await client.GetMovieImagesAsync(movieID)).Result;
    }

    public static SearchContainer<SearchTv> SearchTvShow(this TMDbClient client, string criteria)
    {
        return Task.Run(async () => await client.SearchTvShowAsync(criteria)).Result;
    }

    public static TvShow GetTvShow(this TMDbClient client, int movieID, TvShowMethods method = TvShowMethods.Undefined)
    {
        return Task.Run(async () => await client.GetTvShowAsync(movieID, method)).Result;
    }

    public static ImagesWithId GetTvShowImages(this TMDbClient client, int showID)
    {
        return Task.Run(async () => await client.GetTvShowImagesAsync(showID)).Result;
    }
}
