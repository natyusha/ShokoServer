﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Extensions;
using Shoko.Server.Plex;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server;

[EmitEmptyEnumerableInsteadOfNull]
[ApiController]
[Route("/v1")]
[ApiExplorerSettings(IgnoreApi = true)]
public partial class ShokoServiceImplementation : Controller, IShokoServer
{
    //TODO Split this file into subfiles with partial class, Move #region functionality from the interface to those subfiles

    private static Logger logger = LogManager.GetCurrentClassLogger();
    private readonly TvDBApiHelper _tvdbHelper;
    private readonly TraktTVHelper _traktHelper;
    private readonly MovieDBHelper _movieDBHelper;
    private readonly ICommandRequestFactory _commandFactory;
    private readonly ISettingsProvider _settingsProvider;

    public ShokoServiceImplementation(TvDBApiHelper tvdbHelper, TraktTVHelper traktHelper, MovieDBHelper movieDBHelper,
        ICommandRequestFactory commandFactory, ISettingsProvider settingsProvider)
    {
        _tvdbHelper = tvdbHelper;
        _traktHelper = traktHelper;
        _movieDBHelper = movieDBHelper;
        _commandFactory = commandFactory;
        _settingsProvider = settingsProvider;
    }

    #region Bookmarks

    [HttpGet("Bookmark")]
    public List<CL_BookmarkedAnime> GetAllBookmarkedAnime()
    {
        var baList = new List<CL_BookmarkedAnime>();
        try
        {
            return RepoFactory.ShokoSeries_Bookmark.GetAll().Select(a => a.ToClient()).ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return baList;
    }

    [HttpPost("Bookmark")]
    public CL_Response<CL_BookmarkedAnime> SaveBookmarkedAnime(CL_BookmarkedAnime contract)
    {
        var contractRet = new CL_Response<CL_BookmarkedAnime> { ErrorMessage = string.Empty };
        try
        {
            BookmarkedAnime ba = null;
            if (contract.BookmarkedAnimeID != 0)
            {
                ba = RepoFactory.ShokoSeries_Bookmark.GetByID(contract.BookmarkedAnimeID);
                if (ba == null)
                {
                    contractRet.ErrorMessage = "Could not find existing Bookmark with ID: " +
                                               contract.BookmarkedAnimeID;
                    return contractRet;
                }
            }
            else
            {
                // if a new record, check if it is allowed
                var baTemp = RepoFactory.ShokoSeries_Bookmark.GetByAnimeID(contract.AnimeID);
                if (baTemp != null)
                {
                    contractRet.ErrorMessage = "A bookmark with the AnimeID already exists: " +
                                               contract.AnimeID;
                    return contractRet;
                }

                ba = new BookmarkedAnime();
            }

            ba.AnimeID = contract.AnimeID;
            ba.Priority = contract.Priority;
            ba.Notes = contract.Notes;
            ba.Downloading = contract.Downloading;

            RepoFactory.ShokoSeries_Bookmark.Save(ba);

            contractRet.Result = ba.ToClient();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            contractRet.ErrorMessage = ex.Message;
            return contractRet;
        }

        return contractRet;
    }

    [HttpDelete("Bookmark/{bookmarkedAnimeID}")]
    public string DeleteBookmarkedAnime(int bookmarkedAnimeID)
    {
        try
        {
            var ba = RepoFactory.ShokoSeries_Bookmark.GetByID(bookmarkedAnimeID);
            if (ba == null)
            {
                return "Bookmarked not found";
            }

            RepoFactory.ShokoSeries_Bookmark.Delete(bookmarkedAnimeID);
            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpGet("Bookmark/{bookmarkedAnimeID}")]
    public CL_BookmarkedAnime GetBookmarkedAnime(int bookmarkedAnimeID)
    {
        try
        {
            return RepoFactory.ShokoSeries_Bookmark.GetByID(bookmarkedAnimeID).ToClient();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return null;
        }
    }

    #endregion

    #region Status and Changes

    [HttpGet("Changes/{date}/{userID}")]
    public CL_MainChanges GetAllChanges(DateTime date, int userID)
    {
        var c = new CL_MainChanges();
        try
        {
            var changes = ChangeTracker<int>.GetChainedChanges(
                new List<ChangeTracker<int>>
                {
                    RepoFactory.Shoko_Group_Filter.GetChangeTracker(),
                    RepoFactory.Shoko_Group.GetChangeTracker(),
                    RepoFactory.Shoko_Group_User.GetChangeTracker(userID),
                    RepoFactory.Shoko_Series.GetChangeTracker(),
                    RepoFactory.Shoko_Series_User.GetChangeTracker(userID)
                }, date);
            c.Filters = new CL_Changes<CL_GroupFilter>
            {
                ChangedItems = changes[0]
                    .ChangedItems.Select(a => RepoFactory.Shoko_Group_Filter.GetByID(a)?.ToClient())
                    .Where(a => a != null)
                    .ToList(),
                RemovedItems = changes[0].RemovedItems.ToList(),
                LastChange = changes[0].LastChange
            };

            //Add Group Filter that one of his child changed.
            bool end;
            do
            {
                end = true;
                foreach (var ag in c.Filters.ChangedItems
                             .Where(a => a.ParentGroupFilterID.HasValue && a.ParentGroupFilterID.Value != 0)
                             .ToList())
                {
                    if (!c.Filters.ChangedItems.Any(a => a.GroupFilterID == ag.ParentGroupFilterID.Value))
                    {
                        end = false;
                        var cag = RepoFactory.Shoko_Group_Filter.GetByID(ag.ParentGroupFilterID.Value)?
                            .ToClient();
                        if (cag != null)
                        {
                            c.Filters.ChangedItems.Add(cag);
                        }
                    }
                }
            } while (!end);

            c.Groups = new CL_Changes<CL_AnimeGroup_User>();
            changes[1].ChangedItems.UnionWith(changes[2].ChangedItems);
            changes[1].ChangedItems.UnionWith(changes[2].RemovedItems);
            if (changes[2].LastChange > changes[1].LastChange)
            {
                changes[1].LastChange = changes[2].LastChange;
            }

            c.Groups.ChangedItems = changes[1]
                .ChangedItems.Select(a => RepoFactory.Shoko_Group.GetByID(a))
                .Where(a => a != null)
                .Select(a => a.GetUserContract(userID))
                .ToList();


            c.Groups.RemovedItems = changes[1].RemovedItems.ToList();
            c.Groups.LastChange = changes[1].LastChange;
            c.Series = new CL_Changes<CL_AnimeSeries_User>();
            changes[3].ChangedItems.UnionWith(changes[4].ChangedItems);
            changes[3].ChangedItems.UnionWith(changes[4].RemovedItems);
            if (changes[4].LastChange > changes[3].LastChange)
            {
                changes[3].LastChange = changes[4].LastChange;
            }

            c.Series.ChangedItems = changes[3]
                .ChangedItems.Select(a => RepoFactory.Shoko_Series.GetByID(a))
                .Where(a => a != null)
                .Select(a => a.GetUserContract(userID))
                .ToList();
            c.Series.RemovedItems = changes[3].RemovedItems.ToList();
            c.Series.LastChange = changes[3].LastChange;
            c.LastChange = c.Filters.LastChange;
            if (c.Groups.LastChange > c.LastChange)
            {
                c.LastChange = c.Groups.LastChange;
            }

            if (c.Series.LastChange > c.LastChange)
            {
                c.LastChange = c.Series.LastChange;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return c;
    }

    [HttpGet("GroupFilter/Changes/{date}")]
    public CL_Changes<CL_GroupFilter> GetGroupFilterChanges(DateTime date)
    {
        var c = new CL_Changes<CL_GroupFilter>();
        try
        {
            var changes = RepoFactory.Shoko_Group_Filter.GetChangeTracker().GetChanges(date);
            c.ChangedItems = changes.ChangedItems.Select(a => RepoFactory.Shoko_Group_Filter.GetByID(a).ToClient())
                .Where(a => a != null)
                .ToList();
            c.RemovedItems = changes.RemovedItems.ToList();
            c.LastChange = changes.LastChange;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return c;
    }

    [DatabaseBlockedExempt]
    [HttpGet("Server")]
    public CL_ServerStatus GetServerStatus()
    {
        var contract = new CL_ServerStatus();

        try
        {
            contract.HashQueueCount = ShokoService.CmdProcessorHasher.QueueCount;
            contract.HashQueueState =
                ShokoService.CmdProcessorHasher.QueueState.formatMessage(); //Deprecated since 3.6.0.0
            contract.HashQueueStateId = (int)ShokoService.CmdProcessorHasher.QueueState.queueState;
            contract.HashQueueStateParams = ShokoService.CmdProcessorHasher.QueueState.extraParams;

            contract.GeneralQueueCount = ShokoService.CmdProcessorGeneral.QueueCount;
            contract.GeneralQueueState =
                ShokoService.CmdProcessorGeneral.QueueState.formatMessage(); //Deprecated since 3.6.0.0
            contract.GeneralQueueStateId = (int)ShokoService.CmdProcessorGeneral.QueueState.queueState;
            contract.GeneralQueueStateParams = ShokoService.CmdProcessorGeneral.QueueState.extraParams;

            contract.ImagesQueueCount = ShokoService.CmdProcessorImages.QueueCount;
            contract.ImagesQueueState =
                ShokoService.CmdProcessorImages.QueueState.formatMessage(); //Deprecated since 3.6.0.0
            contract.ImagesQueueStateId = (int)ShokoService.CmdProcessorImages.QueueState.queueState;
            contract.ImagesQueueStateParams = ShokoService.CmdProcessorImages.QueueState.extraParams;

            var udp = HttpContext.RequestServices.GetRequiredService<IUDPConnectionHandler>();
            var http = HttpContext.RequestServices.GetRequiredService<IHttpConnectionHandler>();
            if (http.IsBanned)
            {
                contract.IsBanned = true;
                contract.BanReason = http.BanTime?.ToString();
                contract.BanOrigin = @"HTTP";
            }
            else if (udp.IsBanned)
            {
                contract.IsBanned = true;
                contract.BanReason = udp.BanTime?.ToString();
                contract.BanOrigin = @"UDP";
            }
            else
            {
                contract.IsBanned = false;
                contract.BanReason = string.Empty;
                contract.BanOrigin = string.Empty;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return contract;
    }

    [HttpGet("Server/Versions")]
    public CL_AppVersions GetAppVersions()
    {
        try
        {
            //TODO WHEN WE HAVE A STABLE VERSION REPO, WE NEED TO CODE THE RETRIEVAL HERE.
            return new CL_AppVersions();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return null;
    }

    #endregion

    [HttpGet("Years")]
    public List<string> GetAllYears()
    {
        var grps =
            RepoFactory.Shoko_Series.GetAll().Select(a => a.Contract).Where(a => a != null).ToList();
        var allyears = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ser in grps)
        {
            var endyear = ser.AniDBAnime?.AniDBAnime?.EndYear ?? 0;
            var startyear = ser.AniDBAnime?.AniDBAnime?.BeginYear ?? 0;
            if (startyear == 0)
            {
                continue;
            }

            if (endyear == 0)
            {
                endyear = DateTime.Today.Year;
            }

            if (startyear > endyear)
            {
                endyear = startyear;
            }

            if (startyear == endyear)
            {
                allyears.Add(startyear.ToString());
            }
            else
            {
                allyears.UnionWith(Enumerable.Range(startyear,
                        endyear - startyear + 1)
                    .Select(a => a.ToString()));
            }
        }

        return allyears.OrderBy(a => a).ToList();
    }

    [HttpGet("Seasons")]
    public List<string> GetAllSeasons()
    {
        var grps =
            RepoFactory.Shoko_Series.GetAll().Select(a => a.Contract).Where(a => a != null).ToList();
        var allseasons = new SortedSet<string>(new SeasonComparator());
        foreach (var ser in grps)
        {
            allseasons.UnionWith(ser.AniDBAnime.Stat_AllSeasons);
        }

        return allseasons.ToList();
    }

    [HttpGet("Tags")]
    public List<string> GetAllTagNames()
    {
        var allTagNames = new List<string>();

        try
        {
            var start = DateTime.Now;

            foreach (var tag in RepoFactory.AniDB_Tag.GetAll())
            {
                allTagNames.Add(tag.TagName);
            }

            allTagNames.Sort();


            var ts = DateTime.Now - start;
            logger.Info("GetAllTagNames  in {0} ms", ts.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return allTagNames;
    }

    [HttpPost("CloudAccount/Directory")]
    public List<string> DirectoriesFromImportFolderPath([FromForm] string path)
    {
        if (path == null)
        {
            return new List<string>();
        }

        try
        {
            return !Directory.Exists(path)
                ? new List<string>()
                : new DirectoryInfo(path).EnumerateDirectories().Select(a => a.FullName).OrderByNatural(a => a)
                    .ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return new List<string>();
    }

    #region Settings

    [HttpPost("Server/Settings")]
    public CL_Response SaveServerSettings(CL_ServerSettings contractIn)
    {
        var contract = new CL_Response { ErrorMessage = string.Empty };
        try
        {
            var settings = _settingsProvider.GetSettings();
            // validate the settings
            var anidbSettingsChanged = false;
            if (ushort.TryParse(contractIn.AniDB_ClientPort, out var newAniDB_ClientPort) &&
                newAniDB_ClientPort != settings.AniDb.ClientPort)
            {
                anidbSettingsChanged = true;
                contract.ErrorMessage += "AniDB Client Port must be numeric and greater than 0" +
                                         Environment.NewLine;
            }

            if (ushort.TryParse(contractIn.AniDB_ServerPort, out var newAniDB_ServerPort) &&
                newAniDB_ServerPort != settings.AniDb.ServerPort)
            {
                anidbSettingsChanged = true;
                contract.ErrorMessage += "AniDB Server Port must be numeric and greater than 0" +
                                         Environment.NewLine;
            }

            if (contractIn.AniDB_Username != settings.AniDb.Username)
            {
                anidbSettingsChanged = true;
                if (string.IsNullOrEmpty(contractIn.AniDB_Username))
                {
                    contract.ErrorMessage += "AniDB User Name must have a value" + Environment.NewLine;
                }
            }

            if (contractIn.AniDB_Password != settings.AniDb.Password)
            {
                anidbSettingsChanged = true;
                if (string.IsNullOrEmpty(contractIn.AniDB_Password))
                {
                    contract.ErrorMessage += "AniDB Password must have a value" + Environment.NewLine;
                }
            }

            if (contractIn.AniDB_ServerAddress != settings.AniDb.ServerAddress)
            {
                anidbSettingsChanged = true;
                if (string.IsNullOrEmpty(contractIn.AniDB_ServerAddress))
                {
                    contract.ErrorMessage += "AniDB Server Address must have a value" + Environment.NewLine;
                }
            }

            if (!ushort.TryParse(contractIn.AniDB_AVDumpClientPort, out var newAniDB_AVDumpClientPort))
            {
                contract.ErrorMessage += "AniDB AVDump port must be a valid port" + Environment.NewLine;
            }


            if (contract.ErrorMessage.Length > 0)
            {
                return contract;
            }

            settings.AniDb.ClientPort = newAniDB_ClientPort;
            settings.AniDb.Password = contractIn.AniDB_Password;
            settings.AniDb.ServerAddress = contractIn.AniDB_ServerAddress;
            settings.AniDb.ServerPort = newAniDB_ServerPort;
            settings.AniDb.Username = contractIn.AniDB_Username;
            settings.AniDb.AVDumpClientPort = newAniDB_AVDumpClientPort;
            settings.AniDb.AVDumpKey = contractIn.AniDB_AVDumpKey;

            settings.AniDb.DownloadRelatedAnime = contractIn.AniDB_DownloadRelatedAnime;
            settings.AniDb.DownloadReleaseGroups = contractIn.AniDB_DownloadReleaseGroups;
            settings.AniDb.DownloadReviews = contractIn.AniDB_DownloadReviews;
            settings.AniDb.DownloadSimilarAnime = contractIn.AniDB_DownloadSimilarAnime;

            settings.AniDb.MyList_AddFiles = contractIn.AniDB_MyList_AddFiles;
            settings.AniDb.MyList_ReadUnwatched = contractIn.AniDB_MyList_ReadUnwatched;
            settings.AniDb.MyList_ReadWatched = contractIn.AniDB_MyList_ReadWatched;
            settings.AniDb.MyList_SetUnwatched = contractIn.AniDB_MyList_SetUnwatched;
            settings.AniDb.MyList_SetWatched = contractIn.AniDB_MyList_SetWatched;
            settings.AniDb.MyList_StorageState = (AniDBFile_State)contractIn.AniDB_MyList_StorageState;
            settings.AniDb.MyList_DeleteType = (AniDBFileDeleteType)contractIn.AniDB_MyList_DeleteType;
            //settings.AniDb.MaxRelationDepth = contractIn.AniDB_MaxRelationDepth;

            settings.AniDb.MyList_UpdateFrequency =
                (ScheduledUpdateFrequency)contractIn.AniDB_MyList_UpdateFrequency;
            settings.AniDb.Calendar_UpdateFrequency =
                (ScheduledUpdateFrequency)contractIn.AniDB_Calendar_UpdateFrequency;
            settings.AniDb.Anime_UpdateFrequency =
                (ScheduledUpdateFrequency)contractIn.AniDB_Anime_UpdateFrequency;
            settings.AniDb.MyListStats_UpdateFrequency =
                (ScheduledUpdateFrequency)contractIn.AniDB_MyListStats_UpdateFrequency;
            settings.AniDb.File_UpdateFrequency =
                (ScheduledUpdateFrequency)contractIn.AniDB_File_UpdateFrequency;

            settings.AniDb.DownloadCharacters = contractIn.AniDB_DownloadCharacters;
            settings.AniDb.DownloadCreators = contractIn.AniDB_DownloadCreators;

            // Web Cache
            settings.WebCache.Address = contractIn.WebCache_Address;
            settings.WebCache.XRefFileEpisode_Get = contractIn.WebCache_XRefFileEpisode_Get;
            settings.WebCache.XRefFileEpisode_Send = contractIn.WebCache_XRefFileEpisode_Send;
            settings.WebCache.TvDB_Get = contractIn.WebCache_TvDB_Get;
            settings.WebCache.TvDB_Send = contractIn.WebCache_TvDB_Send;
            settings.WebCache.Trakt_Get = contractIn.WebCache_Trakt_Get;
            settings.WebCache.Trakt_Send = contractIn.WebCache_Trakt_Send;

            // TvDB
            settings.TvDB.AutoLink = contractIn.TvDB_AutoLink;
            settings.TvDB.AutoFanart = contractIn.TvDB_AutoFanart;
            settings.TvDB.AutoFanartAmount = contractIn.TvDB_AutoFanartAmount;
            settings.TvDB.AutoPosters = contractIn.TvDB_AutoPosters;
            settings.TvDB.AutoPostersAmount = contractIn.TvDB_AutoPostersAmount;
            settings.TvDB.AutoWideBanners = contractIn.TvDB_AutoWideBanners;
            settings.TvDB.AutoWideBannersAmount = contractIn.TvDB_AutoWideBannersAmount;
            settings.TvDB.UpdateFrequency = (ScheduledUpdateFrequency)contractIn.TvDB_UpdateFrequency;
            settings.TvDB.Language = contractIn.TvDB_Language;

            // MovieDB
            settings.TMDB.AutoFanart = contractIn.MovieDB_AutoFanart;
            settings.TMDB.AutoFanartAmount = contractIn.MovieDB_AutoFanartAmount;
            settings.TMDB.AutoPosters = contractIn.MovieDB_AutoPosters;
            settings.TMDB.AutoPostersAmount = contractIn.MovieDB_AutoPostersAmount;

            // Import settings
            settings.Import.VideoExtensions = contractIn.VideoExtensions.Split(',').ToList();
            settings.Import.UseExistingFileWatchedStatus =
                contractIn.Import_UseExistingFileWatchedStatus;
            settings.AutoGroupSeries = contractIn.AutoGroupSeries;
            settings.AutoGroupSeriesUseScoreAlgorithm = contractIn.AutoGroupSeriesUseScoreAlgorithm;
            settings.AutoGroupSeriesRelationExclusions = contractIn.AutoGroupSeriesRelationExclusions;
            settings.FileQualityFilterEnabled = contractIn.FileQualityFilterEnabled;
            if (!string.IsNullOrEmpty(contractIn.FileQualityFilterPreferences))
            {
                settings.FileQualityPreferences =
                    SettingsProvider.Deserialize<FileQualityPreferences>(contractIn.FileQualityFilterPreferences);
            }

            settings.Import.RunOnStart = contractIn.RunImportOnStart;
            settings.Import.ScanDropFoldersOnStart = contractIn.ScanDropFoldersOnStart;
            settings.Import.Hasher.CRC = contractIn.Hash_CRC32;
            settings.Import.Hasher.MD5 = contractIn.Hash_MD5;
            settings.Import.Hasher.SHA1 = contractIn.Hash_SHA1;
            settings.Import.RenameOnImport = contractIn.Import_RenameOnImport;
            settings.Import.MoveOnImport = contractIn.Import_MoveOnImport;
            settings.Import.SkipDiskSpaceChecks = contractIn.SkipDiskSpaceChecks;

            // Language
            settings.LanguagePreference = contractIn.LanguagePreference.Split(',').ToList();
            settings.LanguageUseSynonyms = contractIn.LanguageUseSynonyms;
            settings.EpisodeTitleSource = (DataSource)contractIn.EpisodeTitleSource;
            settings.SeriesDescriptionSource = (DataSource)contractIn.SeriesDescriptionSource;
            settings.SeriesNameSource = (DataSource)contractIn.SeriesNameSource;

            // Trakt
            settings.TraktTv.Enabled = contractIn.Trakt_IsEnabled;
            settings.TraktTv.AuthToken = contractIn.Trakt_AuthToken;
            settings.TraktTv.RefreshToken = contractIn.Trakt_RefreshToken;
            settings.TraktTv.TokenExpirationDate = contractIn.Trakt_TokenExpirationDate;
            settings.TraktTv.UpdateFrequency =
                (ScheduledUpdateFrequency)contractIn.Trakt_UpdateFrequency;
            settings.TraktTv.SyncFrequency = (ScheduledUpdateFrequency)contractIn.Trakt_SyncFrequency;

            //Plex
            settings.Plex.Server = contractIn.Plex_ServerHost;
            settings.Plex.Libraries = contractIn.Plex_Sections.Length > 0
                ? contractIn.Plex_Sections.Split(',').Select(int.Parse).ToList()
                : new List<int>();

            // SAVE!
            _settingsProvider.SaveSettings();

            if (anidbSettingsChanged)
            {
                var handler = HttpContext.RequestServices.GetRequiredService<IUDPConnectionHandler>();

                handler.ForceLogout();
                handler.CloseConnections();

                Thread.Sleep(1000);
                handler.Init(settings.AniDb.Username, settings.AniDb.Password,
                    settings.AniDb.ServerAddress,
                    settings.AniDb.ServerPort, settings.AniDb.ClientPort);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Save server settings exception:\n " + ex);
            contract.ErrorMessage = ex.Message;
        }

        return contract;
    }

    [HttpGet("Server/Settings")]
    public CL_ServerSettings GetServerSettings()
    {
        var contract = new CL_ServerSettings();

        try
        {
            var settings = _settingsProvider.GetSettings();
            return settings.ToContract();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return contract;
    }

    #endregion

    #region Actions

    [HttpPost("Folder/Import")]
    public void RunImport()
    {
        ShokoServer.RunImport();
    }

    [HttpPost("File/Hashes/Sync")]
    public void SyncHashes()
    {
    }

    [HttpPost("Folder/Scan")]
    public void ScanDropFolders()
    {
        Importer.RunImport_DropFolders();
    }

    [HttpPost("Folder/Scan/{importFolderID}")]
    public void ScanFolder(int importFolderID)
    {
        ShokoServer.ScanFolder(importFolderID);
    }

    [HttpPost("Folder/RemoveMissing")]
    public void RemoveMissingFiles()
    {
        ShokoServer.RemoveMissingFiles();
    }

    [HttpPost("Folder/RefreshMediaInfo")]
    public void RefreshAllMediaInfo()
    {
        ShokoServer.RefreshAllMediaInfo();
    }

    [HttpPost("AniDB/MyList/Sync")]
    public void SyncMyList()
    {
        ShokoServer.SyncMyList();
    }

    [HttpPost("AniDB/Vote/Sync")]
    public void SyncVotes()
    {
        var cmdVotes = _commandFactory.Create<CommandRequest_SyncMyVotes>();
        cmdVotes.Save();
    }

    #endregion

    #region Queue Actions

    [HttpPost("CommandQueue/Hasher/{paused}")]
    public void SetCommandProcessorHasherPaused(bool paused)
    {
        ShokoService.CmdProcessorHasher.Paused = paused;
    }

    [HttpPost("CommandQueue/General/{paused}")]
    public void SetCommandProcessorGeneralPaused(bool paused)
    {
        ShokoService.CmdProcessorGeneral.Paused = paused;
    }

    [HttpPost("CommandQueue/Images/{paused}")]
    public void SetCommandProcessorImagesPaused(bool paused)
    {
        ShokoService.CmdProcessorImages.Paused = paused;
    }

    [HttpDelete("CommandQueue/Hasher")]
    public void ClearHasherQueue()
    {
        try
        {
            ShokoService.CmdProcessorHasher.Stop();

            RepoFactory.CommandRequest.ClearHasherQueue();
            ShokoService.CmdProcessorHasher.NotifyOfNewCommand();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }
    }

    [HttpDelete("CommandQueue/Images")]
    public void ClearImagesQueue()
    {
        try
        {
            ShokoService.CmdProcessorImages.Stop();

            RepoFactory.CommandRequest.ClearImageQueue();
            ShokoService.CmdProcessorImages.NotifyOfNewCommand();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }
    }

    [HttpDelete("CommandQueue/General")]
    public void ClearGeneralQueue()
    {
        try
        {
            ShokoService.CmdProcessorGeneral.Stop();

            RepoFactory.CommandRequest.ClearGeneralQueue();
            ShokoService.CmdProcessorGeneral.NotifyOfNewCommand();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }
    }

    #endregion

    [HttpPost("AniDB/Status")]
    public string TestAniDBConnection()
    {
        var log = string.Empty;
        try
        {
            var handler = HttpContext.RequestServices.GetRequiredService<IUDPConnectionHandler>();
            log += "Disposing..." + Environment.NewLine;
            handler.ForceLogout();
            handler.CloseConnections();

            log += "Init..." + Environment.NewLine;
            var settings = _settingsProvider.GetSettings();
            handler.Init(settings.AniDb.Username, settings.AniDb.Password,
                settings.AniDb.ServerAddress,
                settings.AniDb.ServerPort, settings.AniDb.ClientPort);

            log += "Login..." + Environment.NewLine;
            if (handler.Login())
            {
                log += "Login Success!" + Environment.NewLine;
                log += "Logout..." + Environment.NewLine;
                handler.ForceLogout();
                log += "Logged out" + Environment.NewLine;
            }
            else
            {
                log += "Login FAILED!" + Environment.NewLine;
            }

            return log;
        }
        catch (Exception ex)
        {
            log += ex.Message + Environment.NewLine;
        }

        return log;
    }

    [HttpGet("MediaInfo/Quality")]
    public List<string> GetAllUniqueVideoQuality()
    {
        try
        {
            return RepoFactory.AniDB_File.GetAll().Select(a => a.File_Source).Distinct().OrderBy(a => a).ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return new List<string>();
        }
    }

    [HttpGet("MediaInfo/AudioLanguages")]
    public List<string> GetAllUniqueAudioLanguages()
    {
        try
        {
            return RepoFactory.CR_AniDB_File_Languages.GetAll().Select(a => a.LanguageName).Distinct()
                .OrderBy(a => a).ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return new List<string>();
        }
    }

    [HttpGet("MediaInfo/SubtitleLanguages")]
    public List<string> GetAllUniqueSubtitleLanguages()
    {
        try
        {
            return RepoFactory.CR_AniDB_File_Subtitles.GetAll().Select(a => a.LanguageName).Distinct()
                .OrderBy(a => a).ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return new List<string>();
        }
    }

    #region Plex

    [HttpGet("User/Plex/LoginUrl/{userID}")]
    public string LoginUrl(int userID)
    {
        var user = RepoFactory.Shoko_User.GetByID(userID);
        return PlexHelper.GetForUser(user).LoginUrl;
    }

    [HttpGet("User/Plex/Authenticated/{userID}")]
    public bool IsPlexAuthenticated(int userID)
    {
        var user = RepoFactory.Shoko_User.GetByID(userID);
        return PlexHelper.GetForUser(user).IsAuthenticated;
    }

    [HttpGet("User/Plex/Remove/{userID}")]
    public bool RemovePlexAuth(int userID)
    {
        var user = RepoFactory.Shoko_User.GetByID(userID);
        PlexHelper.GetForUser(user).InvalidateToken();
        return true;
    }

    #endregion

    [HttpPost("Image/Enable/{enabled}/{imageID}/{imageType}")]
    public string EnableDisableImage(bool enabled, int imageID, int imageType)
    {
        try
        {
            var imgType = (ImageEntityType)imageType;

            switch (imgType)
            {
                case ImageEntityType.AniDB_Cover:
                    var anime = RepoFactory.AniDB_Anime.GetByAnidbAnimeId(imageID);
                    if (anime == null)
                    {
                        return "Could not find anime";
                    }

                    anime.ImageEnabled = enabled ? 1 : 0;
                    RepoFactory.AniDB_Anime.Save(anime);
                    break;

                case ImageEntityType.TvDB_Banner:
                    var banner = RepoFactory.TvDB_Banner.GetByID(imageID);
                    if (banner == null)
                    {
                        return "Could not find image";
                    }

                    banner.Enabled = enabled ? 1 : 0;
                    RepoFactory.TvDB_Banner.Save(banner);
                    break;

                case ImageEntityType.TvDB_Cover:
                    var poster = RepoFactory.TvDB_Poster.GetByID(imageID);
                    if (poster == null)
                    {
                        return "Could not find image";
                    }

                    poster.Enabled = enabled ? 1 : 0;
                    RepoFactory.TvDB_Poster.Save(poster);
                    break;

                case ImageEntityType.TvDB_FanArt:
                    var fanart = RepoFactory.TvDB_Fanart.GetByID(imageID);
                    if (fanart == null)
                    {
                        return "Could not find image";
                    }

                    fanart.Enabled = enabled ? 1 : 0;
                    RepoFactory.TvDB_Fanart.Save(fanart);
                    break;

                case ImageEntityType.TMDB_Poster:
                    var moviePoster = RepoFactory.TMDB_Movie_Poster.GetByID(imageID);
                    if (moviePoster == null)
                    {
                        return "Could not find image";
                    }

                    moviePoster.Enabled = enabled ? 1 : 0;
                    RepoFactory.TMDB_Movie_Poster.Save(moviePoster);
                    break;

                case ImageEntityType.TMDB_Fanart:
                    var movieFanart = RepoFactory.TMDB_Fanart.GetByID(imageID);
                    if (movieFanart == null)
                    {
                        return "Could not find image";
                    }

                    movieFanart.Enabled = enabled ? 1 : 0;
                    RepoFactory.TMDB_Fanart.Save(movieFanart);
                    break;
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpPost("Image/Default/{isDefault}/{animeID}/{imageID}/{imageType}/{imageSizeType}")]
    public string SetDefaultImage(bool isDefault, int animeID, int imageID, int imageType, int imageSizeType)
    {
        try
        {
            var imgType = (ImageEntityType)imageType;
            var sizeType = ImageSizeType.Poster;

            switch (imgType)
            {
                case ImageEntityType.AniDB_Cover:
                case ImageEntityType.TvDB_Cover:
                case ImageEntityType.TMDB_Poster:
                    sizeType = ImageSizeType.Poster;
                    break;

                case ImageEntityType.TvDB_Banner:
                    sizeType = ImageSizeType.WideBanner;
                    break;

                case ImageEntityType.TvDB_FanArt:
                case ImageEntityType.TMDB_Fanart:
                    sizeType = ImageSizeType.Fanart;
                    break;
            }

            if (!isDefault)
            {
                // this mean we are removing an image as deafult
                // which esssential means deleting the record

                var img =
                    RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(animeID, sizeType);
                if (img != null)
                {
                    RepoFactory.AniDB_Anime_DefaultImage.Delete(img.AniDB_Anime_DefaultImageID);
                }
            }
            else
            {
                // making the image the default for it's type (poster, fanart etc)
                var img =
                    RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(animeID, sizeType);
                if (img == null)
                {
                    img = new AniDB_Anime_DefaultImage();
                }

                img.AnimeID = animeID;
                img.ImageParentID = imageID;
                img.ImageParentType = (int)imgType;
                img.ImageType = (int)sizeType;
                RepoFactory.AniDB_Anime_DefaultImage.Save(img);
            }

            var series = RepoFactory.Shoko_Series.GetByAnidbAnimeId(animeID);
            RepoFactory.Shoko_Series.Save(series, false);

            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }
    }

    #region Calendar (Dashboard)

    [HttpGet("AniDB/Anime/Calendar/{userID}/{numberOfDays}")]
    public List<CL_AniDB_Anime> GetMiniCalendar(int userID, int numberOfDays)
    {
        // get all the series
        var animeList = new List<CL_AniDB_Anime>();

        try
        {
            var user = RepoFactory.Shoko_User.GetByID(userID);
            if (user == null)
            {
                return animeList;
            }

            var animes = RepoFactory.AniDB_Anime.GetForDate(
                DateTime.Today.AddDays(0 - numberOfDays),
                DateTime.Today.AddDays(numberOfDays));
            foreach (var anime in animes)
            {
                if (anime?.Contract?.AniDBAnime == null)
                {
                    continue;
                }

                if (!user.GetHideCategories().FindInEnumerable(anime.Contract.AniDBAnime.GetAllTags()))
                {
                    animeList.Add(anime.Contract.AniDBAnime);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return animeList;
    }

    [HttpGet("AniDB/Anime/ForMonth/{userID}/{month}/{year}")]
    public List<CL_AniDB_Anime> GetAnimeForMonth(int userID, int month, int year)
    {
        // get all the series
        var animeList = new List<CL_AniDB_Anime>();

        try
        {
            var user = RepoFactory.Shoko_User.GetByID(userID);
            if (user == null)
            {
                return animeList;
            }

            var startDate = new DateTime(year, month, 1, 0, 0, 0);
            var endDate = startDate.AddMonths(1);
            endDate = endDate.AddMinutes(-10);

            var animes = RepoFactory.AniDB_Anime.GetForDate(startDate, endDate);
            foreach (var anime in animes)
            {
                if (anime?.Contract?.AniDBAnime == null)
                {
                    continue;
                }

                if (!user.GetHideCategories().FindInEnumerable(anime.Contract.AniDBAnime.GetAllTags()))
                {
                    animeList.Add(anime.Contract.AniDBAnime);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return animeList;
    }

    [HttpPost("AniDB/Anime/Calendar/Update")]
    public string UpdateCalendarData()
    {
        try
        {
            Importer.CheckForCalendarUpdate(true);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return string.Empty;
    }

    /*public List<Contract_AniDBAnime> GetMiniCalendar(int numberOfDays)
    {
        AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
        JMMUserRepository repUsers = new JMMUserRepository();

        // get all the series
        List<Contract_AniDBAnime> animeList = new List<Contract_AniDBAnime>();

        try
        {

            List<AniDB_Anime> animes = repAnime.GetForDate(DateTime.Today.AddDays(0 - numberOfDays), DateTime.Today.AddDays(numberOfDays));
            foreach (AniDB_Anime anime in animes)
            {

                    animeList.Add(anime.ToContract());
            }

        }
        catch (Exception ex)
        {
            logger.Error( ex,ex.ToString());
        }
        return animeList;
    }*/

    #endregion

    /// <summary>
    /// Returns a list of recommendations based on the users votes
    /// </summary>
    /// <param name="maxResults"></param>
    /// <param name="userID"></param>
    /// <param name="recommendationType">1 = to watch, 2 = to download</param>
    [HttpGet("Recommendation/{maxResults}/{userID}/{recommendationType}")]
    public List<CL_Recommendation> GetRecommendations(int maxResults, int userID, int recommendationType)
    {
        var recs = new List<CL_Recommendation>();

        try
        {
            var juser = RepoFactory.Shoko_User.GetByID(userID);
            if (juser == null)
            {
                return recs;
            }

            // get all the anime the user has chosen to ignore
            var ignoreType = 1;
            switch (recommendationType)
            {
                case 1:
                    ignoreType = 1;
                    break;
                case 2:
                    ignoreType = 2;
                    break;
            }

            var ignored = RepoFactory.ShokoSeries_Ignore.GetByUserAndType(userID, ignoreType);
            var dictIgnored = new Dictionary<int, IgnoreAnime>();
            foreach (var ign in ignored)
            {
                dictIgnored[ign.AnimeID] = ign;
            }


            // find all the series which the user has rated
            var allVotes = RepoFactory.AniDB_Vote.GetAll()
                .OrderByDescending(a => a.VoteValue)
                .ToList();
            if (allVotes.Count == 0)
            {
                return recs;
            }


            var dictRecs = new Dictionary<int, CL_Recommendation>();

            var animeVotes = new List<AniDB_Vote>();
            foreach (var vote in allVotes)
            {
                if (vote.VoteType != (int)AniDBVoteType.Anime &&
                    vote.VoteType != (int)AniDBVoteType.AnimeTemp)
                {
                    continue;
                }

                if (dictIgnored.ContainsKey(vote.EntityID))
                {
                    continue;
                }

                // check if the user has this anime
                var anime = RepoFactory.AniDB_Anime.GetByAnidbAnimeId(vote.EntityID);
                if (anime == null)
                {
                    continue;
                }

                // get similar anime
                var simAnime = anime.GetSimilarAnime()
                    .OrderByDescending(a => a.GetApprovalPercentage())
                    .ToList();
                // sort by the highest approval

                foreach (var link in simAnime)
                {
                    if (dictIgnored.ContainsKey(link.SimilarAnimeID))
                    {
                        continue;
                    }

                    var animeLink = RepoFactory.AniDB_Anime.GetByAnimeID(link.SimilarAnimeID);
                    if (animeLink != null)
                    {
                        if (!juser.AllowedAnime(animeLink))
                        {
                            continue;
                        }
                    }

                    // don't recommend to watch anime that the user doesn't have
                    if (animeLink == null && recommendationType == 1)
                    {
                        continue;
                    }

                    // don't recommend to watch series that the user doesn't have
                    var ser = RepoFactory.Shoko_Series.GetByAnimeID(link.SimilarAnimeID);
                    if (ser == null && recommendationType == 1)
                    {
                        continue;
                    }


                    if (ser != null)
                    {
                        // don't recommend to watch series that the user has already started watching
                        ShokoSeries_User userRecord = ser.GetUserRecord(userID);
                        if (userRecord != null)
                        {
                            if (userRecord.WatchedEpisodeCount > 0 && recommendationType == 1)
                            {
                                continue;
                            }
                        }

                        // don't recommend to download anime that the user has files for
                        if (ser.LatestLocalEpisodeNumber > 0 && recommendationType == 2)
                        {
                            continue;
                        }
                    }

                    var rec = new CL_Recommendation
                    {
                        BasedOnAnimeID = anime.AnimeId, RecommendedAnimeID = link.SimilarAnimeID
                    };

                    // if we don't have the anime locally. lets assume the anime has a high rating
                    decimal animeRating = 850;
                    if (animeLink != null)
                    {
                        animeRating = animeLink.GetAniDBRating();
                    }

                    rec.Score =
                        CalculateRecommendationScore(vote.VoteValue, link.GetApprovalPercentage(), animeRating);
                    rec.BasedOnVoteValue = vote.VoteValue;
                    rec.RecommendedApproval = link.GetApprovalPercentage();

                    // check if we have added this recommendation before
                    // this might happen where animes are recommended based on different votes
                    // and could end up with different scores
                    if (dictRecs.ContainsKey(rec.RecommendedAnimeID))
                    {
                        if (rec.Score < dictRecs[rec.RecommendedAnimeID].Score)
                        {
                            continue;
                        }
                    }

                    rec.Recommended_AniDB_Anime = null;
                    if (animeLink != null)
                    {
                        rec.Recommended_AniDB_Anime = animeLink.Contract.AniDBAnime;
                    }

                    rec.BasedOn_AniDB_Anime = anime.Contract.AniDBAnime;

                    rec.Recommended_AnimeSeries = null;
                    if (ser != null)
                    {
                        rec.Recommended_AnimeSeries = ser.GetUserContract(userID);
                    }

                    var serBasedOn = RepoFactory.Shoko_Series.GetByAnidbAnimeId(anime.AnimeId);
                    if (serBasedOn == null)
                    {
                        continue;
                    }

                    rec.BasedOn_AnimeSeries = serBasedOn.GetUserContract(userID);

                    dictRecs[rec.RecommendedAnimeID] = rec;
                }
            }

            var tempRecs = new List<CL_Recommendation>();
            foreach (var rec in dictRecs.Values)
            {
                tempRecs.Add(rec);
            }

            // sort by the highest score

            var numRecs = 0;
            foreach (var rec in tempRecs.OrderByDescending(a => a.Score))
            {
                if (numRecs == maxResults)
                {
                    break;
                }

                recs.Add(rec);
                numRecs++;
            }

            if (recs.Count == 0)
            {
                return recs;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return recs;
    }

    private double CalculateRecommendationScore(int userVoteValue, double approvalPercentage, decimal animeRating)
    {
        double score = userVoteValue;

        score = score + approvalPercentage;

        if (approvalPercentage > 90)
        {
            score = score + 100;
        }

        if (approvalPercentage > 80)
        {
            score = score + 100;
        }

        if (approvalPercentage > 70)
        {
            score = score + 100;
        }

        if (approvalPercentage > 60)
        {
            score = score + 100;
        }

        if (approvalPercentage > 50)
        {
            score = score + 100;
        }

        if (animeRating > 900)
        {
            score = score + 100;
        }

        if (animeRating > 800)
        {
            score = score + 100;
        }

        if (animeRating > 700)
        {
            score = score + 100;
        }

        if (animeRating > 600)
        {
            score = score + 100;
        }

        if (animeRating > 500)
        {
            score = score + 100;
        }

        return score;
    }

    [HttpGet("AniDB/ReleaseGroup/{animeID}")]
    public List<CL_AniDB_GroupStatus> GetReleaseGroupsForAnime(int animeID)
    {
        var relGroups = new List<CL_AniDB_GroupStatus>();

        try
        {
            var series = RepoFactory.Shoko_Series.GetByAnidbAnimeId(animeID);
            if (series == null)
            {
                return relGroups;
            }

            // get a list of all the release groups the user is collecting
            //List<int> userReleaseGroups = new List<int>();
            var userReleaseGroups = new Dictionary<int, int>();
            foreach (var ep in series.GetEpisodes())
            {
                var vids = ep.GetVideoLocals();
                var hashes = vids.Where(a => !string.IsNullOrEmpty(a.Hash)).Select(a => a.Hash).ToList();
                foreach (var h in hashes)
                {
                    var vid = vids.First(a => a.Hash == h);
                    var anifile = vid.GetAniDBFile();
                    if (anifile != null)
                    {
                        if (!userReleaseGroups.ContainsKey(anifile.GroupID))
                        {
                            userReleaseGroups[anifile.GroupID] = 0;
                        }

                        userReleaseGroups[anifile.GroupID] = userReleaseGroups[anifile.GroupID] + 1;
                    }
                }
            }

            // get all the release groups for this series
            var grpStatuses = RepoFactory.AniDB_Anime_ReleaseGroup_Status.GetByAnimeID(animeID);
            foreach (var gs in grpStatuses)
            {
                var cl = gs.ToClient();
                if (userReleaseGroups.ContainsKey(gs.GroupID))
                {
                    cl.UserCollecting = true;
                    cl.FileCount = userReleaseGroups[gs.GroupID];
                }
                else
                {
                    cl.UserCollecting = false;
                    cl.FileCount = 0;
                }

                relGroups.Add(cl);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return relGroups;
    }

    [HttpGet("AniDB/Character/{animeID}")]
    public List<CL_AniDB_Character> GetCharactersForAnime(int animeID)
    {
        var chars = new List<CL_AniDB_Character>();

        try
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnidbAnimeId(animeID);
            return anime.GetCharactersContract();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return chars;
    }

    [HttpGet("AniDB/Character/FromSeiyuu/{seiyuuID}")]
    public List<CL_AniDB_Character> GetCharactersForSeiyuu(int seiyuuID)
    {
        var chars = new List<CL_AniDB_Character>();

        try
        {
            var seiyuu = RepoFactory.AniDB_Creator.GetByID(seiyuuID);
            if (seiyuu == null)
            {
                return chars;
            }

            var links = RepoFactory.AniDB_Character_Creator.GetBySeiyuuID(seiyuu.SeiyuuID);

            foreach (var chrSei in links)
            {
                var chr = RepoFactory.AniDB_Character.GetByID(chrSei.CharID);
                if (chr != null)
                {
                    var aniChars =
                        RepoFactory.AniDB_Anime_Character.GetByCharID(chr.CharID);
                    if (aniChars.Count > 0)
                    {
                        var anime = RepoFactory.AniDB_Anime.GetByAnidbAnimeId(aniChars[0].AnimeID);
                        if (anime != null)
                        {
                            var cl = chr.ToClient(aniChars[0].CharType);
                            chars.Add(cl);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return chars;
    }

    [HttpGet("AniDB/Seiyuu/{seiyuuID}")]
    public AniDB_Seiyuu GetAniDBSeiyuu(int seiyuuID)
    {
        try
        {
            return RepoFactory.AniDB_Creator.GetByID(seiyuuID);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return null;
    }
}
