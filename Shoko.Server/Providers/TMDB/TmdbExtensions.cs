using System.Threading.Tasks;
using Shoko.Models.Client;
using TMDbLib.Client;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.Search;

namespace Shoko.Server.Providers.TMDB;

public static class TmdbExtensions
{
    public static CL_MovieDBMovieSearch_Response ToContract(this SearchMovie movie)
        => new()
        {
            MovieID = movie.Id,
            MovieName = movie.Title,
            OriginalName = movie.OriginalTitle,
            Overview = movie.Overview,
        };
}
