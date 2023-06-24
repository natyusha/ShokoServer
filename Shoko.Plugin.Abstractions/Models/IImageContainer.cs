using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Models.Search;

namespace Shoko.Plugin.Abstractions.Models;

public interface IImageContainer
{
    IImageMetadata? PreferredImage { get; }

    IReadOnlyList<IImageMetadata> AllImages { get; }

    IReadOnlyList<IImageMetadata> GetImages(ImageMetadataSearchOptions? options = null);
}
