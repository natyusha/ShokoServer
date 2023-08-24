using System.Net;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Properties;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server.TMDB;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.ImageDownload;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands;

[Command(CommandRequestType.ImageDownload_New)]
public class CommandRequest_DownloadImage : CommandRequestImplementation
{
    private const string FailedToDownloadNoID = "Image failed to download: Can\'t find valid {EntityType} with ID: {EntityID}";

    private const string FailedToDownloadNoImpl = "Image failed to download: No implementation found for {EntityType}";

    [XmlIgnore, JsonIgnore]
    private readonly IUDPConnectionHandler _handler;

    [XmlIgnore, JsonIgnore]
    private readonly TMDBHelper _tmdbHelper;

    public virtual int EntityID { get; set; }

    public virtual int ImageType { get; set; } = 0;

    public virtual int DataSource { get; set; }

    public virtual bool ForceDownload { get; set; }

    [XmlIgnore, JsonIgnore]
    public virtual ImageEntityType_New ImageTypeEnum
    {
        get => (ImageEntityType_New)ImageType;
        set => ImageType = (int)value;
    }

    [XmlIgnore, JsonIgnore]
    public virtual DataSourceEnum DataSourceEnum
    {
        get => (DataSourceEnum)DataSource;
        set => DataSource = (int)value;
    }

    public override CommandRequestPriority DefaultPriority
        => CommandRequestPriority.Priority2;

    public override QueueStateStruct PrettyDescription
        => new()
        {
            message = "Downloading Image {0} {1}: {2}",
            queueState = QueueStateEnum.DownloadImage,
            extraParams = new[] { DataSourceEnum.ToString(), ImageTypeEnum.ToString(), EntityID.ToString() }
        };

    protected override void Process()
    {
        Logger.LogInformation("Processing {CommandRequest}: {EntityID}", nameof(CommandRequest_DownloadImage), EntityID);
        ImageDownloadRequest req = null;

        switch (DataSourceEnum)
        {
            case DataSourceEnum.AniDB:
                switch (ImageTypeEnum)
                {
                    case ImageEntityType_New.Poster:
                        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(EntityID);
                        if (anime == null)
                        {
                            Logger.LogWarning(FailedToDownloadNoID, "AniDB anime poster", EntityID);
                            return;
                        }

                        req = new ImageDownloadRequest(anime, ForceDownload, _handler.ImageServerUrl);
                        break;

                    case ImageEntityType_New.Character:
                        var character = RepoFactory.AniDB_Character.GetByCharID(EntityID);
                        if (character == null)
                        {
                            Logger.LogWarning(FailedToDownloadNoID, "AniDB character", EntityID);
                            return;
                        }

                        req = new ImageDownloadRequest(character, ForceDownload, _handler.ImageServerUrl);
                        break;

                    case ImageEntityType_New.Person:
                        var creator = RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(EntityID);
                        if (creator == null)
                        {
                            Logger.LogWarning(FailedToDownloadNoID, "AniDB Creator", EntityID);
                            return;
                        }

                        req = new ImageDownloadRequest(creator, ForceDownload, _handler.ImageServerUrl);
                        break;
                }
                break;
            case DataSourceEnum.TvDB:
                switch (ImageTypeEnum)
                {
                    case ImageEntityType_New.Thumbnail:
                        var ep = RepoFactory.TvDB_Episode.GetByID(EntityID);
                        if (string.IsNullOrEmpty(ep?.Filename))
                        {
                            Logger.LogWarning(FailedToDownloadNoID, "TvDB episode", EntityID);
                            return;
                        }

                        req = new ImageDownloadRequest(ep, ForceDownload);
                        break;

                    case ImageEntityType_New.Backdrop:
                        var tvdbBackdrop = RepoFactory.TvDB_ImageFanart.GetByID(EntityID);
                        if (string.IsNullOrEmpty(tvdbBackdrop?.BannerPath))
                        {
                            Logger.LogWarning(FailedToDownloadNoID, "TvDB backdrop", EntityID);
                            RemoveImageRecord();
                            return;
                        }

                        req = new ImageDownloadRequest(tvdbBackdrop, ForceDownload);
                        break;

                    case ImageEntityType_New.Poster:
                        var tvdbPoster = RepoFactory.TvDB_ImagePoster.GetByID(EntityID);
                        if (string.IsNullOrEmpty(tvdbPoster?.BannerPath))
                        {
                            Logger.LogWarning(FailedToDownloadNoID, "TvDB poster", EntityID);
                            RemoveImageRecord();
                            return;
                        }

                        req = new ImageDownloadRequest(tvdbPoster, ForceDownload);
                        break;

                    case ImageEntityType_New.Banner:
                        var tvdbBanner = RepoFactory.TvDB_ImageWideBanner.GetByID(EntityID);
                        if (string.IsNullOrEmpty(tvdbBanner?.BannerPath))
                        {
                            Logger.LogWarning(FailedToDownloadNoID, "TvDB banner", EntityID);
                            RemoveImageRecord();
                            return;
                        }

                        req = new ImageDownloadRequest(tvdbBanner, ForceDownload);
                        break;
                }
                break;
            case DataSourceEnum.TMDB:
                var tmdbImage = RepoFactory.TMDB_ImageMetadata.GetByID(EntityID);
                if (string.IsNullOrEmpty(tmdbImage.RemoteURL))
                {
                    Logger.LogWarning(FailedToDownloadNoID, $"TMDB {ImageTypeEnum}", EntityID);
                    RemoveImageRecord();
                    return;
                }

                req = new ImageDownloadRequest(tmdbImage, ForceDownload, _tmdbHelper.ImageServerUrl);
                break;
        }

        if (req == null)
        {
            Logger.LogWarning(FailedToDownloadNoImpl, ImageTypeEnum.ToString());
            return;
        }

        try
        {
            // If this has any issues, it will throw an exception, so the catch below will handle it.
            var result = req.DownloadNow();
            switch (result)
            {
                case ImageDownloadResult.Success:
                    Logger.LogInformation("Image downloaded; {FilePath} from {DownloadUrl}", req.FilePath, req.DownloadUrl);
                    break;
                case ImageDownloadResult.Cached:
                    Logger.LogDebug("Image already in cache; {FilePath} from {DownloadUrl}", req.FilePath, req.DownloadUrl);
                    break;
                case ImageDownloadResult.Failure:
                    Logger.LogError("Image failed to download; {FilePath} from {DownloadUrl}", req.FilePath, req.DownloadUrl);
                    break;
                case ImageDownloadResult.RemovedResource:
                    Logger.LogWarning("Image failed to download and the local entry has been removed; {FilePath} from {DownloadUrl}", req.FilePath,
                        req.DownloadUrl);
                    break;
                case ImageDownloadResult.InvalidResource:
                    Logger.LogError("Image failed to download and the local entry could not be removed; {FilePath} from {DownloadUrl}", req.FilePath,
                        req.DownloadUrl);
                    break;
            }
        }
        catch (WebException e)
        {
            Logger.LogWarning("Error processing {CommandRequest}: {Url} ({EntityID}) - {Message}",
                nameof(CommandRequest_DownloadImage),
                req.DownloadUrl,
                EntityID,
                e.Message);
            // Remove the record if the image doesn't exist or can't download
            if (!RemoveImageRecord())
                Logger.LogWarning("Unable to remove record.");
        }
    }

    private bool RemoveImageRecord()
    {
        switch (DataSourceEnum)
        {
            case DataSourceEnum.TvDB:
                switch (ImageTypeEnum)
                {
                    case ImageEntityType_New.Backdrop:
                        var backdrop = RepoFactory.TvDB_ImageFanart.GetByID(EntityID);
                        if (backdrop == null)
                            break;

                        RepoFactory.TvDB_ImageFanart.Delete(backdrop);
                        return true;

                    case ImageEntityType_New.Poster:
                        var poster = RepoFactory.TvDB_ImagePoster.GetByID(EntityID);
                        if (poster == null)
                            break;

                        RepoFactory.TvDB_ImagePoster.Delete(poster);
                        return true;

                    case ImageEntityType_New.Banner:
                        var banner = RepoFactory.TvDB_ImageWideBanner.GetByID(EntityID);
                        if (banner == null)
                            break;

                        RepoFactory.TvDB_ImageWideBanner.Delete(banner);
                        return true;
                }
                break;
            
            case DataSourceEnum.TMDB:
                var tmdbImage = RepoFactory.TMDB_ImageMetadata.GetByID(EntityID);
                if (tmdbImage == null)
                    break;

                RepoFactory.TMDB_ImageMetadata.Delete(tmdbImage);
                return true;
                
        }
        return false;
    }

    public override void GenerateCommandID()
    {
        CommandID = $"{nameof(CommandRequest_DownloadImage)}_{DataSourceEnum}_{ImageTypeEnum}_{EntityID}";
    }

    protected override bool Load()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        EntityID = int.Parse(docCreator.TryGetProperty(nameof(CommandRequest_DownloadImage), nameof(EntityID)));
        ImageType = int.Parse(docCreator.TryGetProperty(nameof(CommandRequest_DownloadImage), nameof(ImageType)));
        DataSource = int.Parse(docCreator.TryGetProperty(nameof(CommandRequest_DownloadImage), nameof(DataSource)));
        ForceDownload = bool.Parse(docCreator.TryGetProperty(nameof(CommandRequest_DownloadImage), nameof(ForceDownload)));

        return true;
    }

    public CommandRequest_DownloadImage(ILoggerFactory loggerFactory, IUDPConnectionHandler handler, TMDBHelper tmdbHelper) : base(
        loggerFactory)
    {
        _handler = handler;
        _tmdbHelper = tmdbHelper;
    }

    protected CommandRequest_DownloadImage()
    {
    }
}
