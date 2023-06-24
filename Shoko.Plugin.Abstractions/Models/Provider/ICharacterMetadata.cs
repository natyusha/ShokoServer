
using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.Models.Provider;

public interface ICharacterMetadata : IBaseMetadata<string>
{
    #region Links

    IReadOnlyList<IRoleMetadata> Roles { get; }

    IReadOnlyList<IShowMetadata> Shows { get; }

    IReadOnlyList<IMovieMetadata> Movies { get; }

    #endregion
}
