namespace Shoko.Server.Settings;

public class TMDBSettings
{
    /// <summary>
    /// Automagically link AniDB Series to TMDB Shows and Movies.
    /// </summary>
    public bool AutoLink { get; set; } = false;

    /// <summary>
    /// Automagically download episode groups for tv shows.
    /// </summary>
    public bool AutoDownloadEpisodeGroups { get; set; } = false;

    /// <summary>
    /// Automagically download backdrops for TMDB entities that supports
    /// backdrops up to <seealso cref="MaxAutoBackdrops"/> images per entity.
    /// </summary>
    public bool AutoDownloadBackdrops { get; set; } = true;

    /// <summary>
    /// The maximum number of backdrops to download for each TMDB entity that
    /// supports backdrops.
    /// </summary>
    public int MaxAutoBackdrops { get; set; } = 10;

    /// <summary>
    /// Automagically download posters for TMDB entities that supports
    /// posters up to <seealso cref="MaxAutoPosters"/> images per entity.
    /// </summary>
    public bool AutoDownloadPosters { get; set; } = true;

    /// <summary>
    /// The maximum number of posters to download for each TMDB entity that
    /// supports posters.
    /// </summary>
    public int MaxAutoPosters { get; set; } = 10;

    /// <summary>
    /// Automagically download logos for TMDB entities that supports
    /// logos up to <seealso cref="MaxAutoLogos"/> images per entity.
    /// </summary>
    public bool AutoDownloadLogos { get; set; } = true;

    /// <summary>
    /// The maximum number of logos to download for each TMDB entity that
    /// supports logos.
    /// </summary>
    public int MaxAutoLogos { get; set; } = 10;

    /// <summary>
    /// Automagically download thumbnails for TMDB entities that supports
    /// thumbnails.
    /// </summary>
    public bool AutoDownloadThumbnails { get; set; } = true;
}
