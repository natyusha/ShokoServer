
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models.Provider;

public interface IStaffMemberMetadata : IMetadata<string>, IImageContainer, ITitleContainer, IOverviewContainer
{
    #region Links

    IReadOnlyList<IRoleMetadata> Roles { get; }

    IReadOnlyList<IShowMetadata> Shows { get; }

    IReadOnlyList<IMovieMetadata> Movies { get; }

    #endregion

    #region Metadata

    StaffMemberType Type { get; }

    TextLanguage Language { get; }

    string LanguageCode { get; }

    #endregion
}
