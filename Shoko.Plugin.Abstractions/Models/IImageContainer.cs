using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.Models;

public interface IImageContainer
{
    IImageMetadata? PreferredImage { get; }

    IReadOnlyList<IImageMetadata> Images { get; }
}
