
namespace Shoko.Server.Models.TMDB;

public class TMDB_Network
{
    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_NetworkID { get; set; }

    /// <summary>
    /// TMDB Network ID.
    /// </summary>
    public int TmdbNetworkID { get; set; }

    /// <summary>
    /// Main name of the network on TMDB.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The country the network originates from.
    /// </summary>
    public string CountryOfOrigin { get; set; }
}
