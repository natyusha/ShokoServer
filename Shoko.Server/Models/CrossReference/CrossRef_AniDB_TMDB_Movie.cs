using Shoko.Models.Enums;

namespace Shoko.Server.Models.CrossReference;

public class CrossRef_AniDB_TMDB_Movie
{
    #region Database Columns

    public int CrossRef_AniDB_TMDB_MovieID { get; set; }

    public int AnidbAnimeID { get; set; }

    public int? AnidbEpisodeID { get; set; }

    public int TmdbMovieID { get; set; }

    public MatchRating MatchRating { get; set; }

    public CrossRefSource Source { get; set; }

    #endregion
    #region Constructors

    public CrossRef_AniDB_TMDB_Movie() { }

    public CrossRef_AniDB_TMDB_Movie(int anidbAnimeId, int anidbEpisodeId, int tmdbMovieId, MatchRating rating = MatchRating.UserVerified, CrossRefSource source = CrossRefSource.User)
    {
        AnidbAnimeID = anidbAnimeId;
        AnidbEpisodeID = anidbEpisodeId;
        TmdbMovieID = tmdbMovieId;
        MatchRating = rating;
        Source = source;
    }

    #endregion
}
