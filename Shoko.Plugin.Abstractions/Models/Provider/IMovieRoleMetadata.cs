
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models.Provider;

public interface IMovieRoleMetadata : IRoleMetadata
{
    #region Identifiers

    string MovieId { get; }

    #endregion

    #region Links

    IMovieMetadata Movie { get; }

    #endregion
}

public interface IMovieVoiceActorMetadata : IMovieRoleMetadata
{
    /// <inheritdoc/>
    new string CharacterId { get; }

    /// <inheritdoc/>
    new ICharacterMetadata Character { get; }

    /// <inheritdoc/>
    new StaffRoleType Type => StaffRoleType.VoiceActor;
}
