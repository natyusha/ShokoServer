using Shoko.Models.Enums;

#nullable enable
namespace Shoko.Server.Models.CrossReference;

public class CrossRef_AniDB_TMDB_Show
{
    #region Database Columns

    public int CrossRef_AniDB_TMDB_ShowID { get; set; }

    public int AnidbAnimeID { get; set; }

    public int TmdbShowID { get; set; }

    public string? TmdbSeasonID { get; set; }

    public CrossRefSource Source { get; set; }

    #endregion
    #region Constructors

    public CrossRef_AniDB_TMDB_Show() { }

    public CrossRef_AniDB_TMDB_Show(int anidbAnimeId, int tmdbShowId, string? tmdbSeasonId = null, CrossRefSource source = CrossRefSource.User)
    {
        AnidbAnimeID = anidbAnimeId;
        TmdbShowID = tmdbShowId;
        TmdbSeasonID = tmdbSeasonId;
        Source = source;
    }

    #endregion
}
