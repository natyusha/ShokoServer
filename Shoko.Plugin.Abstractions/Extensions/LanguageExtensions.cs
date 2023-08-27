﻿using System;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions.Extensions;

public static class LanguageExtensions
{
    public static TitleLanguage GetTitleLanguage(this string lang)
    {
        return lang?.ToUpper() switch
        {
            "EN" or "ENG" => TitleLanguage.English,
            "X-JAT" => TitleLanguage.Romaji,
            "JA" or "JPN" => TitleLanguage.Japanese,
            "AR" or "ARA" => TitleLanguage.Arabic,
            "BD" or "BAN" => TitleLanguage.Bangladeshi,
            "BG" or "BUL" => TitleLanguage.Bulgarian,
            "CA" => TitleLanguage.FrenchCanadian,
            "CS" or "CES" or "CZ" => TitleLanguage.Czech,
            "DA" or "DAN" or "DK" => TitleLanguage.Danish,
            "DE" or "DEU" => TitleLanguage.German,
            "EL" or "ELL" or "GR" => TitleLanguage.Greek,
            "ES" or "SPA" => TitleLanguage.Spanish,
            "ET" or "EST" => TitleLanguage.Estonian,
            "FI" or "FIN" => TitleLanguage.Finnish,
            "FR" or "FRA" or "CA" => TitleLanguage.French,
            "GL" or "GLG" => TitleLanguage.Galician,
            "HE" or "HEB" or "IL" => TitleLanguage.Hebrew,
            "HU" or "HUN" => TitleLanguage.Hungarian,
            "IT" or "ITA" => TitleLanguage.Italian,
            "KO" or "KOR" => TitleLanguage.Korean,
            "X-KOT" => TitleLanguage.KoreanTranscription,
            "LT" or "LIT" => TitleLanguage.Lithuanian,
            "MN" or "MON" => TitleLanguage.Mongolian,
            "MS" or "MSA" or "MY" => TitleLanguage.Malaysian,
            "NL" or "NLD" => TitleLanguage.Dutch,
            "NO" or "NOR" => TitleLanguage.Norwegian,
            "PL" or "POL" => TitleLanguage.Polish,
            "PT" or "POR" => TitleLanguage.Portuguese,
            "PT-BR" => TitleLanguage.BrazilianPortuguese,
            "RO" or "RON" => TitleLanguage.Romanian,
            "RU" or "RUS" => TitleLanguage.Russian,
            "SK" or "SLK" => TitleLanguage.Slovak,
            "SL" or "SLV" => TitleLanguage.Slovenian,
            "SR" or "SRP" => TitleLanguage.Serbian,
            "SV" or "SWE" or "SE" => TitleLanguage.Swedish,
            "TH" or "THA" => TitleLanguage.Thai,
            "TR" or "TUR" => TitleLanguage.Turkish,
            "UK" or "UKR" or "UA" => TitleLanguage.Ukrainian,
            "VI" or "VIE" => TitleLanguage.Vietnamese,
            "ZH" or "ZHO" => TitleLanguage.Chinese,
            "X-ZHT" => TitleLanguage.Pinyin,
            "ZH-HANS" => TitleLanguage.ChineseSimplified,
            "ZH-HANT" => TitleLanguage.ChineseTraditional,
            "AF" or "AFR" => TitleLanguage.Afrikaans,
            "SQ" or "SQI" => TitleLanguage.Albanian,
            "AM" or "AMH" => TitleLanguage.Amharic,
            "HY" or "HYE" => TitleLanguage.Armenian,
            "AZ" or "AZE" => TitleLanguage.Azerbaijani,
            "EU" or "EUS" => TitleLanguage.Basque,
            "BE" or "BEL" => TitleLanguage.Belarusian,
            "BN" or "BEN" => TitleLanguage.Bengali,
            "BS" or "BOS" => TitleLanguage.Bosnian,
            "CA" or "CAT" => TitleLanguage.Catalan,
            "NY" or "NYA" => TitleLanguage.Chichewa,
            "CO" or "COS" => TitleLanguage.Corsican,
            "HR" or "HRV" => TitleLanguage.Croatian,
            "DV" or "DIV" => TitleLanguage.Divehi,
            "EO" or "EPO" => TitleLanguage.Esperanto,
            "TL" or "FIL" => TitleLanguage.Filipino,
            "FJ" or "FIJ" => TitleLanguage.Fijian,
            "KA" or "KAT" => TitleLanguage.Georgian,
            "GU" or "GUJ" => TitleLanguage.Gujarati,
            "HT" or "HAT" => TitleLanguage.HaitianCreole,
            "HA" or "HAU" => TitleLanguage.Hausa,
            "HI" or "HIN" => TitleLanguage.Hindi,
            "IS" or "ISL" => TitleLanguage.Icelandic,
            "IG" or "IBO" => TitleLanguage.Igbo,
            "ID" or "IND" => TitleLanguage.Indonesian,
            "GA" or "GLE" => TitleLanguage.Irish,
            "JV" or "JAV" => TitleLanguage.Javanese,
            "KN" or "KAN" => TitleLanguage.Kannada,
            "KK" or "KAZ" => TitleLanguage.Kazakh,
            "KM" or "KHM" => TitleLanguage.Khmer,
            "KU" or "KUR" => TitleLanguage.Kurdish,
            "KY" or "KIR" => TitleLanguage.Kyrgyz,
            "LO" or "LAO" => TitleLanguage.Lao,
            "LA" or "LAT" => TitleLanguage.Latin,
            "LV" or "LAV" => TitleLanguage.Latvian,
            "LB" or "LUX" => TitleLanguage.Luxembourgish,
            "MK" or "MKD" => TitleLanguage.Macedonian,
            "MG" or "MLG" => TitleLanguage.Malagasy,
            "ML" or "MAL" => TitleLanguage.Malayalam,
            "MT" or "MLT" => TitleLanguage.Maltese,
            "MI" or "MRI" => TitleLanguage.Maori,
            "MR" or "MAR" => TitleLanguage.Marathi,
            "MY" or "MYA" => TitleLanguage.MyanmarBurmese,
            "NE" or "NEP" => TitleLanguage.Nepali,
            "OR" or "ORI" => TitleLanguage.Oriya,
            "PS" or "PUS" => TitleLanguage.Pashto,
            "FA" or "FAS" => TitleLanguage.Persian,
            "PA" or "PAN" => TitleLanguage.Punjabi,
            "QU" or "QUE" => TitleLanguage.Quechua,
            "SM" or "SMO" => TitleLanguage.Samoan,
            "GD" or "GLA" => TitleLanguage.ScotsGaelic,
            "ST" or "SOT" => TitleLanguage.Sesotho,
            "SN" or "SNA" => TitleLanguage.Shona,
            "SD" or "SND" => TitleLanguage.Sindhi,
            "SI" or "SIN" => TitleLanguage.Sinhala,
            "SO" or "SOM" => TitleLanguage.Somali,
            "SW" or "SWA" => TitleLanguage.Swahili,
            "TG" or "TGK" => TitleLanguage.Tajik,
            "TA" or "TAM" => TitleLanguage.Tamil,
            "TT" or "TAT" => TitleLanguage.Tatar,
            "TE" or "TEL" => TitleLanguage.Telugu,
            "TK" or "TUK" => TitleLanguage.Turkmen,
            "UG" or "UIG" => TitleLanguage.Uighur,
            "UZ" or "UZB" => TitleLanguage.Uzbek,
            "CY" or "CYM" => TitleLanguage.Welsh,
            "XH" or "XHO" => TitleLanguage.Xhosa,
            "YI" or "YID" => TitleLanguage.Yiddish,
            "YO" or "YOR" => TitleLanguage.Yoruba,
            "ZU" or "ZUL" => TitleLanguage.Zulu,
            "UR" or "URD" => TitleLanguage.Urdu,
            "GREEK (ANCIENT)" => TitleLanguage.Greek,
            "JAVANESE" or "MALAY" or "INDONESIAN" => TitleLanguage.Malaysian,
            "PORTUGUESE (BRAZILIAN)" => TitleLanguage.BrazilianPortuguese,
            "THAI (TRANSCRIPTION)" => TitleLanguage.ThaiTranscription,
            "CHINESE (SIMPLIFIED)" => TitleLanguage.ChineseSimplified,
            "CHINESE (TRADITIONAL)" => TitleLanguage.ChineseTraditional,
            "CHINESE (CANTONESE)" or "CHINESE (MANDARIN)" or
            "CHINESE (UNSPECIFIED)" or "TAIWANESE" => TitleLanguage.Chinese,
            "CHINESE (TRANSCRIPTION)" => TitleLanguage.Pinyin,
            "JAPANESE (TRANSCRIPTION)" => TitleLanguage.Romaji,
            "CATALAN" or "SPANISH (LATIN AMERICAN)" => TitleLanguage.Spanish,
            "KOREAN (TRANSCRIPTION)" => TitleLanguage.KoreanTranscription,
            "FILIPINO (TAGALOG)" => TitleLanguage.Filipino,
            "" => TitleLanguage.None,
            null => TitleLanguage.None,
            _ => Enum.TryParse<TitleLanguage>(lang.ToLowerInvariant(), out var titleLanguage) ?
                titleLanguage : TitleLanguage.Unknown,
        };
    }

    public static string GetDescription(this TitleLanguage lang)
    {
        return lang switch
        {
            TitleLanguage.Romaji => "Japanese (romanji/x-jat)",
            TitleLanguage.Japanese => "Japanese (kanji)",
            TitleLanguage.Bangladeshi => "Bangladesh",
            TitleLanguage.FrenchCanadian => "Canadian-French",
            TitleLanguage.BrazilianPortuguese => "Brazilian Portuguese",
            TitleLanguage.Chinese => "Chinese (any)",
            TitleLanguage.ChineseSimplified => "Chinese (simplified)",
            TitleLanguage.ChineseTraditional => "Chinese (traditional)",
            TitleLanguage.Pinyin => "Chinese (pinyin/x-zhn)",
            TitleLanguage.KoreanTranscription => "Korean (x-kot)",
            TitleLanguage.ThaiTranscription => "Thai (x-tha)",
            _ => lang.ToString(),
        };
    }

    public static string GetString(this TitleLanguage lang)
    {
        return lang switch
        {
            TitleLanguage.None => "none",
            TitleLanguage.English => "en",
            TitleLanguage.Romaji => "x-jat",
            TitleLanguage.Japanese => "ja",
            TitleLanguage.Arabic => "ar",
            TitleLanguage.Bangladeshi => "bd",
            TitleLanguage.Bulgarian => "bg",
            TitleLanguage.FrenchCanadian => "ca",
            TitleLanguage.Czech => "cz",
            TitleLanguage.Danish => "da",
            TitleLanguage.German => "de",
            TitleLanguage.Greek => "gr",
            TitleLanguage.Spanish => "es",
            TitleLanguage.Estonian => "et",
            TitleLanguage.Finnish => "fi",
            TitleLanguage.French => "fr",
            TitleLanguage.Galician => "gl",
            TitleLanguage.Hebrew => "he",
            TitleLanguage.Hungarian => "hu",
            TitleLanguage.Italian => "it",
            TitleLanguage.Korean => "ko",
            TitleLanguage.KoreanTranscription => "x-kot",
            TitleLanguage.Lithuanian => "lt",
            TitleLanguage.Mongolian => "mn",
            TitleLanguage.Malaysian => "ms",
            TitleLanguage.Dutch => "ml",
            TitleLanguage.Norwegian => "no",
            TitleLanguage.Polish => "pl",
            TitleLanguage.Portuguese => "pt",
            TitleLanguage.BrazilianPortuguese => "pt-br",
            TitleLanguage.Romanian => "ro",
            TitleLanguage.Russian => "ru",
            TitleLanguage.Slovak => "sk",
            TitleLanguage.Slovenian => "sl",
            TitleLanguage.Serbian => "sr",
            TitleLanguage.Swedish => "sv",
            TitleLanguage.Thai => "th",
            TitleLanguage.ThaiTranscription => "x-tha",
            TitleLanguage.Turkish => "tr",
            TitleLanguage.Ukrainian => "uk",
            TitleLanguage.Vietnamese => "vi",
            TitleLanguage.Chinese => "zh",
            TitleLanguage.Pinyin => "x-zht",
            TitleLanguage.ChineseSimplified => "zh-hans",
            TitleLanguage.ChineseTraditional => "zh-hant",
            TitleLanguage.Afrikaans => "af",
            TitleLanguage.Albanian => "sq",
            TitleLanguage.Amharic => "am",
            TitleLanguage.Armenian => "hy",
            TitleLanguage.Azerbaijani => "az",
            TitleLanguage.Basque => "eu",
            TitleLanguage.Belarusian => "be",
            TitleLanguage.Bengali => "bn",
            TitleLanguage.Bosnian => "bs",
            TitleLanguage.Catalan => "ca",
            TitleLanguage.Chichewa => "ny",
            TitleLanguage.Corsican => "co",
            TitleLanguage.Croatian => "hr",
            TitleLanguage.Divehi => "dv",
            TitleLanguage.Esperanto => "eo",
            TitleLanguage.Filipino => "tl",
            TitleLanguage.Fijian => "fj",
            TitleLanguage.Georgian => "ka",
            TitleLanguage.Gujarati => "gu",
            TitleLanguage.HaitianCreole => "ht",
            TitleLanguage.Hausa => "ha",
            TitleLanguage.Hindi => "hi",
            TitleLanguage.Icelandic => "is",
            TitleLanguage.Igbo => "ig",
            TitleLanguage.Indonesian => "id",
            TitleLanguage.Irish => "ga",
            TitleLanguage.Javanese => "jv",
            TitleLanguage.Kannada => "kn",
            TitleLanguage.Kazakh => "kk",
            TitleLanguage.Khmer => "km",
            TitleLanguage.Kurdish => "ku",
            TitleLanguage.Kyrgyz => "ky",
            TitleLanguage.Lao => "lo",
            TitleLanguage.Latin => "la",
            TitleLanguage.Latvian => "lv",
            TitleLanguage.Luxembourgish => "lb",
            TitleLanguage.Macedonian => "mk",
            TitleLanguage.Malagasy => "mg",
            TitleLanguage.Malayalam => "ml",
            TitleLanguage.Maltese => "mt",
            TitleLanguage.Maori => "mi",
            TitleLanguage.Marathi => "mr",
            TitleLanguage.MyanmarBurmese => "my",
            TitleLanguage.Nepali => "ne",
            TitleLanguage.Oriya => "or",
            TitleLanguage.Pashto => "ps",
            TitleLanguage.Persian => "fa",
            TitleLanguage.Punjabi => "pa",
            TitleLanguage.Quechua => "qu",
            TitleLanguage.Samoan => "sm",
            TitleLanguage.ScotsGaelic => "gd",
            TitleLanguage.Sesotho => "st",
            TitleLanguage.Shona => "sn",
            TitleLanguage.Sindhi => "sd",
            TitleLanguage.Sinhala => "si",
            TitleLanguage.Somali => "so",
            TitleLanguage.Swahili => "sw",
            TitleLanguage.Tajik => "tg",
            TitleLanguage.Tamil => "ta",
            TitleLanguage.Tatar => "tt",
            TitleLanguage.Telugu => "te",
            TitleLanguage.Turkmen => "tk",
            TitleLanguage.Uighur => "ug",
            TitleLanguage.Uzbek => "uz",
            TitleLanguage.Urdu => "ur",
            TitleLanguage.Welsh => "cy",
            TitleLanguage.Xhosa => "xh",
            TitleLanguage.Yiddish => "yi",
            TitleLanguage.Yoruba => "yo",
            TitleLanguage.Zulu => "zu",
            _ => "unk",
        };
    }

    public static string GetString(this TitleType type)
    {
        return type.ToString().ToLowerInvariant();
    }

    public static TitleType GetTitleType(this string name)
    {
        foreach (var type in Enum.GetValues(typeof(TitleType)).Cast<TitleType>())
        {
            if (type.GetString().Equals(name.ToLowerInvariant())) return type;
        }

        return name.ToLowerInvariant() switch
        {
            "syn" => TitleType.Synonym,
            "card" => TitleType.TitleCard,
            "kana" => TitleType.KanjiReading,
            "kanareading" => TitleType.KanjiReading,
            _ => TitleType.None,
        };
    }

    /// <summary>
    /// Convert from an ISO3166 Alpha-2 or Alpha-3 country code to an ISO639-1
    /// language code.
    /// </summary>
    /// <remarks>
    /// This conversion list was compiled using
    /// https://github.com/annexare/Countries as a base, since it was the most
    /// complete library i could find that contained some kind of mapping
    /// between countries and languages, and with some minor modifications
    /// afterwards.
    /// </remarks>
    /// <param name="countryCode">Aplha-2 or Aplha-3 country code.</param>
    /// <returns></returns>
    public static string FromIso3166ToIso639(this string countryCode)
    {
        return countryCode?.ToUpper() switch
        {
            "AD" or "AND" => "CA",
            "AE" or "ARE" => "AR",
            "AF" or "AFG" => "PS",
            "AG" or "ATG" => "EN",
            "AI" or "AIA" => "EN",
            "AL" or "ALB" => "SQ",
            "AM" or "ARM" => "HY",
            "AO" or "AGO" => "PT",
            "AQ" or "ATA" => "EN",
            "AR" or "ARG" => "ES",
            "AS" or "ASM" => "EN",
            "AT" or "AUT" => "DE",
            "AU" or "AUS" => "EN",
            "AW" or "ABW" => "NL",
            "AX" or "ALA" => "SV",
            "AZ" or "AZE" => "AZ",
            "BA" or "BIH" => "BS",
            "BB" or "BRB" => "EN",
            "BD" or "BGD" => "BN",
            "BE" or "BEL" => "NL",
            "BF" or "BFA" => "FR",
            "BG" or "BGR" => "BG",
            "BH" or "BHR" => "AR",
            "BI" or "BDI" => "FR",
            "BJ" or "BEN" => "FR",
            "BL" or "BLM" => "FR",
            "BM" or "BMU" => "EN",
            "BN" or "BRN" => "MS",
            "BO" or "BOL" => "ES",
            "BQ" or "BES" => "NL",
            "BR" or "BRA" => "PT",
            "BS" or "BHS" => "EN",
            "BT" or "BTN" => "DZ",
            "BV" or "BVT" => "NO",
            "BW" or "BWA" => "EN",
            "BY" or "BLR" => "BE",
            "BZ" or "BLZ" => "EN",
            "CA" or "CAN" => "EN",
            "CC" or "CCK" => "EN",
            "CD" or "COD" => "FR",
            "CF" or "CAF" => "FR",
            "CG" or "COG" => "FR",
            "CH" or "CHE" => "DE",
            "CI" or "CIV" => "FR",
            "CK" or "COK" => "EN",
            "CL" or "CHL" => "ES",
            "CM" or "CMR" => "EN",
            "CN" or "CHN" => "ZH-HANS",
            "CO" or "COL" => "ES",
            "CR" or "CRI" => "ES",
            "CU" or "CUB" => "ES",
            "CV" or "CPV" => "PT",
            "CW" or "CUW" => "NL",
            "CX" or "CXR" => "EN",
            "CY" or "CYP" => "EL",
            "CZ" or "CZE" => "CS",
            "DE" or "DEU" => "DE",
            "DJ" or "DJI" => "FR",
            "DK" or "DNK" => "DA",
            "DM" or "DMA" => "EN",
            "DO" or "DOM" => "ES",
            "DZ" or "DZA" => "AR",
            "EC" or "ECU" => "ES",
            "EE" or "EST" => "ET",
            "EG" or "EGY" => "AR",
            "EH" or "ESH" => "ES",
            "ER" or "ERI" => "TI",
            "ES" or "ESP" => "ES",
            "ET" or "ETH" => "AM",
            "FI" or "FIN" => "FI",
            "FJ" or "FJI" => "EN",
            "FK" or "FLK" => "EN",
            "FM" or "FSM" => "EN",
            "FO" or "FRO" => "FO",
            "FR" or "FRA" => "FR",
            "GA" or "GAB" => "FR",
            "GB" or "GBR" => "EN",
            "GD" or "GRD" => "EN",
            "GE" or "GEO" => "KA",
            "GF" or "GUF" => "FR",
            "GG" or "GGY" => "EN",
            "GH" or "GHA" => "EN",
            "GI" or "GIB" => "EN",
            "GL" or "GRL" => "KL",
            "GM" or "GMB" => "EN",
            "GN" or "GIN" => "FR",
            "GP" or "GLP" => "FR",
            "GQ" or "GNQ" => "ES",
            "GR" or "GRC" => "EL",
            "GS" or "SGS" => "EN",
            "GT" or "GTM" => "ES",
            "GU" or "GUM" => "EN",
            "GW" or "GNB" => "PT",
            "GY" or "GUY" => "EN",
            "HK" or "HKG" => "ZH-HANT",
            "HM" or "HMD" => "EN",
            "HN" or "HND" => "ES",
            "HR" or "HRV" => "HR",
            "HT" or "HTI" => "FR",
            "HU" or "HUN" => "HU",
            "ID" or "IDN" => "ID",
            "IE" or "IRL" => "GA",
            "IL" or "ISR" => "HE",
            "IM" or "IMN" => "EN",
            "IN" or "IND" => "HI",
            "IO" or "IOT" => "EN",
            "IQ" or "IRQ" => "AR",
            "IR" or "IRN" => "FA",
            "IS" or "ISL" => "IS",
            "IT" or "ITA" => "IT",
            "JE" or "JEY" => "EN",
            "JM" or "JAM" => "EN",
            "JO" or "JOR" => "AR",
            "JP" or "JPN" => "JA",
            "KE" or "KEN" => "EN",
            "KG" or "KGZ" => "KY",
            "KH" or "KHM" => "KM",
            "KI" or "KIR" => "EN",
            "KM" or "COM" => "AR",
            "KN" or "KNA" => "EN",
            "KP" or "PRK" => "KO",
            "KR" or "KOR" => "KO",
            "KW" or "KWT" => "AR",
            "KY" or "CYM" => "EN",
            "KZ" or "KAZ" => "KK",
            "LA" or "LAO" => "LO",
            "LB" or "LBN" => "AR",
            "LC" or "LCA" => "EN",
            "LI" or "LIE" => "DE",
            "LK" or "LKA" => "SI",
            "LR" or "LBR" => "EN",
            "LS" or "LSO" => "EN",
            "LT" or "LTU" => "LT",
            "LU" or "LUX" => "FR",
            "LV" or "LVA" => "LV",
            "LY" or "LBY" => "AR",
            "MA" or "MAR" => "AR",
            "MC" or "MCO" => "FR",
            "MD" or "MDA" => "RO",
            "ME" or "MNE" => "SR",
            "MF" or "MAF" => "EN",
            "MG" or "MDG" => "FR",
            "MH" or "MHL" => "EN",
            "MK" or "MKD" => "MK",
            "ML" or "MLI" => "FR",
            "MM" or "MMR" => "MY",
            "MN" or "MNG" => "MN",
            "MO" or "MAC" => "ZH",
            "MP" or "MNP" => "EN",
            "MQ" or "MTQ" => "FR",
            "MR" or "MRT" => "AR",
            "MS" or "MSR" => "EN",
            "MT" or "MLT" => "MT",
            "MU" or "MUS" => "EN",
            "MV" or "MDV" => "DV",
            "MW" or "MWI" => "EN",
            "MX" or "MEX" => "ES",
            "MY" or "MYS" => "MS",
            "MZ" or "MOZ" => "PT",
            "NA" or "NAM" => "EN",
            "NC" or "NCL" => "FR",
            "NE" or "NER" => "FR",
            "NF" or "NFK" => "EN",
            "NG" or "NGA" => "EN",
            "NI" or "NIC" => "ES",
            "NL" or "NLD" => "NL",
            "NO" or "NOR" => "NO",
            "NP" or "NPL" => "NE",
            "NR" or "NRU" => "EN",
            "NU" or "NIU" => "EN",
            "NZ" or "NZL" => "EN",
            "OM" or "OMN" => "AR",
            "PA" or "PAN" => "ES",
            "PE" or "PER" => "ES",
            "PF" or "PYF" => "FR",
            "PG" or "PNG" => "EN",
            "PH" or "PHL" => "EN",
            "PK" or "PAK" => "EN",
            "PL" or "POL" => "PL",
            "PM" or "SPM" => "FR",
            "PN" or "PCN" => "EN",
            "PR" or "PRI" => "ES",
            "PS" or "PSE" => "AR",
            "PT" or "PRT" => "PT",
            "PW" or "PLW" => "EN",
            "PY" or "PRY" => "ES",
            "QA" or "QAT" => "AR",
            "RE" or "REU" => "FR",
            "RO" or "ROU" => "RO",
            "RS" or "SRB" => "SR",
            "RU" or "RUS" => "RU",
            "RW" or "RWA" => "RW",
            "SA" or "SAU" => "AR",
            "SB" or "SLB" => "EN",
            "SC" or "SYC" => "FR",
            "SD" or "SDN" => "AR",
            "SE" or "SWE" => "SV",
            "SG" or "SGP" => "EN",
            "SH" or "SHN" => "EN",
            "SI" or "SVN" => "SL",
            "SJ" or "SJM" => "NO",
            "SK" or "SVK" => "SK",
            "SL" or "SLE" => "EN",
            "SM" or "SMR" => "IT",
            "SN" or "SEN" => "FR",
            "SO" or "SOM" => "SO",
            "SR" or "SUR" => "NL",
            "SS" or "SSD" => "EN",
            "ST" or "STP" => "PT",
            "SV" or "SLV" => "ES",
            "SX" or "SXM" => "NL",
            "SY" or "SYR" => "AR",
            "SZ" or "SWZ" => "EN",
            "TC" or "TCA" => "EN",
            "TD" or "TCD" => "FR",
            "TF" or "ATF" => "FR",
            "TG" or "TGO" => "FR",
            "TH" or "THA" => "TH",
            "TJ" or "TJK" => "TG",
            "TK" or "TKL" => "EN",
            "TL" or "TLS" => "PT",
            "TM" or "TKM" => "TK",
            "TN" or "TUN" => "AR",
            "TO" or "TON" => "EN",
            "TR" or "TUR" => "TR",
            "TT" or "TTO" => "EN",
            "TV" or "TUV" => "EN",
            "TW" or "TWN" => "ZH-HANT",
            "TZ" or "TZA" => "SW",
            "UA" or "UKR" => "UK",
            "UG" or "UGA" => "EN",
            "UM" or "UMI" => "EN",
            "US" or "USA" => "EN",
            "UY" or "URY" => "ES",
            "UZ" or "UZB" => "UZ",
            "VA" or "VAT" => "IT",
            "VC" or "VCT" => "EN",
            "VE" or "VEN" => "ES",
            "VG" or "VGB" => "EN",
            "VI" or "VIR" => "EN",
            "VN" or "VNM" => "VI",
            "VU" or "VUT" => "BI",
            "WF" or "WLF" => "FR",
            "WS" or "WSM" => "SM",
            "XK" or "XKX" => "SQ",
            "YE" or "YEM" => "AR",
            "YT" or "MYT" => "FR",
            "ZA" or "ZAF" => "AF",
            "ZM" or "ZMB" => "EN",
            "ZW" or "ZWE" => "EN",
            _ => countryCode?.ToUpper(),
        };
    }
}
