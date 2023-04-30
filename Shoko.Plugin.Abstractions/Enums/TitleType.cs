using System.Xml.Serialization;

#nullable enable
namespace Shoko.Plugin.Abstractions.Enums;

public enum TitleType
{
    [XmlEnum("none")]
    None = 0,
    [XmlEnum("main")]
    Main = 1,
    [XmlEnum("official")]
    Official = 2,
    [XmlEnum("short")]
    Short = 3,
    [XmlEnum("syn")]
    Synonym = 4,
    [XmlEnum("card")]
    TitleCard = 5,
    [XmlEnum("kana")]
    KanjiReading = 6,
}
