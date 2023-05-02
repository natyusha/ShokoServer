
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Extensions;

public static class EnumExtensions
{
    public static AnimeType ToAnimeType(this string type)
    {
        if (string.IsNullOrEmpty(type))
            return AnimeType.None;

        return type.ToLowerInvariant() switch
        {
            "movie" => AnimeType.Movie,
            "ova" => AnimeType.OVA,
            "tv series" =>  AnimeType.TV,
            "tv special" => AnimeType.TVSpecial,
            "web" => AnimeType.Web,
            "other" => AnimeType.Other,
            _ => AnimeType.None,
        };
    }

    public static string ToRawString(this AnimeType type)
    {
        return type switch
        {
            AnimeType.Movie => "movie",
            AnimeType.OVA => "ova",
            AnimeType.TV => "tv series",
            AnimeType.TVSpecial => "tv special",
            AnimeType.Web => "web",
            AnimeType.Other => "other",
            _ => "none",
        };
    }

    public static FileSource ToFileSource(this string source)
    {
        if (string.IsNullOrEmpty(source))
            return FileSource.Unknown;

        return source.Replace("-", "").ToLowerInvariant() switch
        {
            "tv" => FileSource.TV,
            "dtv" => FileSource.TV,
            "hdtv" => FileSource.TV,
            "dvd" => FileSource.DVD,
            "hkdvd" => FileSource.DVD,
            "hddvd" => FileSource.DVD,
            "bluray" => FileSource.BluRay,
            "www" => FileSource.Web,
            "web" => FileSource.Web,
            "vhs" => FileSource.VHS,
            "vcd" => FileSource.VCD,
            "svcd" => FileSource.VCD,
            "ld" => FileSource.LaserDisc,
            "laserdisc" => FileSource.LaserDisc,
            "camcorder" => FileSource.Camera,
            _ => FileSource.Unknown,
        };
    }

    public static string ToRawString(this FileSource source)
    {
        return source switch
        {
            FileSource.TV => "tv",
            FileSource.DVD => "dvd",
            FileSource.BluRay => "blu-ray",
            FileSource.Web => "www",
            FileSource.VHS => "vhs",
            FileSource.VCD => "vcd",
            FileSource.LaserDisc => "ld",
            FileSource.Camera => "camcorder",
            _ => "",
        };
    }
}
