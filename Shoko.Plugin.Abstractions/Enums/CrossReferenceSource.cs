
using System;

namespace Shoko.Plugin.Abstractions.Enums;

[Flags]
public enum CrossReferenceSource
{
    Unknown = 0,
    User = 1,
    Plugin = 2,
    Anidb = 4,
    Animeshon = 8,
}
