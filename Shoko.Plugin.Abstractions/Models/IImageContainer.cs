using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.Models;

public interface IImageContainer
{
    IImageMetadata DefaultImage { get; }

    IReadOnlyList<IImageMetadata> Images { get; }
}
