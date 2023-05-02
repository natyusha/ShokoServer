using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels;

public interface IImageContainer
{
    IImageMetadata DefaultImage { get; }

    IReadOnlyList<IImageMetadata> Images { get; }
}
