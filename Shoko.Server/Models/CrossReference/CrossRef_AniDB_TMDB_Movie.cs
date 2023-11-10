using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.CrossReference;

public class CrossRef_AniDB_TMDB_Movie
{
    #region Database Columns

    public int CrossRef_AniDB_TMDB_MovieID { get; set; }

    public int AnidbAnimeID { get; set; }

    public int? AnidbEpisodeID { get; set; }

    public int TmdbMovieID { get; set; }

    public CrossRefSource Source { get; set; }

    #endregion
    #region Constructors

    public CrossRef_AniDB_TMDB_Movie() { }

    public CrossRef_AniDB_TMDB_Movie(int anidbAnimeId, int tmdbMovieId, MatchRating rating = MatchRating.UserVerified, CrossRefSource source = CrossRefSource.User)
    {
        AnidbAnimeID = anidbAnimeId;
        TmdbMovieID = tmdbMovieId;
        Source = source;
    }

    #endregion

    #region Methods

    public AniDB_Episode? GetAnidbEpisode() => AnidbEpisodeID.HasValue
        ? RepoFactory.AniDB_Episode.GetByEpisodeID(AnidbEpisodeID.Value)
        : null;

    public AniDB_Anime? GetAnidbAnime() =>
        RepoFactory.AniDB_Anime.GetByAnimeID(AnidbAnimeID);

    public SVR_AnimeEpisode? GetShokoEpisode() => AnidbEpisodeID.HasValue
        ? RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(AnidbEpisodeID.Value)
        : null;

    public SVR_AnimeSeries? GetShokoSeries() =>
        RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    public TMDB_Movie? GetTmdbMovie()
        => RepoFactory.TMDB_Movie.GetByTmdbMovieID(TmdbMovieID);

    #endregion
}
