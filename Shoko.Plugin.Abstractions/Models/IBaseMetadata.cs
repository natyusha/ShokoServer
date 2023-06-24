using System;

namespace Shoko.Plugin.Abstractions.Models;

public interface IBaseMetadata : IMetadata, IImageContainer, ITitleContainer, IOverviewContainer
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

public interface IBaseMetadata<TId> : IBaseMetadata, IMetadata<TId> { }
