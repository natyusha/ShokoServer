using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;

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

    #endregion
}
