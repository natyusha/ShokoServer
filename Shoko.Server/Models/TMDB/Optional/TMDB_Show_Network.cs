
#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_Show_Network
{
    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_Show_NetworkID { get; set; }

    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Network ID.
    /// </summary>
    public int TmdbNetworkID { get; set; }

    /// <summary>
    /// Ordering.
    /// </summary>
    public int Ordering { get; set; }
}
