using System;

#nullable enable
namespace Shoko.Models.Server.Image;

[Flags]
public enum ForeignEntityType {
    None = 0,
    Collection = 1,
    Movie = 2,
    Show = 4,
    Season = 8,
    Episode = 16,
    Company = 32,
    Studio = 64,
    Network = 128,
    Person = 256,
    Character = 512,
}
