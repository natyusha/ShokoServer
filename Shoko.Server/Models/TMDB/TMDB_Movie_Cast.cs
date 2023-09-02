
namespace Shoko.Server.Models.TMDB;

public class TMDB_Movie_Cast
{
    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_Movie_CastID { get; set; }

    /// <summary>
    /// TMDB Movie ID for the movie this role belongs to.
    /// </summary>
    public int TmdbMovieID { get; set; }

    /// <summary>
    /// TMDB Person ID for the cast memeber.
    /// </summary>
    public int TmdbPersonID { get; set; }

    /// <summary>
    /// TMDB Credit ID for the acting job.
    /// </summary>
    public string TmdbCreditID { get; set; }

    /// <summary>
    /// Character name.
    /// </summary>
    public string CharacterName { get; set; }

    /// <summary>
    /// Indicates the role is not a recurring role within the season.
    /// </summary>
    public bool IsGuestRole { get; set; }

    /// <summary>
    /// Ordering.
    /// </summary>
    public int Order { get; set; }
}
