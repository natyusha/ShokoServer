using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.CrossReference;

public class CrossRef_AniDB_TMDB_Show
{
    #region Database Columns

    public int CrossRef_AniDB_TMDB_ShowID { get; set; }

    public int AnidbAnimeID { get; set; }

    public int TmdbShowID { get; set; }

    public int? TmdbSeasonID { get; set; }

    public CrossRefSource Source { get; set; }

    #endregion
    #region Constructors

    public CrossRef_AniDB_TMDB_Show() { }

    public CrossRef_AniDB_TMDB_Show(int anidbAnimeId, int tmdbShowId, int? tmdbSeasonId = null, CrossRefSource source = CrossRefSource.User)
    {
        AnidbAnimeID = anidbAnimeId;
        TmdbShowID = tmdbShowId;
        TmdbSeasonID = tmdbSeasonId;
        Source = source;
    }

    #endregion
    #region Methods

    public SVR_AniDB_Anime? GetAnidbAnime() =>
        RepoFactory.AniDB_Anime.GetByAnimeID(AnidbAnimeID);

    public SVR_AnimeSeries? GetShokoSeries() =>
        RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    public TMDB_Season? GetTmdbSeason() => TmdbSeasonID.HasValue
        ? RepoFactory.TMDB_Season.GetByTmdbSeasonID(TmdbSeasonID.Value)
        : null;

    public TMDB_Show? GetTmdbShow() =>
        RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID);

    #endregion
}
