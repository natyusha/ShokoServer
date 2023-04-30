using System;

namespace Shoko.Plugin.Abstractions.Enums;

/// <summary>
/// Available data sources to chose from.
/// </summary>
[Flags]
public enum DataSource
{
    /// <summary>
    /// No source.
    /// </summary>
    None = 0,

    /// <summary>
    /// AniDB.
    /// </summary>
    AniDB = 1,

    /// <summary>
    /// The Tv Database (TvDB).
    /// </summary>
    TvDB = 2,

    /// <summary>
    /// The Movie Database (TMDB).
    /// </summary>
    TMDB = 4,

    /// <summary>
    /// TraktTv.
    /// </summary>
    Trakt = 8,

    /// <summary>
    /// My Anime List (MAL).
    /// </summary>
    MAL = 16,

    /// <summary>
    /// AniList (AL).
    /// </summary>
    AniList = 32,

    /// <summary>
    /// Animeshon.
    /// </summary>
    Animeshon = 64,

    /// <summary>
    /// Kitsu.
    /// </summary>
    Kitsu = 128,

    /// <summary>
    /// Fanart.Tv.
    /// </summary>
    Fanart = 256,

    /// <summary>
    /// Shoko.
    /// </summary>
    Shoko = 1024,
}
