
using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.Models.Provider;

public interface ICharacterMetadata : IMetadata<string>, IImageContainer, ITitleContainer, IOverviewContainer
{
    #region Links

    IReadOnlyList<IRoleMetadata> Roles { get; }

    IReadOnlyList<IShowMetadata> Shows { get; }

    IReadOnlyList<IMovieMetadata> Movies { get; }

    #endregion
}
