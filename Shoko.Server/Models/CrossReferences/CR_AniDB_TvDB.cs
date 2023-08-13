using Shoko.Models.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.TvDB;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.CrossReferences;

public class CR_AniDB_TvDB
{
    #region Database Columns

    public int Id { get; set; }

    public int AnidbAnimeId { get; set; }

    public int TvdbShowId { get; set; }

    public MatchRating MatchRating { get; set; } = MatchRating.SarahJessicaParker;

    public CrossRefSource Source { get; set; } = CrossRefSource.Automatic;

    #endregion

    #region Helpers

    public AniDB_Anime? AniDBAnime =>
        RepoFactory.AniDB_Anime.GetByAnidbAnimeId(AnidbAnimeId);

    public TvDB_Show? TvDBShow =>
        RepoFactory.TvDB_Show.GetByShowId(TvdbShowId);

    #endregion
}
