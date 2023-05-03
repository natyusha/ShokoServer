
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models.Provider;

public interface IShowRoleMetadata : IRoleMetadata
{
    #region Identifiers

    string ShowId { get; }

    #endregion

    #region Links

    IShowMetadata Show { get; }

    #endregion
}

public interface IShowVoiceActorMetadata : IShowRoleMetadata
{
    /// <inheritdoc/>
    new string CharacterId { get; }

    /// <inheritdoc/>
    new ICharacterMetadata Character { get; }

    /// <inheritdoc/>
    new StaffRoleType Type => StaffRoleType.VoiceActor;
}
