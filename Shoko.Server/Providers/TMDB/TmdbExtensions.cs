using System;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Server;
using TMDbLib.Objects.Search;

#nullable enable
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

    public static DateOnly? GetAirDateAsDateOnly(this AniDB_Episode episode)
    {
        var dateTime = episode.GetAirDateAsDate();
        if (!dateTime.HasValue)
            return null;

        return DateOnly.FromDateTime(dateTime.Value);
    }

}
