
using Shoko.Models.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.CrossReferences;

public class CR_AniDB_TMDB_Movie
{
    #region Database Columns

    public int Id { get; set; }

    public int AnidbAnimeId { get; set; }

    public int? AnidbEpisodeId { get; set; }

    public int TmdbMovieId { get; set; }

    public MatchRating MatchRating { get; set; } = MatchRating.SarahJessicaParker;

    public CrossRefSource Source { get; set; } = CrossRefSource.Automatic;

    #endregion

    #region Helpers

    public AniDB_Anime? AniDBAnime
        => RepoFactory.AniDB_Anime.GetByAnidbAnimeId(AnidbAnimeId);

    public AniDB_Episode? AniDBEpisode
        => AnidbEpisodeId.HasValue ? RepoFactory.AniDB_Episode.GetByAnidbEpisodeId(AnidbEpisodeId.Value) : null;

    public TMDB_Movie? TMDBMovie
        => RepoFactory.TMDB_Movie.GetByMovieId(TmdbMovieId);

    #endregion
}
