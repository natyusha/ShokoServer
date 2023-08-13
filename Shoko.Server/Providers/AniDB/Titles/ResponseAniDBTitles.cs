using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;

namespace Shoko.Server.Providers.AniDB.Titles;

[XmlRoot("animetitles")]
public class ResponseAniDBTitles
{
    [XmlElement("anime")] public List<Anime> Animes { get; set; }

    public class Anime
    {
        [XmlIgnore]
        public virtual string MainTitle =>
            (Titles?.FirstOrDefault(t => t.IsPreferred) ?? Titles?.FirstOrDefault())?.Value ?? "";

        [XmlAttribute(DataType = "int", AttributeName = "aid")]
        public int AnimeId { get; set; }

        [XmlElement("title")]
        public List<AnimeTitle> Titles { get; set; }

        public class AnimeTitle : ITitle
        {
            [XmlText]
            public string Value { get; set; }

            [XmlAttribute("type")]
            public TitleType Type { get; set; }

            [XmlIgnore]
            public TextLanguage Language { get; set; }

            [XmlIgnore]
            public bool IsPreferred =>
                Type == TitleType.Main;

            [XmlAttribute(DataType = "string", AttributeName = "xml:lang")]
            public string LanguageCode
            {
                get => Language.ToLanguageCode();
                set => Language = value.ToTextLanguage();
            }

            [XmlIgnore]
            DataSource IMetadata.DataSource =>
                DataSource.AniDB;
        }
    }
}
