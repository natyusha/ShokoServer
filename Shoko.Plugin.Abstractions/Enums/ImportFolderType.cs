using System;

#nullable enable
namespace Shoko.Plugin.Abstractions.Enums;

/// <summary>
/// The rules that this Import Folder should adhere to. A folder that is both a Source and Destination cares not how files are moved in or out of it.
/// </summary>
[Flags]
public enum ImportFolderType
{
    Excluded = 0,
    Source = 1,
    Destination = 2,
    Both = Source | Destination,
}
