using Shoko.Models.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.CrossReferences;

public class CR_AniDB_TMDB_Show
{
    #region Database Columns

    public int Id { get; set; }

    public int AnidbAnimeId { get; set; }

    public int TmdbShowId { get; set; }

    public string? TmdbSeasonId { get; set; }

    public MatchRating MatchRating { get; set; } = MatchRating.SarahJessicaParker;

    public CrossRefSource Source { get; set; } = CrossRefSource.Automatic;

    #endregion

    #region Helpers

    public AniDB_Anime? AniDB_Anime =>
        RepoFactory.AniDB_Anime.GetByAnidbAnimeId(AnidbAnimeId);

    public TMDB_Show? TMDB_Show =>
        RepoFactory.TMDB_Show.GetByShowId(TmdbShowId);

    public TMDB_Season? TMDB_Season =>
        RepoFactory.TMDB_Season.GetBySeasonId(TmdbSeasonId);

    #endregion
}
