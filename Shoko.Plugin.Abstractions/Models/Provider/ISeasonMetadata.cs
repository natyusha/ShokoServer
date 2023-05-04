using System;
using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.Models.Provider;

public interface ISeasonMetadata : IBaseMetadata
{
    #region Ids

    string ShowId { get; }

    #endregion
    
    #region Links

    IShowMetadata Show { get; }

    IReadOnlyList<IEpisodeMetadata> Episodes { get; }

    #endregion

    #region Metadata

    int Number { get; }

    /// <summary>
    /// When the metadata was last updated.
    /// </summary>
    DateTime LastUpdated { get; }

    #endregion
}
