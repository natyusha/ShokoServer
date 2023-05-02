using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.DataModels;

public interface ISeasonMetadata : IImageContainer, ITitleContainer, IOverviewContainer
{
    #region Ids

    string Id { get; }

    string ShowId { get; }

    #endregion
    
    #region Links

    IShowMetadata Show { get; }

    IReadOnlyList<IEpisodeMetadata> Episodes { get; }

    #endregion

    #region Metadata

    int Number { get; }

    #endregion

    DataSource Source { get; }
}
