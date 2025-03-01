﻿using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Properties;
using Shoko.Commons.Queue;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands;

[Command(CommandRequestType.ValidateAllImages)]
public class CommandRequest_ValidateAllImages : CommandRequestImplementation
{
    private const string ScanForType = "Scanning {EntityType} for corrupted images...";

    private const string FoundCorruptedOfType = "Found {Count} corrupted {EntityType}";

    private const string CorruptImageFound = "Corrupt image found! Attempting Redownload: {FullImagePath}";

    private const string ReQueueingForDownload = "Deleting and queueing for redownload {CurrentCount}/{TotalCount}";

    private readonly ICommandRequestFactory _commandFactory;

    private readonly IServerSettings _settings;

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority3;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Validating Images {0}",
        queueState = QueueStateEnum.ValidateAllImages,
        extraParams = new[] { string.Empty }
    };

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_ValidateAllImages");

        var count = 0;
        UpdateProgress(Resources.Command_ValidateAllImages_TvDBEpisodes);
        Logger.LogInformation(ScanForType, "TvDB episodes");
        var episodes = RepoFactory.TvDB_Episode.GetAll()
            .Where(episode => !Misc.IsImageValid(episode.GetFullImagePath()))
            .ToList();

        Logger.LogInformation(FoundCorruptedOfType, episodes.Count, episodes.Count == 1 ? "TvDB episode thumbnail" : "TvDB episode thumbnails");
        foreach (var episode in episodes)
        {
            RemoveImageAndQueueRedownload(ImageEntityType.TvDB_Episode, episode.TvDB_EpisodeID, episode);
            if (++count % 10 == 0)
            {
                Logger.LogInformation(ReQueueingForDownload, count, episodes.Count);
                UpdateProgress($"{Resources.Command_ValidateAllImages_TvDBEpisodes} - {count}/{episodes.Count}");
            }
        }

        if (_settings.TvDB.AutoFanart)
        {
            count = 0;
            UpdateProgress(Resources.Command_ValidateAllImages_TvDBPosters);
            Logger.LogInformation(ScanForType, "TvDB posters");
            var posters = RepoFactory.TvDB_ImagePoster.GetAll()
                .Where(poster => !Misc.IsImageValid(poster.GetFullImagePath()))
                .ToList();

            Logger.LogInformation(FoundCorruptedOfType, posters.Count, posters.Count == 1 ? "TvDB poster" : "TvDB posters");
            foreach (var poster in posters)
            {
                RemoveImageAndQueueRedownload(ImageEntityType.TvDB_Cover, poster.TvDB_ImagePosterID, poster);
                if (++count % 10 == 0)
                {
                    Logger.LogInformation(ReQueueingForDownload, count, posters.Count);
                    UpdateProgress($"{Resources.Command_ValidateAllImages_TvDBPosters} - {count}/{posters.Count}");
                }
            }
        }

        if (_settings.TvDB.AutoPosters)
        {
            count = 0;
            UpdateProgress(Resources.Command_ValidateAllImages_TvDBFanarts);
            Logger.LogInformation(ScanForType, "TvDB fanart");
            var fanartList = RepoFactory.TvDB_ImageFanart.GetAll()
                .Where(fanart => !Misc.IsImageValid(fanart.GetFullImagePath()))
                .ToList();

            Logger.LogInformation(FoundCorruptedOfType, fanartList.Count, "TvDB fanart");
            foreach (var fanart in fanartList)
            {
                RemoveImageAndQueueRedownload(ImageEntityType.TvDB_FanArt, fanart.TvDB_ImageFanartID, fanart);
                if (++count % 10 == 0)
                {
                    Logger.LogInformation(ReQueueingForDownload, count, fanartList.Count);
                    UpdateProgress($"{Resources.Command_ValidateAllImages_TvDBFanarts} - {count}/{fanartList.Count}");
                }
            }
        }

        if (_settings.TvDB.AutoWideBanners)
        {
            count = 0;
            Logger.LogInformation(ScanForType, "TvDB wide-banners");
            UpdateProgress(Resources.Command_ValidateAllImages_TvDBBanners);
            var wideBanners = RepoFactory.TvDB_ImageWideBanner.GetAll()
                .Where(wideBanner => !Misc.IsImageValid(wideBanner.GetFullImagePath()))
                .ToList();

            Logger.LogInformation(FoundCorruptedOfType, wideBanners.Count, wideBanners.Count == 1 ? "TvDB wide-banner" : "TvDB wide-banners");
            foreach (var wideBanner in wideBanners)
            {
                RemoveImageAndQueueRedownload(ImageEntityType.TvDB_Banner, wideBanner.TvDB_ImageWideBannerID, wideBanner);
                if (++count % 10 == 0)
                {
                    Logger.LogInformation(ReQueueingForDownload, count, wideBanners.Count);
                    UpdateProgress($"{Resources.Command_ValidateAllImages_TvDBBanners} - {count}/{wideBanners.Count}");
                }
            }
        }

        if (_settings.MovieDb.AutoPosters)
        {
            count = 0;
            UpdateProgress(Resources.Command_ValidateAllImages_MovieDBPosters);
            Logger.LogInformation(ScanForType, "TMDB posters");
            var posters = RepoFactory.MovieDB_Poster.GetAll()
                .Where(poster => !Misc.IsImageValid(poster.GetFullImagePath()))
                .ToList();

            Logger.LogInformation(FoundCorruptedOfType, posters.Count, posters.Count == 1 ? "TMDB poster" : "TMDB posters");
            foreach (var poster in posters)
            {
                RemoveImageAndQueueRedownload(ImageEntityType.MovieDB_Poster, poster.MovieDB_PosterID, poster);
                if (++count % 10 == 0)
                {
                    Logger.LogInformation(ReQueueingForDownload, count, posters.Count);
                    UpdateProgress($"{Resources.Command_ValidateAllImages_MovieDBPosters} - {count}/{posters.Count}");
                }
            }
        }

        if (_settings.MovieDb.AutoFanart)
        {
            UpdateProgress(Resources.Command_ValidateAllImages_MovieDBFanarts);
            count = 0;
            Logger.LogInformation(ScanForType, "TMDB fanart");
            var fanartList = RepoFactory.MovieDB_Fanart.GetAll()
                .Where(fanart => !Misc.IsImageValid(fanart.GetFullImagePath()))
                .ToList();

            Logger.LogInformation(FoundCorruptedOfType, fanartList.Count, "TMDB fanart");
            foreach (var fanart in fanartList)
            {
                RemoveImageAndQueueRedownload(ImageEntityType.MovieDB_FanArt, fanart.MovieDB_FanartID, fanart);
                if (++count % 10 == 0)
                {
                    Logger.LogInformation(ReQueueingForDownload, count, fanartList.Count);
                    UpdateProgress($"{Resources.Command_ValidateAllImages_MovieDBFanarts} - {count}/{fanartList.Count}");
                }
            }
        }

        count = 0;
        UpdateProgress(Resources.Command_ValidateAllImages_AniDBPosters);
        Logger.LogInformation(ScanForType, "AniDB posters");
        var animeList = RepoFactory.AniDB_Anime.GetAll()
            .Where(anime => !Misc.IsImageValid(anime.PosterPath))
            .ToList();

        Logger.LogInformation(FoundCorruptedOfType, animeList.Count, animeList.Count == 1 ? "AniDB poster" : "AniDB posters");
        foreach (var anime in animeList)
        {
            RemoveImageAndQueueRedownload(ImageEntityType.AniDB_Cover, anime.AnimeID, anime);
            if (++count % 10 == 0)
            {
                Logger.LogInformation(ReQueueingForDownload, count, animeList.Count);
                UpdateProgress($"{Resources.Command_ValidateAllImages_AniDBPosters} - {count}/{animeList.Count}");
            }
        }

        if (_settings.AniDb.DownloadCharacters)
        {
            count = 0;
            UpdateProgress(Resources.Command_ValidateAllImages_AniDBCharacters);
            Logger.LogInformation(ScanForType, "AniDB characters");
            var characters = RepoFactory.AniDB_Character.GetAll()
                .Where(character => !Misc.IsImageValid(character.GetPosterPath()))
                .ToList();

            Logger.LogInformation(FoundCorruptedOfType, characters.Count, characters.Count == 1 ? "AniDB character" : "AniDB characters");
            foreach (var character in characters)
            {
                RemoveImageAndQueueRedownload(ImageEntityType.AniDB_Character, character.AniDB_CharacterID, character);
                if (++count % 10 == 0)
                {
                    Logger.LogInformation(ReQueueingForDownload, count, characters.Count);
                    UpdateProgress($"{Resources.Command_ValidateAllImages_AniDBCharacters} - {count}/{characters.Count}");
                }
            }
        }

        if (_settings.AniDb.DownloadCreators)
        {
            count = 0;
            UpdateProgress(Resources.Command_ValidateAllImages_AniDBSeiyuus);
            Logger.LogInformation(ScanForType, "AniDB voice actors");
            var staff = RepoFactory.AniDB_Seiyuu.GetAll()
                .Where(va => !Misc.IsImageValid(va.GetPosterPath()))
                .ToList();

            Logger.LogInformation(FoundCorruptedOfType, staff.Count, staff.Count == 1 ? "AniDB voice actor" : "AniDB voice actors");
            foreach (var fanart in staff)
            {
                RemoveImageAndQueueRedownload(ImageEntityType.AniDB_Creator, fanart.SeiyuuID, fanart);
                if (++count % 10 == 0)
                {
                    Logger.LogInformation(ReQueueingForDownload, count,
                        staff.Count);
                    UpdateProgress($"{Resources.Command_ValidateAllImages_AniDBSeiyuus} - {count}/{staff.Count}");
                }
            }
        }
    }

    private void RemoveImageAndQueueRedownload(ImageEntityType entityTypeEnum, int entityID, object entity)
    {
        var fullPath = entity switch
        {
            AniDB_Character character => character.GetPosterPath(),
            AniDB_Seiyuu creator => creator.GetPosterPath(),
            MovieDB_Fanart image => image.GetFullImagePath(),
            MovieDB_Poster image => image.GetFullImagePath(),
            SVR_AniDB_Anime anime => anime.PosterPath,
            TvDB_Episode episode => episode.GetFullImagePath(),
            TvDB_ImageFanart image => image.GetFullImagePath(),
            TvDB_ImagePoster image => image.GetFullImagePath(),
            TvDB_ImageWideBanner image => image.GetFullImagePath(),
            _ => string.Empty,
        };

        if (string.IsNullOrEmpty(fullPath))
            return;

        Logger.LogTrace(CorruptImageFound, fullPath);
        try
        {
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (Exception e)
        {
            Logger.LogError("Unable to delete {FullImagePath} - {Message}", fullPath, e.Message);
        }

        _commandFactory.CreateAndSave<CommandRequest_DownloadImage>(
            c =>
            {
                c.EntityID = entityID;
                c.EntityTypeEnum = entityTypeEnum;
                c.ForceDownload = true;
            }
        );
    }

    private void UpdateProgress(string progressText)
    {
        if (Processor == null)
            return;

        var desc = PrettyDescription;
        desc.extraParams = new[] { progressText };
        Processor.QueueState = desc;
    }

    public override void GenerateCommandID()
    {
        CommandID = "CommandRequest_ValidateAllImages";
    }

    protected override bool Load()
    {

        return true;
    }

    public CommandRequest_ValidateAllImages(ILoggerFactory loggerFactory, ICommandRequestFactory commandFactory, ISettingsProvider settingsProvider) :
        base(loggerFactory)
    {
        _commandFactory = commandFactory;
        _settings = settingsProvider.GetSettings();
    }

    protected CommandRequest_ValidateAllImages()
    {
    }
}
