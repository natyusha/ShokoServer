using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Models.Provider;

namespace Shoko.Server.Models.TMDB;

public class TMDB_Show : IShowMetadata
{
    /// <summary>
    /// Local id.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Remote TMDB Show Id.
    /// </summary>
    public string ShowId { get; set; }

    /// <summary>
    /// Main title.
    /// </summary>
    public string MainTitle { get; set; }

    /// <summary>
    /// Main overview.
    /// </summary>
    public string MainOverview { get; set; }

    public List<TMDB_ContentRating> ContentRating { get; set; }
}
