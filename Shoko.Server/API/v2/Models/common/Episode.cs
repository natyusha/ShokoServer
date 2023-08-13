using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Http;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Models.Internal;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v2.Models.common;

[DataContract]
public class Episode : BaseDirectory
{
    public override string type => string.Intern("ep");

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public string season { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public int view { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public DateTime? view_date { get; set; }

    [DataMember] public string eptype { get; set; }

    [DataMember] public int epnumber { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public int aid { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public int eid { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public List<RawFile> files { get; set; }

    internal static Episode GenerateFromAnimeEpisodeID(HttpContext ctx, int anime_episode_id, int uid, int level,
        int pic = 1)
    {
        var ep = new Episode();

        if (anime_episode_id > 0)
        {
            ep = GenerateFromAnimeEpisode(ctx, RepoFactory.Shoko_Episode.GetByID(anime_episode_id), uid,
                level, pic);
        }

        return ep;
    }

    internal static Episode GenerateFromAnimeEpisode(HttpContext ctx, Shoko_Episode shokoEpisode, int uid, int level,
        int pic = 1)
    {
        var episode = new Episode { id = shokoEpisode.Id, art = new ArtCollection() };

        var anidbEpisode = shokoEpisode.AniDB;
        if (anidbEpisode != null)
        {
            episode.eptype = anidbEpisode.Type.ToString();
            episode.aid = anidbEpisode.AnimeId;
            episode.eid = anidbEpisode.EpisodeId;
        }

        var userrating = shokoEpisode.UserRating;
        if (userrating != null)
        {
            episode.userrating = userrating.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (double.TryParse(episode.rating, out var rating))
        {
            // 0.1 should be the absolute lowest rating
            if (rating > 10)
            {
                episode.rating = (rating / 100).ToString(CultureInfo.InvariantCulture);
            }
        }

        var cae = shokoEpisode.GetUserContract(uid);
        if (cae != null)
        {
            episode.name = cae.AniDB_EnglishName;
            episode.summary = cae.Description;

            episode.year = cae.AniDB_AirDate?.Year.ToString(CultureInfo.InvariantCulture);
            episode.air = cae.AniDB_AirDate?.ToPlexDate();

            episode.votes = cae.AniDB_Votes;
            episode.rating = cae.AniDB_Rating;

            episode.view = cae.WatchedDate != null ? 1 : 0;
            episode.view_date = cae.WatchedDate;
            episode.epnumber = cae.EpisodeNumber;
        }

        var tvep = shokoEpisode.TvDBEpisode;

        if (tvep != null)
        {
            if (!string.IsNullOrEmpty(tvep.EpisodeName))
            {
                episode.name = tvep.EpisodeName;
            }

            if (pic > 0)
            {
                if (Misc.IsImageValid(tvep.GetFullImagePath()))
                {
                    episode.art.thumb.Add(new Art
                    {
                        index = 0,
                        url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, (int)ImageEntityType.TvDB_Episode,
                            tvep.Id)
                    });
                }

                var fanarts = shokoEpisode.GetAnimeSeries()?.GetAnime()?.Contract?.AniDBAnime?.Fanarts;
                if (fanarts != null && fanarts.Count > 0)
                {
                    var cont_image =
                        fanarts[new Random().Next(fanarts.Count)];
                    episode.art.fanart.Add(new Art
                    {
                        url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, cont_image.ImageType,
                            cont_image.AniDB_Anime_DefaultImageID),
                        index = 0
                    });
                }
                else
                {
                    episode.art.fanart.Add(new Art
                    {
                        index = 0,
                        url = APIHelper.ConstructImageLinkFromTypeAndId(ctx, (int)ImageEntityType.TvDB_Episode,
                            tvep.Id)
                    });
                }
            }

            if (!string.IsNullOrEmpty(tvep.Overview))
            {
                episode.summary = tvep.Overview;
            }

            var zeroPadding = tvep.EpisodeNumber.ToString().Length;
            var episodeNumber = tvep.EpisodeNumber.ToString().PadLeft(zeroPadding, '0');
            zeroPadding = tvep.SeasonNumber.ToString().Length;
            var seasonNumber = tvep.SeasonNumber.ToString().PadLeft(zeroPadding, '0');

            episode.season = $"{seasonNumber}x{episodeNumber}";
            var airdate = tvep.AirDate;
            if (airdate != null)
            {
                episode.air = airdate.Value.ToPlexDate();
                episode.year = airdate.Value.Year.ToString(CultureInfo.InvariantCulture);
            }
        }

        if (string.IsNullOrEmpty(episode.summary))
        {
            episode.summary = string.Intern("Episode Overview not Available");
        }

        if (pic > 0 && episode.art.thumb.Count == 0)
        {
            episode.art.thumb.Add(
                new Art { index = 0, url = APIV2Helper.ConstructSupportImageLink(ctx, "plex_404.png") });
            episode.art.fanart.Add(new Art { index = 0, url = APIV2Helper.ConstructSupportImageLink(ctx, "plex_404.png") });
        }

        if (string.IsNullOrEmpty(episode.year))
        {
            episode.year = shokoEpisode.GetAnimeSeries().AirDate?.Year.ToString(CultureInfo.InvariantCulture) ?? "1";
        }

        if (level > 0)
        {
            var vls = shokoEpisode.GetVideoLocals();
            if (vls.Count > 0)
            {
                episode.files = new List<RawFile>();
                foreach (var vl in vls)
                {
                    var file = new RawFile(ctx, vl, level - 1, uid, shokoEpisode);
                    episode.files.Add(file);
                }
            }
        }

        return episode;
    }
}
