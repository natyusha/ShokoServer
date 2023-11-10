using Shoko.Models.Enums;

#nullable enable
namespace Shoko.Server.Models.CrossReference;

public class CrossRef_AniDB_TMDB_Episode
{
    #region Database Columns

    public int CrossRef_AniDB_TMDB_EpisodeID { get; set; }

    public int AnidbAnimeID { get; set; }

    public int AnidbEpisodeID { get; set; }

    public int TmdbShowID { get; set; }

    public int TmdbEpisodeID { get; set; }

    public int Index { get; set; }

    public MatchRating MatchRating { get; set; }

    #endregion
    #region Constructors

    public CrossRef_AniDB_TMDB_Episode() { }

    public CrossRef_AniDB_TMDB_Episode(int anidbEpisodeId, int anidbAnimeId, int tmdbEpisodeId, int tmdbShowId, MatchRating rating = MatchRating.UserVerified, int index = 0)
    {
        AnidbEpisodeID = anidbEpisodeId;
        AnidbAnimeID = anidbAnimeId;
        TmdbEpisodeID = tmdbEpisodeId;
        TmdbShowID = tmdbShowId;
        Index = index;
        MatchRating = rating;
    }

    #endregion
}
