
namespace Shoko.Plugin.Abstractions.Enums;

public enum StaffRoleType
{
    /// <summary>
    /// We don't know why they're listed as staff, but they're listed.
    /// </summary>
    None = 0,

    /// <summary>
    /// Voice actor or voice actress.
    /// </summary>
    VoiceActor = 1,

    /// <summary>
    /// This can be anything involved in writing the show.
    /// </summary>
    Staff,

    /// <summary>
    /// The studio responsible for publishing the show.
    /// </summary>
    Studio,

    /// <summary>
    /// The main producer(s) for the show.
    /// </summary>
    Producer,

    /// <summary>
    /// Direction.
    /// </summary>
    Director,

    /// <summary>
    /// Series Composition.
    /// </summary>
    SeriesComposer,

    /// <summary>
    /// Character Design.
    /// </summary>
    CharacterDesign,

    /// <summary>
    /// Music composer.
    /// </summary>
    Music,

    /// <summary>
    /// Responsible for the creation of the source work this show is detrived from.
    /// </summary>
    SourceWork,
}
