using System;

namespace Shoko.Plugin.Abstractions.Models.Provider;

public interface IBaseMetadata : IMetadata<string>, IImageContainer, ITitleContainer, IOverviewContainer
{
    /// <summary>
    /// When the local metadata entity was created.
    /// </summary>
    DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the local metadata entity was last updated.
    /// </summary>
    DateTime LastUpdatedAt { get; }
}
