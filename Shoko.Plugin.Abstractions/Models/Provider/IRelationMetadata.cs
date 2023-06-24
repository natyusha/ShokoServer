using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models.Shoko;

namespace Shoko.Plugin.Abstractions.Models.Provider;

public interface IRelationMetadata : IMetadata<string>
{
    #region Identifiers

    string BaseId { get; }

    string RelatedId { get; }

    #endregion

    #region Metadata

    RelationType Type { get; }

    #endregion

    #region Links

    /// <summary>
    /// The base entity, if it is locally available.
    /// </summary>
    IBaseMetadata? Base { get; }

    /// <summary>
    /// The releated entity, if it is locally available.
    /// </summary>
    IBaseMetadata? Related { get; }

    #endregion
}
