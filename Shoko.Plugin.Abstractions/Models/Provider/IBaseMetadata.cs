using System;

namespace Shoko.Plugin.Abstractions.Models.Provider;

public interface IBaseMetadata : IMetadata<string>, IImageContainer, ITitleContainer, IOverviewContainer
{
    /// <summary>
    /// When the metadata was last updated.
    /// </summary>
    DateTime LastUpdatedAt { get; }
}
