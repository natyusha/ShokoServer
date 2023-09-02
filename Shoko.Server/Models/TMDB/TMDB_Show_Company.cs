
namespace Shoko.Server.Models.TMDB;

public class TMDB_Show_Company
{
    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_Show_CompanyID { get; set; }

    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Company ID.
    /// </summary>
    public int TmdbCompanyID { get; set; }

    /// <summary>
    /// Ordering.
    /// </summary>
    public int Order { get; set; }
}
