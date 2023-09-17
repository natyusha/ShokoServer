﻿using System.IO;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.ImageDownload;

namespace Shoko.Server.Extensions;

public static class ImageResolvers
{
    public static string GetFullImagePath(this TvDB_Episode episode)
    {
        if (string.IsNullOrEmpty(episode.Filename))
        {
            return string.Empty;
        }

        var fname = episode.Filename.Replace("/", $"{Path.DirectorySeparatorChar}");
        return Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
    }

    public static string GetFullImagePath(this TvDB_ImageFanart fanart)
    {
        if (string.IsNullOrEmpty(fanart.BannerPath))
        {
            return string.Empty;
        }

        var fname = fanart.BannerPath.Replace("/", $"{Path.DirectorySeparatorChar}");
        return Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
    }

    public static string GetFullImagePath(this TvDB_ImagePoster poster)
    {
        if (string.IsNullOrEmpty(poster.BannerPath))
        {
            return string.Empty;
        }

        var fname = poster.BannerPath.Replace("/", $"{Path.DirectorySeparatorChar}");
        return Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
    }

    public static string GetFullImagePath(this TvDB_ImageWideBanner banner)
    {
        if (string.IsNullOrEmpty(banner.BannerPath))
        {
            return string.Empty;
        }

        var fname = banner.BannerPath.Replace("/", $"{Path.DirectorySeparatorChar}");
        return Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
    }

    public static void Valid(this TvDB_ImageFanart fanart)
    {
        if (!File.Exists(fanart.GetFullImagePath()))
        {
            //clean leftovers
            if (File.Exists(fanart.GetFullImagePath()))
            {
                File.Delete(fanart.GetFullImagePath());
            }
        }
    }

    public static string GetPosterPath(this AniDB_Character character)
    {
        if (string.IsNullOrEmpty(character.PicName))
        {
            return string.Empty;
        }

        return Path.Combine(ImageUtils.GetAniDBCharacterImagePath(character.CharID), character.PicName);
    }

    public static string GetPosterPath(this AniDB_Seiyuu seiyuu)
    {
        if (string.IsNullOrEmpty(seiyuu.PicName))
        {
            return string.Empty;
        }

        return Path.Combine(ImageUtils.GetAniDBCreatorImagePath(seiyuu.SeiyuuID), seiyuu.PicName);
    }

    //The resources need to be moved
    public static string GetAnimeTypeDescription(this AniDB_Anime anidbanime)
    {
        switch (anidbanime.GetAnimeTypeEnum())
        {
            case AnimeType.Movie:
                return Resources.AnimeType_Movie;
            case AnimeType.Other:
                return Resources.AnimeType_Other;
            case AnimeType.OVA:
                return Resources.AnimeType_OVA;
            case AnimeType.TVSeries:
                return Resources.AnimeType_TVSeries;
            case AnimeType.TVSpecial:
                return Resources.AnimeType_TVSpecial;
            case AnimeType.Web:
                return Resources.AnimeType_Web;
            default:
                return Resources.AnimeType_Other;
        }
    }
}
