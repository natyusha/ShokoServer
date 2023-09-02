
namespace Shoko.Server.Models.TMDB;

public class TMDB_Movie_Company
{
    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_Movie_CompanyID { get; set; }

    /// <summary>
    /// TMDB Movie ID.
    /// </summary>
    public int TmdbMovieID { get; set; }

    /// <summary>
    /// TMDB Company ID.
    /// </summary>
    public int TmdbCompanyID { get; set; }

    /// <summary>
    /// Ordering.
    /// </summary>
    public int Order { get; set; }
}
