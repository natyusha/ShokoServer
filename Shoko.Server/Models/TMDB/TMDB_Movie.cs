using System;
using Shoko.Plugin.Abstractions.Models.Provider;

namespace Shoko.Server.Models.TMDB;

public class TMDB_Movie : IMovieMetadata
{
    public string Id { get; set; }

    public string MainTitle { get; set; }

    public string MainOverview { get; set; }
}
