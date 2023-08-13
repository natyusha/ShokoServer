using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.API.Converters;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Models;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Internal;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

using AbstractDataSource = Shoko.Plugin.Abstractions.Enums.DataSource;
using AniDBEpisodeType = Shoko.Plugin.Abstractions.Enums.EpisodeType;
using AniDBAnimeType = Shoko.Plugin.Abstractions.Enums.AnimeType;
using RelationType = Shoko.Plugin.Abstractions.Enums.RelationType;
using DataSource = Shoko.Server.API.v3.Models.Common.DataSource;
using Shoko.Server.Providers.MovieDB;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Providers.TvDB;
using Shoko.Plugin.Abstractions.Models;
using ImageEntityType = Shoko.Models.Enums.ImageEntityType;
using Shoko.Plugin.Abstractions.Models.Shoko;

namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
/// Series object, stores all of the series info
/// </summary>
public class Series : BaseModel
{
    /// <summary>
    /// The relevant IDs for the series, Shoko Internal, AniDB, etc
    /// </summary>
    public SeriesIDs IDs { get; set; }

    /// <summary>
    /// The default or random pictures for a series. This allows the client to not need to get all images and pick one.
    /// There should always be a poster, but no promises on the rest.
    /// </summary>
    public Images Images { get; set; }

    /// <summary>
    /// the user's rating
    /// </summary>
    public Rating UserRating { get; set; }

    /// <summary>
    /// The inferred days of the week this series airs on.
    /// </summary>
    /// <remarks>
    /// Will only be set for series of type <see cref="SeriesType.TV"/> and
    /// <see cref="SeriesType.Web"/>.
    /// </remarks>
    /// <value>Each weekday</value>
    [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
    public List<DayOfWeek> AirsOn { get; set; }

    /// <summary>
    /// links to series pages on various sites
    /// </summary>
    public List<Resource> Links { get; set; }

    /// <summary>
    /// Sizes object, has totals
    /// </summary>
    public SeriesSizes Sizes { get; set; }

    /// <summary>
    /// The time when the series was created, during the process of the first file being added
    /// </summary>
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime Created { get; set; }

    /// <summary>
    /// The time when the series was last updated
    /// </summary>
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime Updated { get; set; }

    /// <summary>
    /// The <see cref="Series.AniDB"/>, if <see cref="DataSource.AniDB"/> is
    /// included in the data to add.
    /// </summary>
    [JsonProperty("AniDB", NullValueHandling = NullValueHandling.Ignore)]
    public AniDBWithDate _AniDB { get; set; }

    /// <summary>
    /// The <see cref="Series.TvDB"/> entries, if <see cref="DataSource.TvDB"/>
    /// is included in the data to add.
    /// </summary>
    [JsonProperty("TvDB", NullValueHandling = NullValueHandling.Ignore)]
    public IEnumerable<TvDB> _TvDB { get; set; }

    #region Constructors and Helper Methods

    public Series() { }

    public Series(HttpContext ctx, ShokoSeries ser, bool randomiseImages = false, HashSet<DataSource> includeDataFrom = null)
    {
        var uid = ctx.GetUser()?.Id ?? 0;
        var anime = ser.GetAnime();
        var animeType = (AniDBAnimeType)anime.AnimeType;

        AddBasicAniDBInfo(ctx, ser, anime);

        var ael = ser.GetAnimeEpisodes();
        var contract = ser.Contract;
        if (contract == null)
        {
            ser.UpdateContract();
        }

        IDs = new SeriesIDs(ser);
        Images = GetDefaultImages(ctx, ser, randomiseImages);
        AirsOn = animeType == AniDBAnimeType.TV || animeType == AniDBAnimeType.Web ? ser.GetAirsOnDaysOfWeek(ael) : new();

        Name = ser.GetSeriesName();
        Sizes = ModelHelper.GenerateSeriesSizes(ael, uid);
        Size = Sizes.Local.Credits + Sizes.Local.Episodes + Sizes.Local.Others + Sizes.Local.Parodies +
               Sizes.Local.Specials + Sizes.Local.Trailers;

        Created = ser.DateTimeCreated;
        Updated = ser.DateTimeUpdated;

        if (includeDataFrom?.Contains(DataSource.AniDB) ?? false)
            this._AniDB = new Series.AniDBWithDate(anime, ser);
        if (includeDataFrom?.Contains(DataSource.TvDB) ?? false)
            this._TvDB = GetTvDBInfo(ctx, ser);
    }

    private void AddBasicAniDBInfo(HttpContext ctx, ShokoSeries series, AniDB_Anime anime)
    {
        if (anime == null)
        {
            return;
        }

        Links = new();
        if (!string.IsNullOrEmpty(anime.Site_EN))
            foreach (var site in anime.Site_EN.Split('|'))
                Links.Add(new() { Type = "source", Name = "Official Site (EN)", URL = site });

        if (!string.IsNullOrEmpty(anime.Site_JP))
            foreach (var site in anime.Site_JP.Split('|'))
                Links.Add(new() { Type = "source", Name = "Official Site (JP)", URL = site });

        if (!string.IsNullOrEmpty(anime.Wikipedia_ID))
            Links.Add(new() { Type = "wiki", Name = "Wikipedia (EN)", URL = $"https://en.wikipedia.org/{anime.Wikipedia_ID}" });

        if (!string.IsNullOrEmpty(anime.WikipediaJP_ID))
            Links.Add(new() { Type = "wiki", Name = "Wikipedia (JP)", URL = $"https://en.wikipedia.org/{anime.WikipediaJP_ID}" });

        if (!string.IsNullOrEmpty(anime.CrunchyrollID))
            Links.Add(new() { Type = "streaming", Name = "Crunchyroll", URL = $"https://crunchyroll.com/anime/{anime.CrunchyrollID}" });

        if (!string.IsNullOrEmpty(anime.FunimationID))
            Links.Add(new() { Type = "streaming", Name = "Funimation", URL = anime.FunimationID });

        if (!string.IsNullOrEmpty(anime.HiDiveID))
            Links.Add(new() { Type = "streaming", Name = "HiDive", URL = $"https://www.hidive.com/{anime.HiDiveID}" });

        if (anime.AllCinemaID.HasValue && anime.AllCinemaID.Value > 0)
            Links.Add(new() { Type = "foreign-metadata", Name = "allcinema", URL = $"https://allcinema.net/cinema/{anime.AllCinemaID.Value}" });

        if (anime.AnisonID.HasValue && anime.AnisonID.Value > 0)
            Links.Add(new() { Type = "foreign-metadata", Name = "Anison", URL = $"https://anison.info/data/program/{anime.AnisonID.Value}.html" });

        if (anime.SyoboiID.HasValue && anime.SyoboiID.Value > 0)
            Links.Add(new() { Type = "foreign-metadata", Name = "syoboi", URL = $"https://cal.syoboi.jp/tid/{anime.SyoboiID.Value}/time" });

        if (anime.BangumiID.HasValue && anime.BangumiID.Value > 0)
            Links.Add(new() { Type = "foreign-metadata", Name = "bangumi", URL = $"https://bgm.tv/subject/{anime.BangumiID.Value}" });

        if (anime.LainID.HasValue && anime.LainID.Value > 0)
            Links.Add(new() { Type = "foreign-metadata", Name = ".lain", URL = $"http://lain.gr.jp/mediadb/media/{anime.LainID.Value}" });

        if (anime.ANNID.HasValue && anime.ANNID.Value > 0)
            Links.Add(new() { Type = "english-metadata", Name = "AnimeNewsNetwork", URL = $"https://www.animenewsnetwork.com/encyclopedia/anime.php?id={anime.ANNID.Value}" });

        if (anime.VNDBID.HasValue && anime.VNDBID.Value > 0)
            Links.Add(new() { Type = "english-metadata", Name = "VNDB", URL = $"https://vndb.org/v{anime.VNDBID.Value}" });

        var vote = RepoFactory.AniDB_Vote.GetByEntityAndType(anime.AnimeID, AniDBVoteType.Anime) ??
                   RepoFactory.AniDB_Vote.GetByEntityAndType(anime.AnimeID, AniDBVoteType.AnimeTemp);
        if (vote != null)
        {
            UserRating = new Rating(vote);
        }
    }

    public static bool QueueAniDBRefresh(ICommandRequestFactory commandFactory, IHttpConnectionHandler handler,
        int animeID, bool force, bool downloadRelations, bool createSeriesEntry, bool immediate = false,
        bool cacheOnly = false)
    {
        var command = commandFactory.Create<CommandRequest_GetAnimeHTTP>(c =>
        {
            c.AnimeID = animeID;
            c.DownloadRelations = downloadRelations;
            c.ForceRefresh = force;
            c.CacheOnly = !force && cacheOnly;
            c.CreateSeriesEntry = createSeriesEntry;
            c.BubbleExceptions = immediate;
        });
        if (immediate && (command.CacheOnly || !handler.IsBanned))
        {
            try
            {
                command.ProcessCommand();
            }
            catch
            {
                return false;
            }

            return command.Result != null;
        }

        command.Save();
        return false;
    }

    public static bool QueueTvDBRefresh(ICommandRequestFactory commandFactory, int tvdbID, bool force, bool immediate = false)
    {
        var command = commandFactory.Create<CommandRequest_TvDBUpdateSeries>(c =>
        {
            c.TvDBSeriesID = tvdbID;
            c.ForceRefresh = force;
            c.BubbleExceptions = immediate;
        });
        if (immediate)
        {
            try
            {
                command.ProcessCommand();
            }
            catch
            {
                return false;
            }

            return command.Result != null;
        }

        command.Save();
        return false;
    }

    public static Image GetDefaultImage(int anidbId, ImageSizeType imageSizeType,
        ImageEntityType? imageEntityType = null)
    {
        var defaultImage = imageEntityType.HasValue
            ? RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeTypeAndImageEntityType(anidbId,
                imageSizeType, imageEntityType.Value)
            : RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(anidbId, imageSizeType);
        return defaultImage != null
            ? new Image(defaultImage.ImageParentID, (ImageEntityType)defaultImage.ImageParentType, true)
            : null;
    }

    public static Images GetDefaultImages(HttpContext ctx, ShokoSeries ser, bool randomiseImages = false)
    {
        var images = new Images();
        var random = ctx.Items["Random"] as Random;
        var allImages = GetArt(ctx, ser.AniDB_ID);

        var poster = randomiseImages
            ? allImages.Posters.GetRandomElement(random)
            : GetDefaultImage(ser.AniDB_ID, ImageSizeType.Poster) ?? allImages.Posters.FirstOrDefault();
        if (poster != null)
        {
            images.Posters.Add(poster);
        }

        var fanart = randomiseImages
            ? allImages.Fanarts.GetRandomElement(random)
            : GetDefaultImage(ser.AniDB_ID, ImageSizeType.Fanart) ?? allImages.Fanarts.FirstOrDefault();
        if (fanart != null)
        {
            images.Fanarts.Add(fanart);
        }

        var banner = randomiseImages
            ? allImages.Banners.GetRandomElement(random)
            : GetDefaultImage(ser.AniDB_ID, ImageSizeType.WideBanner) ?? allImages.Banners.FirstOrDefault();
        if (banner != null)
        {
            images.Banners.Add(banner);
        }

        return images;
    }

    /// <summary>
    /// Cast is aggregated, and therefore not in each provider
    /// </summary>
    /// <param name="animeID"></param>
    /// <param name="roleTypes"></param>
    /// <returns></returns>
    public static List<Role> GetCast(int animeID, HashSet<Role.CreatorRoleType> roleTypes = null)
    {
        var roles = new List<Role>();
        var xrefAnimeStaff = RepoFactory.CR_ShokoSeries_ShokoStaff.GetByAnimeID(animeID);
        foreach (var xref in xrefAnimeStaff)
        {
            // Filter out any roles that are not of the desired type.
            if (roleTypes != null && !roleTypes.Contains((Role.CreatorRoleType)xref.RoleType))
                continue;

            var character = xref.RoleID.HasValue ? RepoFactory.Shoko_Character.GetByID(xref.RoleID.Value) : null;
            var staff = RepoFactory.Shoko_Staff.GetByID(xref.StaffID);
            if (staff == null)
                continue;

            var role = new Role
            {
                Character =
                    character != null
                        ? new Role.Person
                        {
                            Name = character.Name,
                            AlternateName = character.AlternateName,
                            Image = new Image(character.CharacterID, ImageEntityType.Character),
                            Description = character.Description
                        }
                        : null,
                Staff = new Role.Person
                {
                    Name = staff.Name,
                    AlternateName = staff.AlternateName,
                    Description = staff.Description,
                    Image = staff.ImagePath != null ? new Image(staff.StaffID, ImageEntityType.Staff) : null
                },
                RoleName = (Role.CreatorRoleType)xref.RoleType,
                RoleDetails = xref.Role
            };
            roles.Add(role);
        }

        return roles;
    }

    public static List<Tag> GetTags(AniDB_Anime anime, TagFilter.Filter filter,
        bool excludeDescriptions = false, bool orderByName = false, bool onlyVerified = true)
    {
        // Only get the user tags if we don't exclude it (false == false), or if we invert the logic and want to include it (true == true).
        IEnumerable<Tag> userTags = new List<Tag>();
        if (filter.HasFlag(TagFilter.Filter.User) == filter.HasFlag(TagFilter.Filter.Invert))
            userTags = RepoFactory.Custom_Tag.GetByAnimeID(anime.AnimeID)
                .Select(tag => new Tag(tag, excludeDescriptions));

        var selectedTags = anime.GetAniDBTags(onlyVerified)
            .DistinctBy(a => a.TagName)
            .ToList();
        var tagFilter = new TagFilter<AniDB_Tag>(name => RepoFactory.AniDB_Tag.GetByName(name).FirstOrDefault(), tag => tag.TagName,
            name => new AniDB_Tag { TagNameSource = name });
        var anidbTags = tagFilter
            .ProcessTags(filter, selectedTags)
            .Select(tag =>
            {
                var xref = RepoFactory.AniDB_Anime_Tag.GetByTagID(tag.TagID).FirstOrDefault(xref => xref.AnimeID == anime.AnimeID);
                return new Tag(tag, excludeDescriptions) { Weight = xref?.Weight ?? 0, IsLocalSpoiler = xref?.LocalSpoiler };
            });

        if (orderByName)
            return userTags.Concat(anidbTags)
                .OrderByDescending(tag => tag.Source)
                .ThenBy(tag => tag.Name)
                .ToList();

        return userTags.Concat(anidbTags)
            .OrderByDescending(tag => tag.Source)
            .ThenByDescending(tag => tag.Weight)
            .ThenBy(tag => tag.Name)
            .ToList();
    }

    public static SeriesType GetAniDBSeriesType(int? animeType)
    {
        return animeType.HasValue ? GetAniDBSeriesType((AniDBAnimeType)animeType.Value) : SeriesType.Unknown;
    }

    public static SeriesType GetAniDBSeriesType(AniDBAnimeType animeType)
    {
        switch (animeType)
        {
            default:
            case AniDBAnimeType.None:
                return SeriesType.Unknown;
            case AniDBAnimeType.TV:
                return SeriesType.TV;
            case AniDBAnimeType.Movie:
                return SeriesType.Movie;
            case AniDBAnimeType.OVA:
                return SeriesType.OVA;
            case AniDBAnimeType.TVSpecial:
                return SeriesType.TVSpecial;
            case AniDBAnimeType.Web:
                return SeriesType.Web;
            case AniDBAnimeType.Other:
                return SeriesType.Other;
        }
    }

    public static List<TvDB> GetTvDBInfo(HttpContext ctx, ShokoSeries ser)
    {
        var ael = ser.GetAnimeEpisodes(true);
        return RepoFactory.CR_AniDB_TvDB.GetByAnimeID(ser.AniDB_ID)
            .Select(xref => RepoFactory.TvDB_Show.GetByTvDBID(xref.TvDBID))
            .Select(tvdbSer => new TvDB(ctx, tvdbSer, ser, ael))
            .ToList();
    }

    public static void AddSeriesVote(ICommandRequestFactory commandFactory, ShokoSeries ser, int userID, Vote vote)
    {
        var voteType = (vote.Type?.ToLowerInvariant() ?? "") switch
        {
            "temporary" => (int)AniDBVoteType.AnimeTemp,
            "permanent" => (int)AniDBVoteType.Anime,
            _ => ser.GetAnime()?.GetFinishedAiring() ?? false ? (int)AniDBVoteType.Anime : (int)AniDBVoteType.AnimeTemp
        };

        var dbVote = RepoFactory.AniDB_Vote.GetByEntityAndType(ser.AniDB_ID, AniDBVoteType.AnimeTemp) ??
                     RepoFactory.AniDB_Vote.GetByEntityAndType(ser.AniDB_ID, AniDBVoteType.Anime);

        if (dbVote == null)
        {
            dbVote = new AniDB_Vote { EntityID = ser.AniDB_ID };
        }

        dbVote.VoteValue = (int)Math.Floor(vote.GetRating(1000));
        dbVote.VoteType = voteType;

        RepoFactory.AniDB_Vote.Save(dbVote);

        var cmdVote = commandFactory.Create<CommandRequest_VoteAnime>(
            c =>
            {
                c.AnimeID = ser.AniDB_ID;
                c.VoteType = voteType;
                c.VoteValue = vote.GetRating();
            }
        );
        cmdVote.Save();
    }

    public static Images GetArt(HttpContext ctx, int animeID, bool includeDisabled = false)
    {
        var images = new Images();
        AddAniDBPoster(ctx, images, animeID);
        AddTvDBImages(ctx, images, animeID, includeDisabled);
        // AddMovieDBImages(ctx, images, animeID, includeDisabled);
        return images;
    }

    private static void AddAniDBPoster(HttpContext ctx, Images images, int animeID)
    {
        images.Posters.Add(GetAniDBPoster(animeID));
    }

    public static Image GetAniDBPoster(int animeID)
    {
        var defaultImage = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(animeID,
            ImageSizeType.Poster);
        var preferred = defaultImage != null && defaultImage.ImageParentType == (int)ImageEntityType.AniDB_Cover;
        return new Image(animeID, ImageEntityType.AniDB_Cover, preferred);
    }

    private static void AddTvDBImages(HttpContext ctx, Images images, int animeID, bool includeDisabled = false)
    {
        var tvdbIDs = RepoFactory.CR_AniDB_TvDB.GetByAnimeID(animeID).ToList();

        var defaultFanart =
            RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeTypeAndImageEntityType(animeID,
                ImageSizeType.Fanart, ImageEntityType.TvDB_FanArt);
        var fanarts = tvdbIDs.SelectMany(a => RepoFactory.TvDB_Fanart.GetBySeriesID(a.TvDBID)).ToList();
        images.Fanarts.AddRange(fanarts.Where(a => includeDisabled || a.Enabled != 0).Select(a =>
        {
            var preferred = defaultFanart != null && defaultFanart.ImageParentID == a.TvDB_ImageFanartID;
            return new Image(a.TvDB_ImageFanartID, ImageEntityType.TvDB_FanArt, preferred, a.Enabled == 0);
        }));

        var defaultBanner =
            RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeTypeAndImageEntityType(animeID,
                ImageSizeType.WideBanner, ImageEntityType.TvDB_Banner);
        var banners = tvdbIDs.SelectMany(a => RepoFactory.TvDB_Banner.GetBySeriesID(a.TvDBID)).ToList();
        images.Banners.AddRange(banners.Where(a => includeDisabled || a.Enabled != 0).Select(a =>
        {
            var preferred = defaultBanner != null && defaultBanner.ImageParentID == a.TvDB_ImageWideBannerID;
            return new Image(a.TvDB_ImageWideBannerID, ImageEntityType.TvDB_Banner, preferred, a.Enabled == 0);
        }));

        var defaultPoster =
            RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeTypeAndImageEntityType(animeID,
                ImageSizeType.Poster, ImageEntityType.TvDB_Cover);
        var posters = tvdbIDs.SelectMany(a => RepoFactory.TvDB_Poster.GetBySeriesID(a.TvDBID)).ToList();
        images.Posters.AddRange(posters.Where(a => includeDisabled || a.Enabled != 0).Select(a =>
        {
            var preferred = defaultPoster != null && defaultPoster.ImageParentID == a.TvDB_ImagePosterID;
            return new Image(a.TvDB_ImagePosterID, ImageEntityType.TvDB_Cover, preferred, a.Enabled == 0);
        }));
    }

    private static void AddMovieDBImages(HttpContext ctx, Images images, int animeID, bool includeDisabled = false)
    {
        var tmdbMovieIDs = RepoFactory.CR_AniDB_Other.GetByAnimeIDAndType(animeID, CrossRefType.MovieDB);

        var defaultPoster =
            RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeTypeAndImageEntityType(animeID,
                ImageSizeType.Poster, ImageEntityType.MovieDB_Poster);
        var tmdbMoviePosters = tmdbMovieIDs.SelectMany(xref => RepoFactory.TMDB_Movie_Poster.GetByMovieID(int.Parse(xref.CrossRefID))).ToList();
        images.Posters.AddRange(tmdbMoviePosters.Where(a => includeDisabled || a.Enabled != 0).Select(a =>
        {
            var preferred = defaultPoster != null && defaultPoster.ImageParentID == a.MovieDB_PosterID;
            return new Image(a.MovieDB_PosterID, ImageEntityType.MovieDB_Poster, preferred, a.Enabled == 1);
        }));

        var defaultFanart =
            RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeTypeAndImageEntityType(animeID,
                ImageSizeType.Fanart, ImageEntityType.MovieDB_FanArt);
        var tmdbMovieFanarts = tmdbMovieIDs.SelectMany(xref => RepoFactory.TMDB_Fanart.GetByMovieID(int.Parse(xref.CrossRefID))).ToList();
        images.Fanarts.AddRange(tmdbMovieFanarts.Where(a => includeDisabled || a.Enabled != 0).Select(a =>
        {
            var preferred = defaultFanart != null && defaultFanart.ImageParentID == a.MovieDB_FanartID;
            return new Image(a.MovieDB_FanartID, ImageEntityType.MovieDB_FanArt, preferred, a.Enabled == 1);
        }));
    }

    #endregion

    /// <summary>
    /// Auto-matching settings for the series.
    /// </summary>
    public class AutoMatchSettings
    {
        public AutoMatchSettings()
        {
            TvDB = false;
            TMDB = false;
            Trakt = false;
            // MAL = false;
            // AniList = false;
            // Animeshon = false;
            // Kitsu = false;
        }

        public AutoMatchSettings(ShokoSeries series)
        {
            TvDB = !series.IsTvDBAutoMatchingDisabled;
            TMDB = !series.IsTMDBAutoMatchingDisabled;
            Trakt = !series.IsTraktAutoMatchingDisabled;
            // MAL = !series.IsMALAutoMatchingDisabled;
            // AniList = !series.IsAniListAutoMatchingDisabled;
            // Animeshon = !series.IsAnimeshonAutoMatchingDisabled;
            // Kitsu = !series.IsKitsuAutoMatchingDisabled;
        }

        public AutoMatchSettings MergeWithExisting(ShokoSeries series)
        {
            series.IsTvDBAutoMatchingDisabled = !TvDB;
            series.IsTMDBAutoMatchingDisabled = !TMDB;
            series.IsTraktAutoMatchingDisabled = !Trakt;
            // series.IsMALAutoMatchingDisabled = !MAL;
            // series.IsAniListAutoMatchingDisabled = !AniList;
            // series.IsAnimeshonAutoMatchingDisabled = !Animeshon;
            // series.IsKitsuAutoMatchingDisabled = !Kitsu;

            RepoFactory.Shoko_Series.Save(series, false, true, true);

            return new AutoMatchSettings(series);
        }

        /// <summary>
        /// Auto-match against TvDB.
        /// </summary>
        [Required]
        public bool TvDB { get; set; }

        /// <summary>
        /// Auto-match against The Movie Database (TMDB).
        /// </summary>
        [Required]
        public bool TMDB { get; set; }

        /// <summary>
        /// Auto-match against Trakt.
        /// </summary>
        [Required]
        public bool Trakt { get; set; }

        // /// <summary>
        // /// Auto-match against My Anime List (MAL).
        // /// </summary>
        // [Required]
        // public bool MAL { get; set; }

        // /// <summary>
        // /// Auto-match against AniList.
        // /// </summary>
        // [Required]
        // public bool AniList { get; set; }

        // /// <summary>
        // /// Auto-match against Animeshon.
        // /// </summary>
        // [Required]
        // public bool Animeshon { get; set; }

        // /// <summary>
        // /// Auto-match against Kitsu.
        // /// </summary>
        // [Required]
        // public bool Kitsu { get; set; }
    }

    /// <summary>
    /// Basic anidb data across all anidb types.
    /// </summary>
    public class AniDB
    {
        public AniDB() { }

        public AniDB(AniDB_Anime anime, bool includeTitles) : this(anime, null, includeTitles) { }

        public AniDB(AniDB_Anime anime, ShokoSeries series = null, bool includeTitles = true)
        {
            series ??= RepoFactory.Shoko_Series.GetByAnidbAnimeId(anime.AnimeID);
            ID = anime.AnimeID;
            ShokoID = series?.AnimeSeriesID;
            Type = GetAniDBSeriesType(anime.AnimeType);
            Title = series?.GetSeriesName() ?? anime.PreferredTitle;
            Titles = includeTitles
                ? anime.GetTitles().Select(title => new Title(title)).ToList()
                : null;
            Description = anime.Description;
            Restricted = anime.Restricted == 1;
            Poster = GetAniDBPoster(anime.AnimeID);
            EpisodeCount = anime.EpisodeCountNormal;
            Rating = new Rating(anime);
            UserApproval = null;
            Relation = null;
        }

        public AniDB(ResponseAniDBTitles.Anime result, bool includeTitles) : this(result, null, includeTitles) { }

        public AniDB(ResponseAniDBTitles.Anime result, ShokoSeries series = null, bool includeTitles = false)
        {
            if (series == null)
            {
                series = RepoFactory.Shoko_Series.GetByAnidbAnimeId(result.AnimeId);
            }

            var anime = series != null ? series.GetAnime() : RepoFactory.AniDB_Anime.GetByAnidbAnimeId(result.AnimeId);

            ID = result.AnimeId;
            ShokoID = series?.AnimeSeriesID;
            Type = GetAniDBSeriesType(anime?.AnimeType);
            Title = series?.GetSeriesName() ?? anime?.PreferredTitle ?? result.MainTitle;
            Titles = includeTitles
                ? result.Titles.Select(title => new Title(title)).ToList()
                : null;
            Description = anime?.Description;
            Restricted = anime is { Restricted: 1 };
            EpisodeCount = anime?.EpisodeCount;
            Poster = GetAniDBPoster(result.AnimeId);
        }

        public AniDB(AniDB_Anime_Relation relation, bool includeTitles) : this(relation, null, includeTitles) { }

        public AniDB(AniDB_Anime_Relation relation, ShokoSeries series = null, bool includeTitles = true)
        {
            series ??= RepoFactory.Shoko_Series.GetByAnidbAnimeId(relation.RelatedAnidbAnimeId);
            ID = relation.RelatedAnidbAnimeId;
            ShokoID = series?.AnimeSeriesID;
            SetTitles(relation, series, includeTitles);
            Poster = GetAniDBPoster(relation.RelatedAnidbAnimeId);
            Rating = null;
            UserApproval = null;
            Relation = relation.Type;
        }

        private void SetTitles(AniDB_Anime_Relation relation, ShokoSeries series, bool includeTitles)
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnidbAnimeId(relation.RelatedAnidbAnimeId);
            if (anime is not null)
            {
                Type = GetAniDBSeriesType(anime.AnimeType);
                Title = series?.GetSeriesName() ?? anime.PreferredTitle;
                Titles = includeTitles
                    ? anime.GetTitles().Select(title => new Title(title)).ToList()
                    : null;
                Description = anime.Description;
                Restricted = anime.Restricted == 1;
                EpisodeCount = anime.EpisodeCountNormal;
                return;
            }

            var result = Utils.AniDBTitleHelper.SearchAnimeID(relation.RelatedAnidbAnimeId);
            if (result != null)
            {
                Type = SeriesType.Unknown;
                Title = result.MainTitle;
                Titles = includeTitles
                    ? result.Titles.Select(title => new Title(title)).ToList()
                    : null;
                Description = null;
                // If the other anime is present we assume they're of the same kind. Be it restricted or unrestricted.
                anime = RepoFactory.AniDB_Anime.GetByAnidbAnimeId(relation.AnidbAnimeId);
                Restricted = anime is not null && anime.Restricted == 1;
                return;
            }

            Type = SeriesType.Unknown;
            Titles = includeTitles ? new List<Title>() : null;
            Restricted = false;
        }

        public AniDB(AniDB_Anime_Similar similar, bool includeTitles) : this(similar, null, includeTitles) { }

        public AniDB(AniDB_Anime_Similar similar, ShokoSeries series = null, bool includeTitles = true)
        {
            series ??= RepoFactory.Shoko_Series.GetByAnidbAnimeId(similar.SimilarAnimeID);
            ID = similar.SimilarAnimeID;
            ShokoID = series?.AnimeSeriesID;
            SetTitles(similar, series, includeTitles);
            Poster = GetAniDBPoster(similar.SimilarAnimeID);
            Rating = null;
            UserApproval = new Rating(similar);
            Relation = null;
            Restricted = false;
        }

        private void SetTitles(AniDB_Anime_Similar similar, ShokoSeries series, bool includeTitles)
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnidbAnimeId(similar.SimilarAnimeID);
            if (anime is not null)
            {
                Type = GetAniDBSeriesType(anime.AnimeType);
                Title = series?.GetSeriesName() ?? anime.PreferredTitle;
                Titles = includeTitles
                    ? anime.GetTitles().Select(title => new Title(title)).ToList()
                    : null;
                Description = anime.Description;
                Restricted = anime.Restricted == 1;
                return;
            }

            var result = Utils.AniDBTitleHelper.SearchAnimeID(similar.SimilarAnimeID);
            if (result != null)
            {
                Type = SeriesType.Unknown;
                Title = result.MainTitle;
                Titles = includeTitles
                    ? result.Titles.Select(title => new Title(title)).ToList()
                    : null;
                Description = null;
                // If the other anime is present we assume they're of the same kind. Be it restricted or unrestricted.
                anime = RepoFactory.AniDB_Anime.GetByAnidbAnimeId(similar.AnimeID);
                Restricted = anime is not null && anime.Restricted == 1;
                return;
            }

            Type = SeriesType.Unknown;
            Title = null;
            Titles = includeTitles ? new List<Title>() : null;
            Description = null;
            Restricted = false;
        }

        /// <summary>
        /// AniDB ID
        /// </summary>
        [Required]
        public int ID { get; set; }

        /// <summary>
        /// <see cref="Series"/> ID if the series is available locally.
        /// </summary>
        /// <value></value>
        public int? ShokoID { get; set; }

        /// <summary>
        /// Series type. Series, OVA, Movie, etc
        /// </summary>
        [Required]
        [JsonConverter(typeof(StringEnumConverter))]
        public SeriesType Type { get; set; }

        /// <summary>
        /// Main Title, usually matches x-jat
        /// </summary>
        [Required]
        public string Title { get; set; }

        /// <summary>
        /// There should always be at least one of these, the <see cref="Title"/>.
        /// </summary>
        [Required]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Title> Titles { get; set; }

        /// <summary>
        /// Description.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }

        /// <summary>
        /// Restricted content. Mainly porn.
        /// </summary>
        public bool Restricted { get; set; }

        /// <summary>
        /// The main or default poster.
        /// </summary>
        [Required]
        public Image Poster { get; set; }

        /// <summary>
        /// Number of <see cref="EpisodeType.Normal"/> episodes contained within the series if it's known.
        /// </summary>
        public int? EpisodeCount { get; set; }

        /// <summary>
        /// The average rating for the anime.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Rating Rating { get; set; }

        /// <summary>
        /// User approval rate for the similar submission. Only available for similar.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Rating UserApproval { get; set; }

        /// <summary>
        /// Relation type. Only available for relations.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public RelationType? Relation { get; set; }
    }

    /// <summary>
    /// The AniDB Data model for series
    /// </summary>
    public class AniDBWithDate : AniDB
    {
        public AniDBWithDate(AniDB_Anime anime, ShokoSeries series = null) : base(anime,
            series)
        {
            if (anime.AirDate.HasValue)
            {
                AirDate = anime.AirDate.Value;
            }

            if (anime.EndDate.HasValue)
            {
                EndDate = anime.EndDate.Value;
            }
        }

        /// <summary>
        /// Air date (2013-02-27, shut up avael). Anything without an air date is going to be missing a lot of info.
        /// </summary>
        [Required]
        [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
        public DateTime? AirDate { get; set; }

        /// <summary>
        /// End date, can be omitted. Omitted means that it's still airing (2013-02-27)
        /// </summary>
        [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
        public DateTime? EndDate { get; set; }
    }

    /// <summary>
    /// The result entries for the "Recommended For You" algorithm.
    /// </summary>
    public class AniDBRecommendedForYou
    {
        /// <summary>
        /// The recommended AniDB entry.
        /// </summary>
        public AniDBWithDate Anime;

        /// <summary>
        /// Number of similar anime that resulted in this recommendation.
        /// </summary>
        public int SimilarTo;
    }

    /// <summary>
    /// The TvDB Data model for series
    /// </summary>
    public class TvDB
    {
        public TvDB(HttpContext ctx, IShowMetadata tvdbSeries, IShokoSeries series,
            List<IShokoEpisode> episodeList = null)
        {
            if (episodeList == null)
            {
                episodeList = series.Episodes.ToList();
            }
            var rating = tvdbSeries.Rating;

            ID = int.Parse(tvdbSeries.Id);
            Description = tvdbSeries.PreferredOverview;
            Title = tvdbSeries.PreferredTitle;
            if (rating != null)
            {
                Rating = new Rating(rating);
            }

            var images = new Images();
            AddTvDBImages(ctx, images, series.AniDBId);
            Posters = images.Posters;
            Fanarts = images.Fanarts;
            Banners = images.Banners;

            // Aggregate stuff
            var firstEp = episodeList
                .Where(ep => ep.AniDB != null && ep.AniDB.Type == AniDBEpisodeType.Normal && ep.AniDB.Number == 1)
                .FirstOrDefault()?.Episodes
                .Where(ep => ep.DataSource == AbstractDataSource.TvDB)
                .FirstOrDefault();

            var lastEp = episodeList
                .Where(ep => ep.AniDB != null && ep.AniDB.Type == AniDBEpisodeType.Normal)
                .OrderBy(ep => ep.AniDB.Type)
                .ThenBy(ep => ep.AniDB.Number)
                .LastOrDefault()?.Episodes
                .Where(ep => ep.DataSource == AbstractDataSource.TvDB)
                .FirstOrDefault();

            Season = firstEp?.SeasonNumber;
            AirDate = firstEp?.AirDate;
            EndDate = lastEp?.AirDate;
        }

        /// <summary>
        /// TvDB ID
        /// </summary>
        [Required]
        public int ID { get; set; }

        /// <summary>
        /// Air date (2013-02-27, shut up avael)
        /// </summary>
        [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
        public DateTime? AirDate { get; set; }

        /// <summary>
        /// End date, can be null. Null means that it's still airing (2013-02-27)
        /// </summary>
        [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// TvDB only supports one title
        /// </summary>
        [Required]
        public string Title { get; set; }

        /// <summary>
        /// Description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// TvDB Season. This value is not guaranteed to be even kind of accurate
        /// TvDB matchings and links affect this. Null means no match. 0 means specials
        /// </summary>
        public int? Season { get; set; }

        /// <summary>
        /// Posters
        /// </summary>
        public List<Image> Posters { get; set; }

        /// <summary>
        /// Fanarts
        /// </summary>
        public List<Image> Fanarts { get; set; }

        /// <summary>
        /// Banners
        /// </summary>
        public List<Image> Banners { get; set; }

        /// <summary>
        /// The rating object
        /// </summary>
        public Rating Rating { get; set; }
    }

    /// <summary>
    /// A site link, as in hyperlink.
    /// </summary>
    public class Resource
    {
        /// <summary>
        /// Resource type.
        /// </summary>
        [Required]
        public string Type { get; set; }

        /// <summary>
        /// site name
        /// </summary>
        [Required]
        public string Name { get; set; }

        /// <summary>
        /// the url to the series page
        /// </summary>
        [Required]
        public string URL { get; set; }
    }

    #region Cross-references

    public static List<SeriesCrossReference> GetCrossReferences(ShokoSeries series)
    {
        var list = new List<SeriesCrossReference>();
        list.AddRange(RepoFactory.CR_AniDB_MAL.GetByAnimeID(series.AniDB_ID).Select(xref => new SeriesCrossReference(xref)));
        list.AddRange(RepoFactory.CR_AniDB_TvDB.GetV2LinksFromAnime(series.AniDB_ID).Select(xref => new SeriesCrossReference(xref)));
        list.AddRange(RepoFactory.CR_AniDB_Other.GetByAnimeIDAndType(series.AniDB_ID, CrossRefType.MovieDB).Select(xref => new SeriesCrossReference(xref)));
        list.AddRange(RepoFactory.CR_AniDB_Trakt.GetByAnimeID(series.AniDB_ID).Select(xref => new SeriesCrossReference(xref)));
        return list;
    }

    public static void AddOrReplaceCrossReferences(ShokoSeries series, SeriesCrossReference.Input.AddCrossReferencesBody body, ModelStateDictionary modelState)
    {
        body.IDs ??= new List<string>();
        switch (body.Source)
        {
            case DataSource.None:
                return;

            default:
                modelState.AddModelError(nameof(body.Source), $"Unsupported data-source \"{body.Source}\".");
                return;

            case DataSource.MAL:
                // We're not allowed to touch the MAL cross-reference, since they're provided by AniDB.
                modelState.AddModelError(nameof(body.Source), "Unable to add or replace MAL cross-references.");
                return;

            case DataSource.TvDB:
            {
                if (body.Type != null)
                {
                    modelState.AddModelError(nameof(body.Type), $"Unsupported type \"{body.Type}\" for data-source \"{body.Source}\"");
                    return;
                }

                if (!ValidateIDs(body, id => int.TryParse(id, out var intID), modelState))
                    return;

                var ids = body.IDs.Select(id => int.Parse(id)).ToList();
                AddOrReplaceTvDBCrossReferences(series, ids, body.Replace);
                return;
            }

            case DataSource.Trakt:
                switch (body.Type.ToLowerInvariant())
                {
                    default:
                        modelState.AddModelError(nameof(body.Type), $"Unsupported type \"{body.Type}\" for data-source \"{body.Source}\"");
                        return;

                    case "movie":
                        if (!ValidateIDs(body, id => true, modelState))
                            return;

                        AddOrReplaceTraktMovieCrossReferences(series, body.IDs, body.Replace);
                        return;

                    case "show":
                        if (!ValidateIDs(body, id => true, modelState))
                            return;

                        AddOrReplaceTraktShowCrossReferences(series, body.IDs, body.Replace);
                        return;
                }

            case DataSource.TMDB:
                switch (body.Type.ToLowerInvariant())
                {
                    default:
                        modelState.AddModelError(nameof(body.Type), $"Unsupported type \"{body.Type}\" for data-source \"{body.Source}\"");
                        return;

                    case "movie":
                    {
                        if (!ValidateIDs(body, id => int.TryParse(id, out var intID), modelState))
                            return;

                        var ids = body.IDs.Select(id => int.Parse(id)).ToList();
                        AddOrReplaceTMDBMovieCrossReferences(series, ids, body.Replace);
                        return;
                    }

                    case "show":
                    {
                        if (!ValidateIDs(body, id => int.TryParse(id, out var intID), modelState))
                            return;

                        var ids = body.IDs.Select(id => int.Parse(id)).ToList();
                        AddOrReplaceTMDBShowCrossReferences(series, ids, body.Replace);
                        return;
                    }
                }
        }
    }

    private static void AddOrReplaceTvDBCrossReferences(ShokoSeries series, IReadOnlyList<int> tvdbShowIDs, bool replaceExisting)
    {
        var tvdbHelper = Utils.ServiceContainer.GetService<TvDBApiHelper>();

        tvdbHelper.AddShowLinks(series.AniDB_ID, tvdbShowIDs, replaceExisting);
    }

    private static void AddOrReplaceTraktMovieCrossReferences(ShokoSeries series, IReadOnlyList<string> traktMovieIDs, bool replaceExisting)
    {
        var settings = Utils.SettingsProvider.GetSettings();
        if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken))
            return;

        var traktHelper = Utils.ServiceContainer.GetService<TraktTVHelper>();

        traktHelper.AddMovieLinks(series.AniDB_ID, traktMovieIDs, replaceExisting);
    }

    private static void AddOrReplaceTraktShowCrossReferences(ShokoSeries series, IReadOnlyList<string> traktShowIDs, bool replaceExisting)
    {
        var settings = Utils.SettingsProvider.GetSettings();
        if (!settings.TraktTv.Enabled || string.IsNullOrEmpty(settings.TraktTv.AuthToken))
            return;

        var traktHelper = Utils.ServiceContainer.GetService<TraktTVHelper>();

        traktHelper.AddShowLinks(series.AniDB_ID, traktShowIDs, replaceExisting);
    }

    private static void AddOrReplaceTMDBMovieCrossReferences(ShokoSeries series, IReadOnlyList<int> tmdbMovieIDs, bool replaceExisting)
    {
        var tmdbMovieHelper = Utils.ServiceContainer.GetService<MovieDBHelper>();

        tmdbMovieHelper.AddMovieLinks(series.AniDB_ID, tmdbMovieIDs, replaceExisting);
    }

    private static void AddOrReplaceTMDBShowCrossReferences(ShokoSeries series, IReadOnlyList<int> tmdbShowIDs, bool replaceExisting)
    {
        var tmdbHelper = Utils.ServiceContainer.GetService<MovieDBHelper>();

        tmdbHelper.AddShowLinks(series.AniDB_ID, tmdbShowIDs, replaceExisting);
    }

    public static void RemoveCrossReferences(ShokoSeries series, SeriesCrossReference.Input.RemoveCrossReferencesBody body, ModelStateDictionary modelState)
    {
        body.IDs ??= new List<string>();
        switch (body.Source)
        {
            case DataSource.None:
                return;

            default:
                modelState.AddModelError(nameof(body.Source), $"Unsupported data-source \"{body.Source}\".");
                return;

            case DataSource.MAL:
                // We're not allowed to touch the MAL cross-reference, since they're provided by AniDB.
                modelState.AddModelError(nameof(body.Source), "Unable to remove MAL cross-references.");
                return;

            case DataSource.TvDB:
            {
                if (body.Type != null)
                {
                    modelState.AddModelError(nameof(body.Type), $"Unsupported type \"{body.Type}\" for data-source \"{body.Source}\"");
                    return;
                }

                if (!ValidateIDs(body, id => int.TryParse(id, out var intID), modelState))
                    return;

                var ids = body.IDs.Select(id => int.Parse(id)).ToList();
                RemoveTvDBShowCrossReferences(series, ids);
                return;
            }

            case DataSource.Trakt:
                switch (body.Type.ToLowerInvariant())
                {
                    default:
                        if (!ValidateIDs(body, id => int.TryParse(id, out var intID), modelState))
                            return;
                        if (body.IDs.Count > 0)
                        {
                            modelState.AddModelError(nameof(body.IDs), "");
                            return;
                        }
                        RemoveTraktShowCrossReferences(series);
                        RemoveTraktMovieCrossReferences(series);
                        return;

                    case "movie":
                        if (!ValidateIDs(body, id => true, modelState))
                            return;

                        RemoveTraktMovieCrossReferences(series, body.IDs);
                        return;

                    case "show":
                        if (!ValidateIDs(body, id => true, modelState))
                            return;

                        RemoveTraktShowCrossReferences(series, body.IDs);
                        return;
                }

            case DataSource.TMDB:
                switch (body.Type.ToLowerInvariant())
                {
                    default:
                        if (!ValidateIDs(body, id => int.TryParse(id, out var intID), modelState))
                            return;
                        if (body.IDs.Count > 0)
                        {
                            modelState.AddModelError(nameof(body.IDs), "");
                            return;
                        }
                        RemoveTMDBMovieCrossReferences(series);
                        RemoveTMDBShowCrossReferences(series);
                        return;

                    case "movie":
                    {
                        if (!ValidateIDs(body, id => int.TryParse(id, out var intID), modelState))
                            return;

                        var ids = body.IDs.Select(id => int.Parse(id)).ToList();
                        RemoveTMDBMovieCrossReferences(series, ids);
                        return;
                    }

                    case "show":
                    {
                        if (!ValidateIDs(body, id => int.TryParse(id, out var intID), modelState))
                            return;

                        var ids = body.IDs.Select(id => int.Parse(id)).ToList();
                        RemoveTMDBShowCrossReferences(series, ids);
                        return;
                    }
                }
        }
    }

    private static void RemoveTvDBShowCrossReferences(ShokoSeries series, IReadOnlyList<int> tvdbShowIDs = null)
    {
        var tvdbHelper = Utils.ServiceContainer.GetService<TvDBApiHelper>();

        tvdbHelper.RemoveShowLinks(series.AniDB_ID, tvdbShowIDs);
    }

    private static void RemoveTraktMovieCrossReferences(ShokoSeries series, IReadOnlyList<string> traktMovieIDs = null)
    {
        var traktHelper = Utils.ServiceContainer.GetService<TraktTVHelper>();

        traktHelper.RemoveMovieLinks(series.AniDB_ID, traktMovieIDs);
    }

    private static void RemoveTraktShowCrossReferences(ShokoSeries series, IReadOnlyList<string> traktShowIDs = null)
    {
        var traktHelper = Utils.ServiceContainer.GetService<TraktTVHelper>();

        traktHelper.RemoveShowLinks(series.AniDB_ID, traktShowIDs);
    }

    private static void RemoveTMDBMovieCrossReferences(ShokoSeries series, IReadOnlyList<int> tmdbMovieIDs = null)
    {
        var tmdbHelper = Utils.ServiceContainer.GetService<MovieDBHelper>();

        tmdbHelper.RemoveMovieLinks(series.AniDB_ID, tmdbMovieIDs);
    }

    private static void RemoveTMDBShowCrossReferences(ShokoSeries series, IReadOnlyList<int> tmdbMovieIDs = null)
    {
        var tmdbHelper = Utils.ServiceContainer.GetService<MovieDBHelper>();

        tmdbHelper.RemoveShowLinks(series.AniDB_ID, tmdbMovieIDs);
    }

    private static bool ValidateIDs(SeriesCrossReference.Input.AddCrossReferencesBody body, Func<string, bool> validator, ModelStateDictionary modelState)
    {
        // We inverse the validator to get the _invalid_ ids.
        var invalidIDs = body.IDs.Where((id) => !validator(id)).ToList();
        if (invalidIDs.Count > 0)
        {
            modelState.AddModelError(nameof(body.Type), $"Invalid ids for the source with the given type; \"{string.Join("\", \"", invalidIDs)}\"");
            return false;
        }

        return true;
    }

    private static bool ValidateIDs(SeriesCrossReference.Input.RemoveCrossReferencesBody body, Func<string, bool> validator, ModelStateDictionary modelState)
    {
        // We inverse the validator to get the _invalid_ ids.
        var invalidIDs = body.IDs.Where((id) => !validator(id)).ToList();
        if (invalidIDs.Count > 0)
        {
            modelState.AddModelError(nameof(body.Type), $"Invalid ids for the source with the given type; \"{string.Join("\", \"", invalidIDs)}\"");
            return false;
        }

        return true;
    }

    #endregion
}

public class SeriesIDs : IDs
{
    #region Groups

    /// <summary>
    /// The ID of the direct parent group, if it has one.
    /// </summary>
    public int ParentGroup { get; set; }

    /// <summary>
    /// The ID of the top-level (ancestor) group this series belongs to.
    /// </summary>
    public int TopLevelGroup { get; set; }

    #endregion

    #region XRefs

    // These are useful for many things, but for clients, it is mostly auxiliary

    /// <summary>
    /// The AniDB ID
    /// </summary>
    [Required]
    public int AniDB { get; set; }

    /// <summary>
    /// The MyAnimeList IDs
    /// </summary>
    public List<int> MAL { get; set; } = new();

    /// <summary>
    /// The TvDB IDs
    /// </summary>
    public List<int> TvDB_Shows { get; set; } = new();

    // TODO Support for TvDB string IDs (like in the new URLs) one day maybe

    /// <summary>
    /// TMDB Show IDs
    /// </summary>
    public List<int> TMDB_Shows { get; set; } = new();

    /// <summary>
    /// TMDB Movie IDs
    /// </summary>
    public List<int> TMDB_Movies { get; set; } = new();

    /// <summary>
    /// The TraktTv IDs
    /// </summary>
    public List<string> Trakt_Shows { get; set; } = new();

    /// <summary>
    /// The TraktTv IDs
    /// </summary>
    public List<string> Trakt_Movies { get; set; } = new();

    #endregion

    public SeriesIDs()
    {
        ID = -1;
        ParentGroup = -1;
        TopLevelGroup = -1;
        AniDB = -1;
        MAL = new();
        TvDB_Shows = new();
        TMDB_Shows = new();
        TMDB_Movies = new();
        Trakt_Shows = new();
        Trakt_Movies = new();
    }

    public SeriesIDs(IShokoSeries series)
    {
        var allMovieIds = series.AllMovieIds;
        var allShowIds = series.AllShowIds;

        ID = series.Id;
        ParentGroup = series.ParentGroupId;
        TopLevelGroup = series.TopLevelGroupId;
        AniDB = series.AnidbAnimeId;
        MAL = allShowIds
            .Where(meta => meta.DataSource == AbstractDataSource.MAL)
            .Select(meta => int.Parse(meta.Id))
            .ToList();
        TvDB_Shows = allShowIds
            .Where(meta => meta.DataSource == AbstractDataSource.TvDB)
            .Select(meta => int.Parse(meta.Id))
            .ToList();
        TMDB_Shows = allShowIds
            .Where(meta => meta.DataSource == AbstractDataSource.TMDB)
            .Select(meta => int.Parse(meta.Id))
            .ToList();
        TMDB_Movies = allMovieIds
            .Where(meta => meta.DataSource == AbstractDataSource.TMDB)
            .Select(meta => int.Parse(meta.Id))
            .ToList();
        Trakt_Shows = allShowIds
            .Where(meta => meta.DataSource == AbstractDataSource.Trakt)
            .Select(meta => meta.Id)
            .ToList();
        Trakt_Movies = allMovieIds
            .Where(meta => meta.DataSource == AbstractDataSource.Trakt)
            .Select(meta => meta.Id)
            .ToList();
    }
}

/// <summary>
/// An Extended Series Model with Values for Search Results
/// </summary>
public class SeriesSearchResult : Series
{
    /// <summary>
    /// Indicates whether the search result is an exact match to the query.
    /// </summary>
    public bool ExactMatch { get; set; }

    /// <summary>
    /// Represents the position of the match within the sanitized string.
    /// This property is only applicable when ExactMatch is set to true.
    /// A lower value indicates a match that occurs earlier in the string.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Represents the similarity measure between the sanitized query and the sanitized matched result.
    /// This may be the sorensen-dice distance or the tag weight when comparing tags for a series.
    /// A lower value indicates a more similar match.
    /// </summary>
    public double Distance { get; set; }

    /// <summary>
    /// Represents the absolute difference in length between the sanitized query and the sanitized matched result.
    /// A lower value indicates a match with a more similar length to the query.
    /// </summary>
    public int LengthDifference { get; set; }

    /// <summary>
    /// Contains the original matched substring from the original string.
    /// </summary>
    public string Match { get; set; } = string.Empty;

    public SeriesSearchResult(HttpContext ctx, SeriesSearch.SearchResult<ShokoSeries> result) : base(ctx, result.Result)
    {
        ExactMatch = result.ExactMatch;
        Index = result.Index;
        Distance = result.Distance;
        LengthDifference = result.LengthDifference;
        Match = result.Match;
    }
}

public enum SeriesType
{
    /// <summary>
    /// The series type is unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A catch-all type for future extensions when a provider can't use a current episode type, but knows what the future type should be.
    /// </summary>
    Other = 1,

    /// <summary>
    /// Standard TV series.
    /// </summary>
    TV = 2,

    /// <summary>
    /// TV special.
    /// </summary>
    TVSpecial = 3,

    /// <summary>
    /// Web series.
    /// </summary>
    Web = 4,

    /// <summary>
    /// All movies, regardless of source (e.g. web or theater)
    /// </summary>
    Movie = 5,

    /// <summary>
    /// Original Video Animations, AKA standalone releases that don't air on TV or the web.
    /// </summary>
    OVA = 6
}

/// <summary>
/// Downloaded, Watched, Total, etc
/// </summary>
public class SeriesSizes
{
    public SeriesSizes() : base()
    {
        Missing = 0;
        Hidden = 0;
        FileSources = new FileSourceCounts();
        Total = new EpisodeTypeCounts();
        Local = new EpisodeTypeCounts();
        Watched = new EpisodeTypeCounts();
    }

    /// <summary>
    /// Count of missing episodes that are not hidden.
    /// </summary>
    public int Missing { get; set; }

    /// <summary>
    /// Count of hidden episodes, be it available or missing.
    /// </summary>
    public int Hidden { get; set; }

    /// <summary>
    /// Counts of each file source type available within the local colleciton
    /// </summary>
    [Required]
    public FileSourceCounts FileSources { get; set; }

    /// <summary>
    /// What is downloaded and available
    /// </summary>
    [Required]
    public EpisodeTypeCounts Local { get; set; }

    /// <summary>
    /// What is local and watched.
    /// </summary>
    public EpisodeTypeCounts Watched { get; set; }

    /// <summary>
    /// Total count of each type
    /// </summary>
    [Required]
    public EpisodeTypeCounts Total { get; set; }

    /// <summary>
    /// Lists the count of each type of episode.
    /// </summary>
    public class EpisodeTypeCounts
    {
        public int Unknown { get; set; }
        public int Episodes { get; set; }
        public int Specials { get; set; }
        public int Credits { get; set; }
        public int Trailers { get; set; }
        public int Parodies { get; set; }
        public int Others { get; set; }
    }

    public class FileSourceCounts
    {
        public int Unknown;
        public int Other;
        public int TV;
        public int DVD;
        public int BluRay;
        public int Web;
        public int VHS;
        public int VCD;
        public int LaserDisc;
        public int Camera;
    }
}
