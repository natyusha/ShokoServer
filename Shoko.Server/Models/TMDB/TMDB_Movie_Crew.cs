
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Crew member for a movie.
/// </summary>
public class TMDB_Movie_Crew
{
    /// <summary>
    ///  Local ID.
    /// </summary>
    public int TMDB_Movie_CrewID { get; set; }

    /// <summary>
    /// TMDB Movie ID for the movie this job belongs to.
    /// </summary>
    public int TmdbMovieID { get; set; }

    /// <summary>
    /// TMDB Show ID for the season this job belongs to.
    /// </summary>
    public int TmdbSeasonID { get; set; }

    /// <summary>
    /// TMDB Show ID for the show this job belongs to.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Person ID for the crew member.
    /// </summary>
    public int TmdbPersonID { get; set; }

    /// <summary>
    /// TMDB Credit ID for the production job.
    /// </summary>
    public string TmdbCreditID { get; set; }

    /// <summary>
    /// The job title.
    /// </summary>
    public string Job { get; set; }

    /// <summary>
    /// The crew department.
    /// </summary>
    public string Department { get; set; }
}
