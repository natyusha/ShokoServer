
#nullable enable
using System.Collections.Generic;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.Models.TMDB;

public class TMDB_Network
{
    #region Properties

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
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The country the network originates from.
    /// </summary>
    public string CountryOfOrigin { get; set; } = string.Empty;

    #endregion

    #region Constructors

    #endregion

    #region Methods

    public IReadOnlyList<TMDB_Image> GetImages(ImageEntityType? entityType = null) => entityType.HasValue
        ? RepoFactory.TMDB_Image.GetByTmdbNetworkIDAndType(TmdbNetworkID, entityType.Value)
        : RepoFactory.TMDB_Image.GetByTmdbNetworkID(TmdbNetworkID);

    #endregion
}
