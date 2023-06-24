
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Models.Provider;

public interface IRoleMetadata : IMetadata<string>
{
    #region Identifiers

    string BaseId { get; }

    string StaffMemberId { get; }

    string? CharacterId { get; }

    #endregion

    #region Links

    IBaseMetadata Base { get; }

    IStaffMemberMetadata StaffMember { get; }

    ICharacterMetadata? Character { get; }

    #endregion

    /// <summary>
    /// If the role is for a language that is not native to the show (e.g. a
    /// dub), then the language indicates which language the role was done by
    /// the staff member and character.
    /// </summary>
    TextLanguage? Language { get; }

    string? LanguageCode { get; }

    StaffRoleType Type { get; }

    /// <summary>
    /// The role details, if any. For example, if the role is as a voice actor,
    /// then the details entail the type of role the character is, e.g. "Main
    /// Character".
    /// </summary>
    string? Details { get; }
}

public interface IVoiceActorMetadata : IRoleMetadata
{
    /// <inheritdoc/>
    new string CharacterId { get; }

    /// <inheritdoc/>
    new ICharacterMetadata Character { get; }

    /// <inheritdoc/>
    new StaffRoleType Type => StaffRoleType.VoiceActor;
}
