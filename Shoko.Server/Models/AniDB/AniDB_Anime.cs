using System;
using System.Collections.Generic;
using System.IO;
using NLog;
using Shoko.Models.Client;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.Provider;
using Shoko.Server.ImageDownload;
using Shoko.Server.LZ4;
using Shoko.Server.Models.Internal;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.AniDB;
public class AniDB_Anime : IShowMetadata
{
    #region Database Columns

    public int AniDB_AnimeID { get; set; }

    public int AnimeId { get; set; }

    public int EpisodeCount { get; set; }

    public DateTime? AirDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string URL { get; set; }

    public string Picname { get; set; }

    public int BeginYear
        => AirDate.HasValue ? AirDate.Value.Year : 0;

    public int EndYear
        => EndDate.HasValue ? EndDate.Value.Year : 0;

    public AnimeType AnimeType { get; set; }

    public string MainTitle { get; set; }

    public string AllTitles { get; set; }

    public string AllTags { get; set; }

    public string Description { get; set; }

    public int EpisodeCountNormal { get; set; }

    public int EpisodeCountSpecial { get; set; }

    public int Rating { get; set; }

    public int VoteCount { get; set; }

    public int TempRating { get; set; }

    public int TempVoteCount { get; set; }

    public int AvgReviewRating { get; set; }

    public int ReviewCount { get; set; }

    [Obsolete("Deprecated in favor of AniDB_AnimeUpdate. This is for when an AniDB_Anime fails to save")]
    public DateTime DateTimeUpdated { get; set; }

    public DateTime DateTimeDescUpdated { get; set; }

    public int ImageEnabled { get; set; }

    public int Restricted { get; set; }

    public int? ANNID { get; set; }

    public int? AllCinemaID { get; set; }

    public int? AnisonID { get; set; }

    public int? SyoboiID { get; set; }

    public int? VNDBID { get; set; }

    public int? BangumiID { get; set; }

    public int? LainID { get; set; }

    public string Site_JP { get; set; }

    public string Site_EN { get; set; }

    public string Wikipedia_ID { get; set; }

    public string WikipediaJP_ID { get; set; }

    public string CrunchyrollID { get; set; }

    public string FunimationID { get; set; }

    public string HiDiveID { get; set; }

    public int? LatestEpisodeNumber { get; set; }

    #region Contract

    public const int CONTRACT_VERSION = 7;

    public int ContractVersion { get; set; }

    public byte[] ContractBlob { get; set; }

    public int ContractSize { get; set; }

    #endregion

    #endregion
    
    #region Helpers

    public bool IsFinishedAiring
    {
        get
        {
            
            // ongoing or not started.
            if (!EndDate.HasValue) return false;

            // all episodes have finished airing.
            if (EndDate.Value < DateTime.Now) return true;

            return false;
        }
    }

    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private CL_AniDB_AnimeDetailed _contract;

    public virtual CL_AniDB_AnimeDetailed Contract
    {
        get
        {
            if (_contract == null && ContractBlob != null && ContractBlob.Length > 0 && ContractSize > 0)
            {
                _contract = CompressionHelper.DeserializeObject<CL_AniDB_AnimeDetailed>(ContractBlob,
                    ContractSize);
            }

            return _contract;
        }
        set
        {
            _contract = value;
            ContractBlob = CompressionHelper.SerializeObject(value, out var outsize);
            ContractSize = outsize;
            ContractVersion = CONTRACT_VERSION;
        }
    }

    public void CollectContractMemory()
    {
        _contract = null;
    }

    #endregion

    #region Properties and fields

    public string PosterPath
    {
        get
        {
            if (string.IsNullOrEmpty(Picname))
            {
                return string.Empty;
            }

            return Path.Combine(ImageUtils.GetAniDBImagePath(AnimeId), Picname);
        }
    }

    public List<TvDB_Episode> GetTvDBEpisodes()
    {
        var results = new List<TvDB_Episode>();
        var id = GetCrossRefTvDB()?.FirstOrDefault()?.TvDBID ?? -1;
        if (id != -1)
        {
            results.AddRange(RepoFactory.TvDB_Episode.GetBySeriesID(id).OrderBy(a => a.SeasonNumber)
                .ThenBy(a => a.EpisodeNumber));
        }

        return results;
    }

    public List<CrossRef_AniDB_TvDB> GetCrossRefTvDB()
    {
        return RepoFactory.CR_AniDB_TvDB.GetByAnimeID(AnimeId);
    }

    public List<CrossRef_AniDB_TraktV2> GetCrossRefTraktV2()
    {
        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            return GetCrossRefTraktV2(session);
        }
    }

    public List<CrossRef_AniDB_TraktV2> GetCrossRefTraktV2(ISession session)
    {
        return RepoFactory.CR_AniDB_Trakt.GetByAnimeID(session, AnimeId);
    }

    public List<CrossRef_AniDB_MAL> GetCrossRefMAL()
    {
        return RepoFactory.CR_AniDB_MAL.GetByAnimeID(AnimeId);
    }

    public List<TvDB_ImageFanart> GetTvDBImageFanarts()
    {
        var results = new List<TvDB_ImageFanart>();
        var id = GetCrossRefTvDB()?.FirstOrDefault()?.TvDBID ?? -1;
        if (id != -1)
        {
            results.AddRange(RepoFactory.TvDB_Fanart.GetBySeriesID(id));
        }

        return results;
    }

    public List<TvDB_ImagePoster> GetTvDBImagePosters()
    {
        var results = new List<TvDB_ImagePoster>();
        var id = GetCrossRefTvDB()?.FirstOrDefault()?.TvDBID ?? -1;
        if (id != -1)
        {
            results.AddRange(RepoFactory.TvDB_Poster.GetBySeriesID(id));
        }

        return results;
    }

    public List<TvDB_ImageWideBanner> GetTvDBImageWideBanners()
    {
        var results = new List<TvDB_ImageWideBanner>();
        var id = GetCrossRefTvDB()?.FirstOrDefault()?.TvDBID ?? -1;
        if (id != -1)
        {
            results.AddRange(RepoFactory.TvDB_Banner.GetBySeriesID(id));
        }

        return results;
    }

    public List<CrossRef_AniDB_Other> GetCrossRefMovieDB()
    {
        return RepoFactory.CR_AniDB_Other.GetByAnimeIDAndType(AnimeId,
            CrossRefType.MovieDB);
    }

    public List<MovieDB_Movie> GetMovieDBMovie()
    {
        return GetCrossRefMovieDB()
            .Select(xref => RepoFactory.TMDB_Movie.GetByOnlineID(int.Parse(xref.CrossRefID)))
            .ToList();
    }

    public List<MovieDB_Fanart> GetMovieDBFanarts()
    {
        return GetCrossRefMovieDB()
            .SelectMany(xref => RepoFactory.TMDB_Fanart.GetByMovieID(int.Parse(xref.CrossRefID)))
            .ToList();
    }

    public List<MovieDB_Poster> GetMovieDBPosters()
    {
        return GetCrossRefMovieDB()
            .SelectMany(xref => RepoFactory.TMDB_Movie_Poster.GetByMovieID(int.Parse(xref.CrossRefID)))
            .ToList();
    }

    public AniDB_Anime_DefaultImage GetDefaultPoster()
    {
        return RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(AnimeId, ImageSizeType.Poster);
    }

    public string PosterPathNoDefault
    {
        get
        {
            var fileName = Path.Combine(ImageUtils.GetAniDBImagePath(AnimeId), Picname);
            return fileName;
        }
    }

    private List<AniDB_Anime_DefaultImage> allPosters;

    public List<AniDB_Anime_DefaultImage> AllPosters
    {
        get
        {
            if (allPosters != null)
            {
                return allPosters;
            }

            var posters = new List<AniDB_Anime_DefaultImage>();
            posters.Add(new AniDB_Anime_DefaultImage
            {
                AniDB_Anime_DefaultImageID = AnimeId,
                ImageType = (int)ImageEntityType.AniDB_Cover
            });
            var tvdbposters = GetTvDBImagePosters()?.Where(img => img != null).Select(img =>
                new AniDB_Anime_DefaultImage
                {
                    AniDB_Anime_DefaultImageID = img.TvDB_ImagePosterID,
                    ImageType = (int)ImageEntityType.TvDB_Cover
                });
            if (tvdbposters != null)
            {
                posters.AddRange(tvdbposters);
            }

            var moviebposters = GetMovieDBPosters()?.Where(img => img != null).Select(img =>
                new AniDB_Anime_DefaultImage
                {
                    AniDB_Anime_DefaultImageID = img.MovieDB_PosterID,
                    ImageType = (int)ImageEntityType.TMDB_Poster
                });
            if (moviebposters != null)
            {
                posters.AddRange(moviebposters);
            }

            allPosters = posters;
            return posters;
        }
    }

    public string GetDefaultPosterPathNoBlanks()
    {
        var defaultPoster = GetDefaultPoster();
        if (defaultPoster == null)
        {
            return PosterPathNoDefault;
        }

        var imageType = (ImageEntityType)defaultPoster.ImageParentType;

        switch (imageType)
        {
            case ImageEntityType.AniDB_Cover:
                return PosterPath;

            case ImageEntityType.TvDB_Cover:
                var tvPoster =
                    RepoFactory.TvDB_Poster.GetByID(defaultPoster.ImageParentID);
                if (tvPoster != null)
                {
                    return tvPoster.GetFullImagePath();
                }
                else
                {
                    return PosterPath;
                }

            case ImageEntityType.TMDB_Poster:
                var moviePoster =
                    RepoFactory.TMDB_Movie_Poster.GetByID(defaultPoster.ImageParentID);
                if (moviePoster != null)
                {
                    return moviePoster.GetFullImagePath();
                }
                else
                {
                    return PosterPath;
                }
        }

        return PosterPath;
    }

    public ImageDetails GetDefaultPosterDetailsNoBlanks()
    {
        var details = new ImageDetails { ImageType = ImageEntityType.AniDB_Cover, ImageID = AnimeId };
        var defaultPoster = GetDefaultPoster();

        if (defaultPoster == null)
        {
            return details;
        }

        var imageType = (ImageEntityType)defaultPoster.ImageParentType;

        switch (imageType)
        {
            case ImageEntityType.AniDB_Cover:
                return details;

            case ImageEntityType.TvDB_Cover:
                var tvPoster =
                    RepoFactory.TvDB_Poster.GetByID(defaultPoster.ImageParentID);
                if (tvPoster != null)
                {
                    details = new ImageDetails
                    {
                        ImageType = ImageEntityType.TvDB_Cover,
                        ImageID = tvPoster.TvDB_ImagePosterID
                    };
                }

                return details;

            case ImageEntityType.TMDB_Poster:
                var moviePoster =
                    RepoFactory.TMDB_Movie_Poster.GetByID(defaultPoster.ImageParentID);
                if (moviePoster != null)
                {
                    details = new ImageDetails
                    {
                        ImageType = ImageEntityType.TMDB_Poster,
                        ImageID = moviePoster.MovieDB_PosterID
                    };
                }

                return details;
        }

        return details;
    }

    public AniDB_Anime_DefaultImage GetDefaultFanart()
    {
        return RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(AnimeId, ImageSizeType.Fanart);
    }

    public ImageDetails GetDefaultFanartDetailsNoBlanks()
    {
        var fanartRandom = new Random();

        ImageDetails details = null;
        var fanart = GetDefaultFanart();
        if (fanart == null)
        {
            var fanarts = Contract.AniDBAnime.Fanarts;
            if (fanarts == null || fanarts.Count == 0)
            {
                return null;
            }

            var art = fanarts[fanartRandom.Next(0, fanarts.Count)];
            details = new ImageDetails
            {
                ImageID = art.AniDB_Anime_DefaultImageID,
                ImageType = (ImageEntityType)art.ImageType
            };
            return details;
        }

        var imageType = (ImageEntityType)fanart.ImageParentType;

        switch (imageType)
        {
            case ImageEntityType.TvDB_FanArt:
                var tvFanart = RepoFactory.TvDB_Fanart.GetByID(fanart.ImageParentID);
                if (tvFanart != null)
                {
                    details = new ImageDetails
                    {
                        ImageType = ImageEntityType.TvDB_FanArt,
                        ImageID = tvFanart.TvDB_ImageFanartID
                    };
                }

                return details;

            case ImageEntityType.TMDB_Fanart:
                var movieFanart = RepoFactory.TMDB_Fanart.GetByID(fanart.ImageParentID);
                if (movieFanart != null)
                {
                    details = new ImageDetails
                    {
                        ImageType = ImageEntityType.TMDB_Fanart,
                        ImageID = movieFanart.MovieDB_FanartID
                    };
                }

                return details;
        }

        return null;
    }

    public string GetDefaultFanartOnlineURL()
    {
        var fanartRandom = new Random();


        if (GetDefaultFanart() == null)
        {
            // get a random fanart
            if (this.GetAnimeTypeEnum() == Shoko.Models.Enums.AnimeType.Movie)
            {
                var fanarts = GetMovieDBFanarts();
                if (fanarts.Count == 0)
                {
                    return string.Empty;
                }

                var movieFanart = fanarts[fanartRandom.Next(0, fanarts.Count)];
                return movieFanart.URL;
            }
            else
            {
                var fanarts = GetTvDBImageFanarts();
                if (fanarts.Count == 0)
                {
                    return null;
                }

                var tvFanart = fanarts[fanartRandom.Next(0, fanarts.Count)];
                return string.Format(Constants.URLS.TvDB_Images, tvFanart.BannerPath);
            }
        }

        var fanart = GetDefaultFanart();
        var imageType = (ImageEntityType)fanart.ImageParentType;

        switch (imageType)
        {
            case ImageEntityType.TvDB_FanArt:
                var tvFanart =
                    RepoFactory.TvDB_Fanart.GetByID(fanart.ImageParentID);
                if (tvFanart != null)
                {
                    return string.Format(Constants.URLS.TvDB_Images, tvFanart.BannerPath);
                }

                break;

            case ImageEntityType.TMDB_Fanart:
                var movieFanart =
                    RepoFactory.TMDB_Fanart.GetByID(fanart.ImageParentID);
                if (movieFanart != null)
                {
                    return movieFanart.URL;
                }

                break;
        }

        return string.Empty;
    }

    public AniDB_Anime_DefaultImage GetDefaultWideBanner()
    {
        return RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(AnimeId, ImageSizeType.WideBanner);
    }

    public ImageDetails GetDefaultWideBannerDetailsNoBlanks()
    {
        var bannerRandom = new Random();

        ImageDetails details;
        var banner = GetDefaultWideBanner();
        if (banner == null)
        {
            // get a random banner (only tvdb)
            if (this.GetAnimeTypeEnum() == Shoko.Models.Enums.AnimeType.Movie)
            {
                // MovieDB doesn't have banners
                return null;
            }

            var banners = Contract.AniDBAnime.Banners;
            if (banners == null || banners.Count == 0)
            {
                return null;
            }

            var art = banners[bannerRandom.Next(0, banners.Count)];
            details = new ImageDetails
            {
                ImageID = art.AniDB_Anime_DefaultImageID,
                ImageType = (ImageEntityType)art.ImageType
            };
            return details;
        }

        var imageType = (ImageEntityType)banner.ImageParentType;

        switch (imageType)
        {
            case ImageEntityType.TvDB_Banner:
                details = new ImageDetails
                {
                    ImageType = ImageEntityType.TvDB_Banner,
                    ImageID = banner.ToClient().TVWideBanner.TvDB_ImageWideBannerID
                };
                return details;
        }

        return null;
    }


    public string TagsString
    {
        get
        {
            var tags = GetTags();
            var temp = string.Empty;
            foreach (var tag in tags)
            {
                temp += tag.TagName + "|";
            }

            if (temp.Length > 2)
            {
                temp = temp.Substring(0, temp.Length - 2);
            }

            return temp;
        }
    }


    public List<AniDB_Tag> GetTags()
    {
        var tags = new List<AniDB_Tag>();
        foreach (var tag in GetAnimeTags())
        {
            var newTag = RepoFactory.AniDB_Tag.GetByTagID(tag.TagID);
            if (newTag != null)
            {
                tags.Add(newTag);
            }
        }

        return tags;
    }

    public List<Custom_Tag> GetCustomTagsForAnime()
    {
        return RepoFactory.Custom_Tag.GetByAnimeID(AnimeId);
    }

    public List<AniDB_Tag> GetAniDBTags(bool onlyVerified = true)
    {
        if (onlyVerified)
            return RepoFactory.AniDB_Tag.GetByAnimeID(AnimeId)
                .Where(tag => tag.Verified)
                .ToList();

        return RepoFactory.AniDB_Tag.GetByAnimeID(AnimeId);
    }

    public List<AniDB_Anime_Tag> GetAnimeTags()
    {
        return RepoFactory.AniDB_Anime_Tag.GetByAnimeID(AnimeId);
    }

    public List<AniDB_Anime_Relation> GetRelatedAnime()
    {
        return RepoFactory.AniDB_Anime_Relation.GetByAnimeID(AnimeId);
    }

    public List<AniDB_Anime_Similar> GetSimilarAnime()
    {
        return RepoFactory.AniDB_Anime_Similar.GetByAnimeID(AnimeId);
    }

    public List<AniDB_Anime_Character> GetAnimeCharacters()
    {
        return RepoFactory.AniDB_Anime_Character.GetByAnimeID(AnimeId);
    }

    public AniDB_AnimeTitle GetMainTitle()
    {
        var mainTitle = RepoFactory.AniDB_Anime_Title.GetByAnimeTitleAndValue(AnimeId, MainTitle);
        if (mainTitle != null)
            mainTitle.IsDefault = true;
        return mainTitle ?? new(AnimeId, TextLanguage.None, MainTitle, TitleType.Main, true);
    }

    public List<AniDB_AnimeTitle> GetTitles()
    {
        return RepoFactory.AniDB_Anime_Title.GetByAnimeId(AnimeId);
    }

    private string GetFormattedTitle(List<AniDB_AnimeTitle> titles = null)
    {
        // Get the titles now if they were not provided as an argument.
        titles ??= GetTitles();

        // Check each preferred language in order.
        foreach (var thisLanguage in Languages.PreferredNamingLanguages.Select(a => a.Language))
        {
            // First check the main title.
            var title = titles.FirstOrDefault(title => title.TitleType == TitleType.Main && title.Language == thisLanguage);
            if (title != null) return title.Value;

            // Then check for an official title.
            title = titles.FirstOrDefault(title => title.TitleType == TitleType.Official && title.Language == thisLanguage);
            if (title != null) return title.Value;

            // Then check for _any_ title at all, if there is no main or official title in the langugage.
            if (Utils.SettingsProvider.GetSettings().LanguageUseSynonyms)
            {
                title = titles.FirstOrDefault(title => title.Language == thisLanguage);
                if (title != null) return title.Value;
            }
        }

        // Otherwise just use the cached main title.
        return MainTitle;
    }

    public AniDB_Vote UserVote
    {
        get
        {
            try
            {
                return RepoFactory.AniDB_Vote.GetByAnimeID(AnimeId);
            }
            catch (Exception ex)
            {
                logger.Error($"Error in  UserVote: {ex}");
                return null;
            }
        }
    }

    public string PreferredTitle => GetFormattedTitle();



    public List<AniDB_Episode> GetAniDBEpisodes()
    {
        return RepoFactory.AniDB_Episode.GetByAnimeID(AnimeId);
    }

    #endregion

    public AniDB_Anime()
    {
    }

    #region Init and Populate

    public ShokoSeries CreateAnimeSeriesAndGroup(ShokoSeries existingSeries = null, int? existingGroupID = null)
    {
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        // Create a new AnimeSeries record
        var series = existingSeries ?? new ShokoSeries();

        series.Populate(this);
        // Populate before making a group to ensure IDs and stats are set for group filters.
        RepoFactory.Shoko_Series.Save(series, false, false, true);

        if (existingGroupID == null)
        {
            var grp = new AnimeGroupCreator().GetOrCreateSingleGroupForSeries(series);
            series.AnimeGroupID = grp.AnimeGroupID;
        }
        else
        {
            var grp = RepoFactory.Shoko_Group.GetByID(existingGroupID.Value) ??
                      new AnimeGroupCreator().GetOrCreateSingleGroupForSeries(series);
            series.AnimeGroupID = grp.AnimeGroupID;
        }

        RepoFactory.Shoko_Series.Save(series, false, false, true);

        // check for TvDB associations
        if (Restricted == 0)
        {
            var settings = Utils.SettingsProvider.GetSettings();
            if (settings.TvDB.AutoLink && !series.IsTvDBAutoMatchingDisabled)
            {
                var cmd = commandFactory.Create<CommandRequest_TvDBSearchAnime>(c => c.AnimeID = AnimeId);
                cmd.Save();
            }

            // check for Trakt associations
            if (settings.TraktTv.Enabled &&
                !string.IsNullOrEmpty(settings.TraktTv.AuthToken) &&
                !series.IsTraktAutoMatchingDisabled)
            {
                var cmd = commandFactory.Create<CommandRequest_TraktSearchAnime>(c => c.AnimeID = AnimeId);
                cmd.Save();
            }

            if (AnimeType == (int)Shoko.Models.Enums.AnimeType.Movie && !series.IsTMDBAutoMatchingDisabled)
            {
                var cmd = commandFactory.Create<CommandRequest_MovieDBSearchAnime>(c => c.AnimeID = AnimeId);
                cmd.Save();
            }
        }

        return series;
    }

    #endregion

    #region Contracts

    private CL_AniDB_Anime GenerateContract(List<AniDB_AnimeTitle> titles)
    {
        var sw = Stopwatch.StartNew();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Updating Character Contracts");
        var characters = GetCharactersContract();
        sw.Stop();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Updated Character Contracts in {sw.Elapsed.TotalSeconds:0.00###}s");
        sw.Restart();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Getting MovieDB Fanarts");
        var movDbFanart = GetMovieDBFanarts();
        sw.Stop();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Got MovieDB Fanarts in {sw.Elapsed.TotalSeconds:0.00###}s");
        sw.Restart();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Getting TvDB Fanarts");
        var tvDbFanart = GetTvDBImageFanarts();
        sw.Stop();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Got TvDB Fanarts in {sw.Elapsed.TotalSeconds:0.00###}s");
        sw.Restart();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Getting TvDB Banners");
        var tvDbBanners = GetTvDBImageWideBanners();
        sw.Stop();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Got TvDB Banners in {sw.Elapsed.TotalSeconds:0.00###}s");
        sw.Restart();

        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Generating Images Contract");
        var cl = GenerateContract(titles, null, characters, movDbFanart, tvDbFanart, tvDbBanners);
        var defFanart = GetDefaultFanart();
        var defPoster = GetDefaultPoster();
        var defBanner = GetDefaultWideBanner();

        cl.DefaultImageFanart = defFanart?.ToClient();
        cl.DefaultImagePoster = defPoster?.ToClient();
        cl.DefaultImageWideBanner = defBanner?.ToClient();

        sw.Stop();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Generated Images Contract in {sw.Elapsed.TotalSeconds:0.00###}s");
        sw.Restart();

        return cl;
    }

    private CL_AniDB_Anime GenerateContract(List<AniDB_AnimeTitle> titles, DefaultAnimeImages defaultImages,
        List<CL_AniDB_Character> characters, IList<MovieDB_Fanart> movDbFanart,
        IList<TvDB_ImageFanart> tvDbFanart,
        IList<TvDB_ImageWideBanner> tvDbBanners)
    {
        var cl = this.ToClient();
        cl.FormattedTitle = GetFormattedTitle(titles);
        cl.Characters = characters;

        if (defaultImages != null)
        {
            cl.DefaultImageFanart = defaultImages.Fanart?.ToContract();
            cl.DefaultImagePoster = defaultImages.Poster?.ToContract();
            cl.DefaultImageWideBanner = defaultImages.WideBanner?.ToContract();
        }

        cl.Fanarts = new List<CL_AniDB_Anime_DefaultImage>();
        if (movDbFanart != null && movDbFanart.Any())
        {
            cl.Fanarts.AddRange(movDbFanart.Select(a => new CL_AniDB_Anime_DefaultImage
            {
                ImageType = (int)ImageEntityType.TMDB_Fanart,
                MovieFanart = a,
                AniDB_Anime_DefaultImageID = a.MovieDB_FanartID
            }));
        }

        if (tvDbFanart != null && tvDbFanart.Any())
        {
            cl.Fanarts.AddRange(tvDbFanart.Select(a => new CL_AniDB_Anime_DefaultImage
            {
                ImageType = (int)ImageEntityType.TvDB_FanArt,
                TVFanart = a,
                AniDB_Anime_DefaultImageID = a.TvDB_ImageFanartID
            }));
        }

        cl.Banners = tvDbBanners?.Select(a => new CL_AniDB_Anime_DefaultImage
        {
            ImageType = (int)ImageEntityType.TvDB_Banner,
            TVWideBanner = a,
            AniDB_Anime_DefaultImageID = a.TvDB_ImageWideBannerID
        })
            .ToList();

        if (cl.Fanarts?.Count == 0)
        {
            cl.Fanarts = null;
        }

        if (cl.Banners?.Count == 0)
        {
            cl.Banners = null;
        }

        return cl;
    }

    public List<CL_AniDB_Character> GetCharactersContract()
    {
        try
        {
            return RepoFactory.AniDB_Character.GetCharactersAndSeiyuuForAnime(AnimeId)
                .Select(a => a.Character.ToClient(a.CharacterType, a.Seiyuu)).ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return new List<CL_AniDB_Character>();
    }

    public static void UpdateContractDetailedBatch(ISessionWrapper session,
        IReadOnlyCollection<AniDB_Anime> animeColl)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (animeColl == null)
        {
            throw new ArgumentNullException(nameof(animeColl));
        }

        var animeIds = animeColl.Select(a => a.AnimeID).ToArray();

        var titlesByAnime = RepoFactory.AniDB_Anime_Title.GetByAnimeIDs(session, animeIds);
        var animeTagsByAnime = RepoFactory.AniDB_Anime_Tag.GetByAnimeIDs(animeIds);
        var tagsByAnime = RepoFactory.AniDB_Tag.GetByAnimeIDs(animeIds);
        var custTagsByAnime = RepoFactory.Custom_Tag.GetByAnimeIDs(animeIds);
        var voteByAnime = RepoFactory.AniDB_Vote.GetByAnimeIDs(animeIds);
        var audioLangByAnime = RepoFactory.CR_AniDB_File_Languages.GetLanguagesByAnime(animeIds);
        var subtitleLangByAnime = RepoFactory.CR_AniDB_File_Subtitles.GetLanguagesByAnime(animeIds);
        var vidQualByAnime = animeIds
            .Select(animeID => (animeID,
                new HashSet<string>(RepoFactory.Shoko_Video.GetByAniDBAnimeID(animeID)
                    .Select(a => a?.AniDB?.RawSource).Where(a => a != null))))
            .ToDictionary(a => a.animeID, tuple => tuple.Item2);
        var epVidQualByAnime = animeIds
            .SelectMany(animeID => RepoFactory.Shoko_Video.GetByAniDBAnimeID(animeID).Select(a => a.AniDB)
                .Where(a => a?.RawSource != null &&
                            a.Episodes.Any(b => b.AnimeID == animeID && b.EpisodeType == (int)EpisodeType.Episode))
                .Select(a => (animeID, a.RawSource))).GroupBy(a => a.animeID).ToDictionary(a => a.Key,
                tuples => new
                {
                    AnimeID = tuples.Key,
                    VideoQualityEpisodeCount =
                        tuples.GroupBy(b => b.RawSource).ToDictionary(b => b.Key, b => b.Count())
                });
        var defImagesByAnime = RepoFactory.AniDB_Anime.GetDefaultImagesByAnime(session, animeIds);
        var charsByAnime = RepoFactory.AniDB_Character.GetCharacterAndSeiyuuByAnime(session, animeIds);
        var movDbFanartByAnime = RepoFactory.TMDB_Fanart.GetByAnimeIDs(session, animeIds);
        var tvDbBannersByAnime = RepoFactory.TvDB_Banner.GetByAnimeIDs(session, animeIds);
        var tvDbFanartByAnime = RepoFactory.TvDB_Fanart.GetByAnimeIDs(session, animeIds);

        foreach (var anime in animeColl)
        {
            var contract = new CL_AniDB_AnimeDetailed();
            var animeTitles = titlesByAnime[anime.AnimeID];

            defImagesByAnime.TryGetValue(anime.AnimeID, out var defImages);

            var characterContracts = charsByAnime[anime.AnimeID].Select(ac => ac.ToClient()).ToList();
            var movieDbFanart = movDbFanartByAnime[anime.AnimeID].ToList();
            var tvDbBanners = tvDbBannersByAnime[anime.AnimeID].ToList();
            var tvDbFanart = tvDbFanartByAnime[anime.AnimeID].ToList();

            contract.AniDBAnime = anime.GenerateContract(animeTitles.ToList(), defImages, characterContracts,
                movieDbFanart, tvDbFanart, tvDbBanners);

            // Anime titles
            contract.AnimeTitles = titlesByAnime[anime.AnimeID]
                .Select(t => new CL_AnimeTitle
                {
                    AnimeID = t.AnimeID,
                    Language = t.LanguageCode,
                    Title = t.Value,
                    TitleType = t.TitleType.ToString().ToLower()
                })
                .ToList();

            // Seasons
            contract.Stat_AllSeasons.UnionWith(anime.GetSeasons().Select(tuple => $"{tuple.Season} {tuple.Year}"));

            // Anime tags
            var dictAnimeTags = animeTagsByAnime[anime.AnimeID]
                .ToDictionary(t => t.TagID);

            contract.Tags = tagsByAnime[anime.AnimeID]
                .Select(t =>
                {
                    var ctag = new CL_AnimeTag
                    {
                        TagID = t.TagID,
                        GlobalSpoiler = t.GlobalSpoiler ? 1 : 0,
                        LocalSpoiler = 0,
                        Weight = 0,
                        TagName = t.TagName,
                        TagDescription = t.TagDescription,
                    };

                    if (dictAnimeTags.TryGetValue(t.TagID, out var xref))
                    {
                        ctag.LocalSpoiler = xref.LocalSpoiler ? 1 : 0;
                        ctag.Weight = xref.Weight;
                    }

                    return ctag;
                })
                .ToList();

            // Custom tags
            contract.CustomTags = custTagsByAnime[anime.AnimeID];

            // Vote

            if (voteByAnime.TryGetValue(anime.AnimeID, out var vote))
            {
                contract.UserVote = vote;
            }


            // Subtitle languages
            contract.Stat_AudioLanguages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            if (audioLangByAnime.TryGetValue(anime.AnimeID, out var langStat))
            {
                contract.Stat_AudioLanguages.UnionWith(langStat);
            }

            // Audio languages
            contract.Stat_SubtitleLanguages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            if (subtitleLangByAnime.TryGetValue(anime.AnimeID, out langStat))
            {
                contract.Stat_SubtitleLanguages.UnionWith(langStat);
            }

            // Anime video quality

            contract.Stat_AllVideoQuality = vidQualByAnime.TryGetValue(anime.AnimeID, out var vidQual)
                ? vidQual
                : new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            // Episode video quality

            contract.Stat_AllVideoQuality_Episodes = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            if (epVidQualByAnime.TryGetValue(anime.AnimeID, out var vidQualStat) &&
                vidQualStat.VideoQualityEpisodeCount.Count > 0)
            {
                contract.Stat_AllVideoQuality_Episodes.UnionWith(vidQualStat.VideoQualityEpisodeCount
                    .Where(kvp => kvp.Value >= anime.EpisodeCountNormal)
                    .Select(kvp => kvp.Key));
            }

            anime.Contract = contract;
        }
    }

    public void UpdateContractDetailed()
    {
        var total = Stopwatch.StartNew();
        var sw = Stopwatch.StartNew();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Start");
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Getting Titles");
        var animeTitles = RepoFactory.AniDB_Anime_Title.GetByAnimeId(AnimeId);
        sw.Stop();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Got Titles in {sw.Elapsed.TotalSeconds:0.00###}s");
        sw.Restart();

        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Generating AniDB_AnimeDetailed");
        var cl = new CL_AniDB_AnimeDetailed
        {
            AniDBAnime = GenerateContract(animeTitles),
            AnimeTitles = new List<CL_AnimeTitle>(),
            Tags = new List<CL_AnimeTag>(),
            CustomTags = new List<Custom_Tag>()
        };

        sw.Stop();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Generated AniDB_AnimeDetailed in {sw.Elapsed.TotalSeconds:0.00###}s");

        // get all the anime titles
        if (animeTitles != null)
        {
            foreach (var title in animeTitles)
            {
                var ctitle = new CL_AnimeTitle
                {
                    AnimeID = title.AnimeID,
                    Language = title.LanguageCode,
                    Title = title.Value,
                    TitleType = title.TitleType.ToString().ToLower()
                };
                cl.AnimeTitles.Add(ctitle);
            }
        }

        cl.Stat_AllSeasons.UnionWith(this.GetSeasons().Select(tuple => $"{tuple.Season} {tuple.Year}"));

        sw.Restart();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Generating Tag Contracts");
        var dictAnimeTags = GetAnimeTags()
            .ToDictionary(xref => xref.TagID);
        foreach (var tag in GetAniDBTags())
        {
            var ctag = new CL_AnimeTag
            {
                TagID = tag.TagID,
                GlobalSpoiler = tag.GlobalSpoiler ? 1 : 0,
                LocalSpoiler = 0,
                Weight = 0,
                TagName = tag.TagName,
                TagDescription = tag.TagDescription,
            };

            if (dictAnimeTags.TryGetValue(tag.TagID, out var xref))
            {
                ctag.LocalSpoiler = xref.LocalSpoiler ? 1 : 0;
                ctag.Weight = xref.Weight;
            }

            cl.Tags.Add(ctag);
        }


        // Get all the custom tags
        foreach (var custag in GetCustomTagsForAnime())
        {
            cl.CustomTags.Add(custag);
        }

        sw.Stop();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Generated Tag Contracts in {sw.Elapsed.TotalSeconds:0.00###}s");
        sw.Restart();

        if (UserVote != null)
        {
            cl.UserVote = UserVote;
        }

        sw.Restart();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Getting Audio Languages");
        using var session = DatabaseFactory.SessionFactory.OpenSession().Wrap();
        // audio languages
        cl.Stat_AudioLanguages = RepoFactory.CR_AniDB_File_Languages.GetLanguagesForAnime(AnimeId);
        sw.Stop();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Got Audio Languages in {sw.Elapsed.TotalSeconds:0.00###}s");

        sw.Restart();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Getting Subtitle Languages");
        // subtitle languages
        cl.Stat_SubtitleLanguages = RepoFactory.CR_AniDB_File_Subtitles.GetLanguagesForAnime(AnimeId);
        sw.Stop();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Got Subtitle Languages in {sw.Elapsed.TotalSeconds:0.00###}s");

        sw.Restart();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Generating Video Quality Contracts");
        cl.Stat_AllVideoQuality = new HashSet<string>(RepoFactory.Shoko_Video.GetByAniDBAnimeID(AnimeId)
            .Select(a => a.AniDB?.RawSource).Where(a => a != null), StringComparer.InvariantCultureIgnoreCase);
        sw.Stop();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Generated Video Quality Contracts in {sw.Elapsed.TotalSeconds:0.00###}s");

        sw.Restart();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Generating Episode Quality Contracts");
        cl.Stat_AllVideoQuality_Episodes = new HashSet<string>(
            RepoFactory.Shoko_Video.GetByAniDBAnimeID(AnimeId).Select(a => a.AniDB)
                .Where(a => a != null &&
                            a.Episodes.Any(b => b.AnimeID == AnimeId && b.EpisodeType == (int)EpisodeType.Episode))
                .Select(a => a.RawSource).Where(a => a != null).GroupBy(b => b)
                .ToDictionary(b => b.Key, b => b.Count()).Where(a => a.Value >= EpisodeCountNormal).Select(a => a.Key),
            StringComparer.InvariantCultureIgnoreCase);
        sw.Stop();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Generated Episode Quality Contracts in {sw.Elapsed.TotalSeconds:0.00###}s");

        Contract = cl;

        total.Stop();
        logger.Trace($"Updating AniDB_Anime Contract {AnimeId} | Finished in {total.Elapsed.TotalSeconds:0.00###}s");
    }

    #endregion

    public static void UpdateStatsByAnimeID(int id)
    {
        var an = RepoFactory.AniDB_Anime.GetByAnidbAnimeId(id);
        if (an != null)
        {
            RepoFactory.AniDB_Anime.Save(an);
        }

        var series = RepoFactory.Shoko_Series.GetByAnidbAnimeId(id);
        // Updating stats saves everything and updates groups
        series?.UpdateStats(true, true);
        series?.AnimeGroup?.TopLevelAnimeGroup?.UpdateStatsFromTopLevel(true, true);
    }

    public DateTime GetDateTimeUpdated()
    {
        var update = RepoFactory.AniDB_Anime_Update.GetByAnimeID(AnimeId);
        return update?.UpdatedAt ?? DateTime.MinValue;
    }
}
