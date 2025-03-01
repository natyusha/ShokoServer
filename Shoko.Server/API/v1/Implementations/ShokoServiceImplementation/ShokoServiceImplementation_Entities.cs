using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Extensions;
using Shoko.Server.Filters;
using Shoko.Server.Filters.Legacy;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Services;
using Shoko.Server.Tasks;
using Shoko.Server.Utilities;

namespace Shoko.Server;

public partial class ShokoServiceImplementation : IShokoServer
{
    #region Episodes and Files

    /// <summary>
    ///     Finds the previous episode for use int the next unwatched episode
    /// </summary>
    /// <param name="animeSeriesID"></param>
    /// <param name="userID"></param>
    /// <returns></returns>
    [HttpGet("Episode/PreviousEpisode/{animeSeriesID}/{userID}")]
    public CL_AnimeEpisode_User GetPreviousEpisodeForUnwatched(int animeSeriesID, int userID)
    {
        try
        {
            var nextEp = GetNextUnwatchedEpisode(animeSeriesID, userID);
            if (nextEp == null)
            {
                return null;
            }

            var epType = nextEp.EpisodeType;
            var epNum = nextEp.EpisodeNumber - 1;

            if (epNum <= 0)
            {
                return null;
            }

            var series = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
            if (series == null)
            {
                return null;
            }

            var anieps = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeTypeNumber(series.AniDB_ID,
                (EpisodeType)epType,
                epNum);
            if (anieps.Count == 0)
            {
                return null;
            }

            var ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(anieps[0].EpisodeID);
            return ep?.GetUserContract(userID);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return null;
        }
    }

    [HttpGet("Episode/NextForSeries/{animeSeriesID}/{userID}")]
    public CL_AnimeEpisode_User GetNextUnwatchedEpisode(int animeSeriesID, int userID)
    {
        try
        {
            var series = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
            if (series == null)
            {
                return null;
            }

            var episode = series.GetNextEpisode(userID);
            if (episode == null)
            {
                return null;
            }

            return episode.GetUserContract(userID);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return null;
        }
    }

    [HttpGet("Episode/Unwatched/{animeSeriesID}/{userID}")]
    public List<CL_AnimeEpisode_User> GetAllUnwatchedEpisodes(int animeSeriesID, int userID)
    {
        var ret = new List<CL_AnimeEpisode_User>();

        try
        {
            return
                RepoFactory.AnimeEpisode.GetBySeriesID(animeSeriesID)
                    .Where(a => a != null && !a.IsHidden)
                    .Select(a => a.GetUserContract(userID))
                    .Where(a => a != null)
                    .Where(a => a.WatchedCount == 0)
                    .OrderBy(a => a.EpisodeType)
                    .ThenBy(a => a.EpisodeNumber)
                    .ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ret;
        }
    }

    [HttpGet("Episode/NextForGroup/{animeGroupID}/{userID}")]
    public CL_AnimeEpisode_User GetNextUnwatchedEpisodeForGroup(int animeGroupID, int userID)
    {
        try
        {
            var grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
            if (grp == null)
            {
                return null;
            }

            var allSeries = grp.GetAllSeries().OrderBy(a => a.AirDate).ToList();


            foreach (var ser in allSeries)
            {
                var contract = GetNextUnwatchedEpisode(ser.AnimeSeriesID, userID);
                if (contract != null)
                {
                    return contract;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return null;
        }
    }

    [HttpGet("Episode/ContinueWatching/{userID}/{maxRecords}")]
    public List<CL_AnimeEpisode_User> GetContinueWatchingFilter(int userID, int maxRecords)
    {
        var retEps = new List<CL_AnimeEpisode_User>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user == null) return retEps;

            // find the locked Continue Watching Filter
            var lockedGFs = RepoFactory.FilterPreset.GetLockedGroupFilters();
            var gf = lockedGFs?.FirstOrDefault(a => a.Name == "Continue Watching");
            if (gf == null) return retEps;

            var evaluator = HttpContext.RequestServices.GetRequiredService<FilterEvaluator>();
            var comboGroups = evaluator.EvaluateFilter(gf, userID).Select(a => RepoFactory.AnimeGroup.GetByID(a.Key)).Where(a => a != null)
                .Select(a => a.GetUserContract(userID));

            foreach (var grp in comboGroups)
            {
                var sers = RepoFactory.AnimeSeries.GetByGroupID(grp.AnimeGroupID).OrderBy(a => a.AirDate).ToList();

                var seriesWatching = new List<int>();

                foreach (var ser in sers)
                {
                    if (!user.AllowedSeries(ser)) continue;

                    var anime = ser.GetAnime();
                    var useSeries = seriesWatching.Count == 0 || anime.AnimeType != (int)AnimeType.TVSeries || !anime.GetRelatedAnime().Any(a =>
                        a.RelationType.ToLower().Trim().Equals("sequel") || a.RelationType.ToLower().Trim().Equals("prequel"));
                    if (!useSeries) continue;

                    var ep = GetNextUnwatchedEpisode(ser.AnimeSeriesID, userID);
                    if (ep == null) continue;

                    retEps.Add(ep);

                    // Lets only return the specified amount
                    if (retEps.Count == maxRecords) return retEps;

                    if (anime.AnimeType == (int)AnimeType.TVSeries) seriesWatching.Add(ser.AniDB_ID);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return retEps;
    }

    /// <summary>
    ///     Gets a list of episodes watched based on the most recently watched series
    ///     It will return the next episode to watch in the most recent 10 series
    /// </summary>
    /// <returns></returns>
    [HttpGet("Episode/WatchedToWatch/{maxRecords}/{userID}")]
    public List<CL_AnimeEpisode_User> GetEpisodesToWatch_RecentlyWatched(int maxRecords, int userID)
    {
        var retEps = new List<CL_AnimeEpisode_User>();
        try
        {
            var start = DateTime.Now;

            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user == null)
            {
                return retEps;
            }

            // get a list of series that is applicable
            var allSeriesUser = RepoFactory.AnimeSeries_User.GetMostRecentlyWatched(userID);

            var ts = DateTime.Now - start;
            logger.Info(string.Format("GetEpisodesToWatch_RecentlyWatched:Series: {0}", ts.TotalMilliseconds));
            start = DateTime.Now;

            foreach (var userRecord in allSeriesUser)
            {
                var series = RepoFactory.AnimeSeries.GetByID(userRecord.AnimeSeriesID);
                if (series == null)
                {
                    continue;
                }

                if (!user.AllowedSeries(series))
                {
                    continue;
                }

                var ep = GetNextUnwatchedEpisode(userRecord.AnimeSeriesID, userID);
                if (ep != null)
                {
                    retEps.Add(ep);

                    // Lets only return the specified amount
                    if (retEps.Count == maxRecords)
                    {
                        ts = DateTime.Now - start;
                        logger.Info(string.Format("GetEpisodesToWatch_RecentlyWatched:Episodes: {0}",
                            ts.TotalMilliseconds));
                        return retEps;
                    }
                }
            }

            ts = DateTime.Now - start;
            logger.Info(string.Format("GetEpisodesToWatch_RecentlyWatched:Episodes: {0}", ts.TotalMilliseconds));
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return retEps;
    }

    [HttpGet("Episode/Watched/{maxRecords}/{userID}")]
    public List<CL_AnimeEpisode_User> GetEpisodesRecentlyWatched(int maxRecords, int userID)
    {
        var retEps = new List<CL_AnimeEpisode_User>();
        try
        {
            return
                RepoFactory.AnimeEpisode_User.GetMostRecentlyWatched(userID, maxRecords)
                    .Select(a => RepoFactory.AnimeEpisode.GetByID(a.AnimeEpisodeID).GetUserContract(userID))
                    .ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return retEps;
    }

    [NonAction]
    public IReadOnlyList<SVR_VideoLocal> GetAllFiles()
    {
        try
        {
            return RepoFactory.VideoLocal.GetAll();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return new List<SVR_VideoLocal>();
        }
    }

    [NonAction]
    public SVR_VideoLocal GetFileByID(int id)
    {
        try
        {
            return RepoFactory.VideoLocal.GetByID(id);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return new SVR_VideoLocal();
        }
    }

    [NonAction]
    public List<SVR_VideoLocal> GetFilesRecentlyAdded(int max_records)
    {
        try
        {
            return RepoFactory.VideoLocal.GetMostRecentlyAdded(max_records, 0);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return new List<SVR_VideoLocal>();
        }
    }

    [HttpGet("Episode/RecentlyAdded/{maxRecords}/{userID}")]
    public List<CL_AnimeEpisode_User> GetEpisodesRecentlyAdded(int maxRecords, int userID)
    {
        var retEps = new List<CL_AnimeEpisode_User>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user == null)
            {
                return retEps;
            }

            // We will deal with a large list, don't perform ops on the whole thing!
            var vids = RepoFactory.VideoLocal.GetMostRecentlyAdded(maxRecords*5, userID);
            foreach (var vid in vids)
            {
                if (string.IsNullOrEmpty(vid.Hash)) continue;

                foreach (var ep in vid.GetAnimeEpisodes())
                {
                    var epContract = ep.GetUserContract(userID);
                    if (!user.AllowedSeries(ep.GetAnimeSeries()) || epContract == null) continue;
                    retEps.Add(epContract);

                    // Lets only return the specified amount
                    if (retEps.Count < maxRecords) continue;
                    return retEps;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return retEps;
    }

    [HttpGet("Episode/RecentlyAdded/Summary/{maxRecords}/{userID}")]
    public List<CL_AnimeEpisode_User> GetEpisodesRecentlyAddedSummary(int maxRecords, int userID)
    {
        var retEps = new List<CL_AnimeEpisode_User>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user == null)
            {
                return retEps;
            }

            var start = DateTime.Now;

            var results = RepoFactory.VideoLocal.GetMostRecentlyAdded(-1, userID)
                .SelectMany(a => a.GetAnimeEpisodes()).Select(a => a.AnimeSeriesID).Distinct().Take(maxRecords);


            var ts2 = DateTime.Now - start;
            logger.Info("GetEpisodesRecentlyAddedSummary:RawData in {0} ms", ts2.TotalMilliseconds);
            start = DateTime.Now;

            var numEps = 0;
            foreach (var res in results)
            {
                var ser = RepoFactory.AnimeSeries.GetByID(res);
                if (ser == null)
                {
                    continue;
                }

                if (!user.AllowedSeries(ser))
                {
                    continue;
                }

                var vids = RepoFactory.VideoLocal.GetMostRecentlyAddedForAnime(1, ser.AniDB_ID);
                if (vids.Count == 0)
                {
                    continue;
                }

                var eps = vids[0].GetAnimeEpisodes();
                if (eps.Count == 0)
                {
                    continue;
                }

                var epContract = eps[0].GetUserContract(userID);
                if (epContract != null)
                {
                    retEps.Add(epContract);
                    numEps++;

                    // Lets only return the specified amount
                    if (retEps.Count == maxRecords)
                    {
                        ts2 = DateTime.Now - start;
                        logger.Info("GetEpisodesRecentlyAddedSummary:Episodes in {0} ms", ts2.TotalMilliseconds);
                        start = DateTime.Now;
                        return retEps;
                    }
                }
            }

            ts2 = DateTime.Now - start;
            logger.Info("GetEpisodesRecentlyAddedSummary:Episodes in {0} ms", ts2.TotalMilliseconds);
            start = DateTime.Now;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return retEps;
    }

    [HttpGet("Series/RecentlyAdded/{maxRecords}/{userID}")]
    public List<CL_AnimeSeries_User> GetSeriesRecentlyAdded(int maxRecords, int userID)
    {
        var retSeries = new List<CL_AnimeSeries_User>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user == null)
            {
                return retSeries;
            }

            var series = RepoFactory.AnimeSeries.GetMostRecentlyAdded(maxRecords, userID);
            retSeries.AddRange(series.Select(a => a.GetUserContract(userID)).Where(a => a != null));
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return retSeries;
    }

    [HttpGet("Episode/LastWatched/{animeSeriesID}/{jmmuserID}")]
    public CL_AnimeEpisode_User GetLastWatchedEpisodeForSeries(int animeSeriesID, int jmmuserID)
    {
        try
        {
            return RepoFactory.AnimeEpisode_User.GetLastWatchedEpisodeForSeries(animeSeriesID, jmmuserID)
                ?.GetAnimeEpisode()?.GetUserContract(jmmuserID);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return null;
    }

    [HttpGet("Episode/{animeEpisodeID}/{userID}")]
    public CL_AnimeEpisode_User GetEpisode(int animeEpisodeID, int userID)
    {
        try
        {
            return RepoFactory.AnimeEpisode.GetByID(animeEpisodeID)?.GetUserContract(userID);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return null;
        }
    }

    [NonAction]
    public IReadOnlyList<AnimeEpisode> GetAllEpisodes()
    {
        try
        {
            return RepoFactory.AnimeEpisode.GetAll();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return null;
        }
    }

    [HttpGet("Episode/AniDB/{episodeID}/{userID}")]
    public CL_AnimeEpisode_User GetEpisodeByAniDBEpisodeID(int episodeID, int userID)
    {
        try
        {
            return RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(episodeID)?.GetUserContract(userID);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return null;
        }
    }

    [HttpDelete("File/Association/{videoLocalID}/{animeEpisodeID}")]
    public string RemoveAssociationOnFile(int videoLocalID, int animeEpisodeID)
    {
        try
        {
            var vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
            if (vid == null)
            {
                return "Could not find video record";
            }

            if (string.IsNullOrEmpty(vid.Hash)) //this shouldn't happen
            {
                return "Could not dissociate a cloud file without hash, hash it locally first";
            }

            int? animeSeriesID = null;
            foreach (var ep in vid.GetAnimeEpisodes())
            {
                if (ep.AniDB_EpisodeID != animeEpisodeID)
                {
                    continue;
                }

                animeSeriesID = ep.AnimeSeriesID;
                var xref =
                    RepoFactory.CrossRef_File_Episode.GetByHashAndEpisodeID(vid.Hash, ep.AniDB_EpisodeID);
                if (xref != null)
                {
                    if (xref.CrossRefSource == (int)CrossRefSource.AniDB)
                    {
                        return "Cannot remove associations created from AniDB data";
                    }

                    RepoFactory.CrossRef_File_Episode.Delete(xref.CrossRef_File_EpisodeID);
                }
            }

            if (vid.DateTimeImported.HasValue)
            {
                // Reset the import date.
                vid.DateTimeImported = null;
                RepoFactory.VideoLocal.Save(vid);
            }

            if (animeSeriesID.HasValue)
            {
                var ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID.Value);
                if (ser != null)
                {
                    ser.QueueUpdateStats();
                }
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpPost("File/Status/{videoLocalID}/{isIgnored}")]
    public string SetIgnoreStatusOnFile(int videoLocalID, bool isIgnored)
    {
        try
        {
            var vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
            if (vid == null)
            {
                return "Could not find video record";
            }

            vid.IsIgnored = isIgnored;
            RepoFactory.VideoLocal.Save(vid, false);
            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpPost("File/Variation/{videoLocalID}/{isVariation}")]
    public string SetVariationStatusOnFile(int videoLocalID, bool isVariation)
    {
        try
        {
            var vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
            if (vid == null)
            {
                return "Could not find video record";
            }

            vid.IsVariation = isVariation;
            RepoFactory.VideoLocal.Save(vid, false);
            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }
    }

    [NonAction]
    private void RemoveXRefsForFile(int VideoLocalID)
    {
        var vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
        var fileEps = RepoFactory.CrossRef_File_Episode.GetByHash(vlocal.Hash);

        foreach (var fileEp in fileEps)
        {
            RepoFactory.CrossRef_File_Episode.Delete(fileEp.CrossRef_File_EpisodeID);
        }

        if (vlocal.DateTimeImported.HasValue)
        {
            // Reset the import date.
            vlocal.DateTimeImported = null;
            RepoFactory.VideoLocal.Save(vlocal);
        }
    }

    [HttpPost("File/Association/{videoLocalID}/{animeEpisodeID}")]
    public string AssociateSingleFile(int videoLocalID, int animeEpisodeID)
    {
        try
        {
            var vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
            if (vid == null)
            {
                return "Could not find video record";
            }

            if (string.IsNullOrEmpty(vid.Hash))
            {
                return "Could not associate a cloud file without hash, hash it locally first";
            }

            var ep = RepoFactory.AnimeEpisode.GetByID(animeEpisodeID);
            if (ep == null)
            {
                return "Could not find episode record";
            }

            RemoveXRefsForFile(videoLocalID);
            _commandFactory.CreateAndSave<CommandRequest_LinkFileManually>(
                c =>
                {
                    c.VideoLocalID = videoLocalID;
                    c.EpisodeID = animeEpisodeID;
                }
            );
            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return string.Empty;
    }

    [HttpPost("File/Association/{videoLocalID}/{animeSeriesID}/{startingEpisodeNumber}/{endEpisodeNumber}")]
    public string AssociateSingleFileWithMultipleEpisodes(int videoLocalID, int animeSeriesID,
        int startingEpisodeNumber,
        int endEpisodeNumber)
    {
        try
        {
            var vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
            if (vid == null)
            {
                return "Could not find video record";
            }

            if (vid.Hash == null)
            {
                return "Could not associate a cloud file without hash, hash it locally first";
            }

            var ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
            if (ser == null)
            {
                return "Could not find anime series record";
            }

            RemoveXRefsForFile(videoLocalID);

            for (var i = startingEpisodeNumber; i <= endEpisodeNumber; i++)
            {
                var aniep = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeNumber(ser.AniDB_ID, i)[0];
                if (aniep == null)
                {
                    return "Could not find the AniDB episode record";
                }

                var ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(aniep.EpisodeID);
                if (ep == null)
                {
                    return "Could not find episode record";
                }

                _commandFactory.CreateAndSave<CommandRequest_LinkFileManually>(
                    c =>
                    {
                        c.VideoLocalID = videoLocalID;
                        c.EpisodeID = ep.AnimeEpisodeID;
                    }
                );
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return string.Empty;
    }

    [HttpPost("File/Association/{animeSeriesID}/{startingEpisodeNumber}/{singleEpisode}")]
    public string AssociateMultipleFiles(List<int> videoLocalIDs, int animeSeriesID, string startingEpisodeNumber,
        bool singleEpisode)
    {
        try
        {
            var ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
            if (ser == null)
            {
                return "Could not find anime series record";
            }

            var typeEnum = EpisodeType.Episode;
            if (!int.TryParse(startingEpisodeNumber, out var epNumber))
            {
                var type = startingEpisodeNumber[0];
                var text = startingEpisodeNumber.Substring(1);
                if (int.TryParse(text, out var epNum))
                {
                    switch (type)
                    {
                        case 'S':
                            typeEnum = EpisodeType.Special;
                            break;
                        case 'C':
                            typeEnum = EpisodeType.Credits;
                            break;
                        case 'T':
                            typeEnum = EpisodeType.Trailer;
                            break;
                        case 'P':
                            typeEnum = EpisodeType.Parody;
                            break;
                        case 'O':
                            typeEnum = EpisodeType.Other;
                            break;
                    }

                    epNumber = epNum;
                }
            }

            var total = epNumber + videoLocalIDs.Count - 1;
            var count = 1;

            foreach (var videoLocalID in videoLocalIDs)
            {
                var vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                {
                    return "Could not find video local record";
                }

                if (vid.Hash == null)
                {
                    return "Could not associate a cloud file without hash, hash it locally first";
                }

                var anieps =
                    RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeTypeNumber(ser.AniDB_ID, typeEnum, epNumber);
                if (anieps.Count == 0)
                {
                    return "Could not find the AniDB episode record";
                }

                var aniep = anieps[0];

                var ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(aniep.EpisodeID);
                if (ep == null)
                {
                    return "Could not find episode record";
                }

                RemoveXRefsForFile(videoLocalID);
                var com = _commandFactory.Create<CommandRequest_LinkFileManually>(
                    c =>
                    {
                        c.VideoLocalID = videoLocalID;
                        c.EpisodeID = ep.AnimeEpisodeID;
                    }
                );
                if (singleEpisode)
                {
                    com.Percentage = (int)Math.Round((double)count / total * 100);
                }

                _commandFactory.Save(com);

                count++;
                if (!singleEpisode)
                {
                    epNumber++;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return string.Empty;
    }

    [HttpPost("AniDB/Refresh/{missingInfo}/{outOfDate}/{countOnly}")]
    public int UpdateAniDBFileData(bool missingInfo, bool outOfDate, bool countOnly)
    {
        try
        {
            return Importer.UpdateAniDBFileData(missingInfo, outOfDate, countOnly);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return 0;
        }
    }

    [HttpPost("File/Refresh/{videoLocalID}")]
    public string UpdateFileData(int videoLocalID)
    {
        try
        {
            var vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
            if (vid == null)
            {
                return "File could not be found";
            }

            _commandFactory.CreateAndSave<CommandRequest_GetFile>(
                c =>
                {
                    c.VideoLocalID = vid.VideoLocalID;
                    c.ForceAniDB = true;
                }
            );
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }

        return string.Empty;
    }

    [HttpPost("File/Rescan/{videoLocalID}")]
    public string RescanFile(int videoLocalID)
    {
        try
        {
            var vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
            if (vid == null)
            {
                return "File could not be found";
            }

            if (string.IsNullOrEmpty(vid.Hash))
            {
                return "Could not Update a cloud file without hash, hash it locally first";
            }

            _commandFactory.CreateAndSave<CommandRequest_ProcessFile>(
                c =>
                {
                    c.VideoLocalID = vid.VideoLocalID;
                    c.ForceAniDB = true;
                }
            );
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.Message);
            return ex.Message;
        }

        return string.Empty;
    }

    [HttpPost("File/Rehash/{videoLocalID}")]
    public void RehashFile(int videoLocalID)
    {
        var vl = RepoFactory.VideoLocal.GetByID(videoLocalID);

        if (vl != null)
        {
            var pl = vl.GetBestVideoLocalPlace(true);
            if (pl == null)
            {
                logger.Error("Unable to hash videolocal with id = {videoLocalID}, it has no assigned place", videoLocalID);
                return;
            }

            _commandFactory.CreateAndSave<CommandRequest_HashFile>(
                c =>
                {
                    c.FileName = pl.FullServerPath;
                    c.ForceHash = true;
                }
            );
        }
    }

    /// <summary>
    ///     Deletes the VideoLocal record and the associated physical file
    /// </summary>
    /// <param name="videoplaceid"></param>
    /// <returns></returns>
    [HttpDelete("File/Physical/{videoplaceid}")]
    public string DeleteVideoLocalPlaceAndFile(int videoplaceid)
    {
        try
        {
            var place = RepoFactory.VideoLocalPlace.GetByID(videoplaceid);
            if (place?.VideoLocal == null)
            {
                return "Database entry does not exist";
            }

            var service = HttpContext.RequestServices.GetRequiredService<VideoLocal_PlaceService>();
            service.RemoveRecordAndDeletePhysicalFile(place);
            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }
    }

    /// <summary>
    ///     Deletes the VideoLocal record and the associated physical file
    /// </summary>
    /// <param name="videoplaceid"></param>
    /// <returns></returns>
    [HttpDelete("File/Physical/{videoplaceid}/SkipFolder")]
    public string DeleteVideoLocalPlaceAndFileSkipFolder(int videoplaceid)
    {
        try
        {
            var place = RepoFactory.VideoLocalPlace.GetByID(videoplaceid);
            if (place?.VideoLocal == null)
            {
                return "Database entry does not exist";
            }

            var service = HttpContext.RequestServices.GetRequiredService<VideoLocal_PlaceService>();
            service.RemoveRecordAndDeletePhysicalFile(place);
            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpPost("File/Resume/{videoLocalID}/{resumeposition}/{userID}")]
    public string SetResumePosition(int videoLocalID, long resumeposition, int userID)
    {
        try
        {
            var vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
            if (vid == null)
            {
                return "Could not find video local record";
            }

            vid.SetResumePosition(resumeposition, userID);
            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpGet("File/ForAnime/{animeID}/{userID}")]
    public List<CL_VideoLocal> GetVideoLocalsForAnime(int animeID, int userID)
    {
        try
        {
            // Try sorted first, then try unsorted if failed
            var list = RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID).Where(a =>
                    a?.Places?.FirstOrDefault(b => !string.IsNullOrEmpty(b.FullServerPath))?.FullServerPath != null)
                .DistinctBy(a => a?.Places?.FirstOrDefault()?.FullServerPath)
                .ToList();
            list.Sort(FileQualityFilter.CompareTo);
            return list.Select(a => a.ToClient(userID)).ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            try
            {
                // Two checks because the Where doesn't guarantee that First will not be null, only that a not-null value exists
                var list = RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID).Where(a =>
                        a?.Places?.FirstOrDefault(b => !string.IsNullOrEmpty(b.FullServerPath))?.FullServerPath != null)
                    .DistinctBy(a => a?.Places?.FirstOrDefault()?.FullServerPath)
                    .Select(a => a.ToClient(userID))
                    .ToList();
                return list;
            }
            catch
            {
                // Ignore
            }
        }

        return new List<CL_VideoLocal>();
    }

    [HttpGet("AniDB/Vote/{animeID}")]
    public AniDB_Vote GetUserVote(int animeID)
    {
        try
        {
            return RepoFactory.AniDB_Vote.GetByEntity(animeID).FirstOrDefault();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return null;
    }

    [HttpGet("Episode/IncrementStats/{animeEpisodeID}/{userID}/{statCountType}")]
    public void IncrementEpisodeStats(int animeEpisodeID, int userID, int statCountType)
    {
        try
        {
            var ep = RepoFactory.AnimeEpisode.GetByID(animeEpisodeID);
            if (ep == null)
            {
                return;
            }

            var epUserRecord = ep.GetUserRecord(userID);

            if (epUserRecord == null)
            {
                epUserRecord = new SVR_AnimeEpisode_User(userID, ep.AnimeEpisodeID, ep.AnimeSeriesID);
            }
            //epUserRecord.WatchedDate = DateTime.Now;

            switch ((StatCountType)statCountType)
            {
                case StatCountType.Played:
                    epUserRecord.PlayedCount++;
                    break;
                case StatCountType.Stopped:
                    epUserRecord.StoppedCount++;
                    break;
                case StatCountType.Watched:
                    epUserRecord.WatchedCount++;
                    break;
            }

            RepoFactory.AnimeEpisode_User.Save(epUserRecord);

            var ser = ep.GetAnimeSeries();
            if (ser == null)
            {
                return;
            }

            var userRecord = ser.GetOrCreateUserRecord(userID);

            switch ((StatCountType)statCountType)
            {
                case StatCountType.Played:
                    userRecord.PlayedCount++;
                    break;
                case StatCountType.Stopped:
                    userRecord.StoppedCount++;
                    break;
                case StatCountType.Watched:
                    userRecord.WatchedCount++;
                    break;
            }

            RepoFactory.AnimeSeries_User.Save(userRecord);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }
    }

    [HttpDelete("AniDB/MyList/{fileID}")]
    public void DeleteFileFromMyList(int fileID)
    {
        var vl = RepoFactory.VideoLocal.GetByMyListID(fileID);
        if (vl == null)
        {
            return;
        }

        _commandFactory.CreateAndSave<CommandRequest_DeleteFileFromMyList>(
            c =>
            {
                c.Hash = vl.Hash;
                c.FileSize = vl.FileSize;
            }
        );
    }

    [HttpPost("AniDB/MyList/{hash}")]
    public void ForceAddFileToMyList(string hash)
    {
        try
        {
            _commandFactory.CreateAndSave<CommandRequest_AddFileToMyList>(c => c.Hash = hash);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }
    }

    [HttpGet("AniDB/Episode/ForAnime/{animeID}")]
    public List<CL_AniDB_Episode> GetAniDBEpisodesForAnime(int animeID)
    {
        try
        {
            return RepoFactory.AniDB_Episode.GetByAnimeID(animeID)
                .Select(a => a.ToClient())
                .OrderBy(a => a.EpisodeType)
                .ThenBy(a => a.EpisodeNumber)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return new List<CL_AniDB_Episode>();
    }

    [HttpGet("Episode/ForSeries/{animeSeriesID}/{userID}")]
    public List<CL_AnimeEpisode_User> GetEpisodesForSeries(int animeSeriesID, int userID)
    {
        var eps = new List<CL_AnimeEpisode_User>();
        try
        {
            return
                RepoFactory.AnimeEpisode.GetBySeriesID(animeSeriesID)
                    .Where(a => a != null && !a.IsHidden)
                    .Select(a => a.GetUserContract(userID))
                    .Where(a => a != null)
                    .ToList();
            /*
                            DateTime start = DateTime.Now;
                            AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
                            AnimeSeriesRepository repAnimeSer = new AnimeSeriesRepository();
                            VideoLocalRepository repVids = new VideoLocalRepository();
                            CrossRef_File_EpisodeRepository repCrossRefs = new CrossRef_File_EpisodeRepository();

                            // get all the data first
                            // we do this to reduce the amount of database calls, which makes it a lot faster
                            AnimeSeries series = repAnimeSer.GetByID(animeSeriesID);
                            if (series == null) return eps;

                            List<AnimeEpisode> epList = repEps.GetBySeriesID(animeSeriesID);
                            List<AnimeEpisode_User> userRecordList = repEpUsers.GetByUserIDAndSeriesID(userID, animeSeriesID);
                            Dictionary<int, AnimeEpisode_User> dictUserRecords = new Dictionary<int, AnimeEpisode_User>();
                            foreach (AnimeEpisode_User epuser in userRecordList)
                                dictUserRecords[epuser.AnimeEpisodeID] = epuser;

                            AniDB_EpisodeRepository repAniEps = new AniDB_EpisodeRepository();
                            List<AniDB_Episode> aniEpList = repAniEps.GetByAnimeID(series.AniDB_ID);
                            Dictionary<int, AniDB_Episode> dictAniEps = new Dictionary<int, AniDB_Episode>();
                            foreach (AniDB_Episode aniep in aniEpList)
                                dictAniEps[aniep.EpisodeID] = aniep;

                            // get all the video local records and cross refs
                            List<VideoLocal> vids = repVids.GetByAniDBAnimeID(series.AniDB_ID);
                            List<CrossRef_File_Episode> crossRefs = repCrossRefs.GetByAnimeID(series.AniDB_ID);

                            TimeSpan ts = DateTime.Now - start;
                            logger.Info("GetEpisodesForSeries: {0} (Database) in {1} ms", series.GetAnime().MainTitle, ts.TotalMilliseconds);


                            start = DateTime.Now;
                            foreach (AnimeEpisode ep in epList)
                            {
                                if (dictAniEps.ContainsKey(ep.AniDB_EpisodeID))
                                {
                                    List<VideoLocal> epVids = new List<VideoLocal>();
                                    foreach (CrossRef_File_Episode xref in crossRefs)
                                    {
                                        if (xref.EpisodeID == dictAniEps[ep.AniDB_EpisodeID].EpisodeID)
                                        {
                                            // don't add the same file twice, this will occur when
                                            // one file appears over more than one episodes
                                            Dictionary<string, string> addedFiles = new Dictionary<string, string>();
                                            foreach (VideoLocal vl in vids)
                                            {
                                                if (string.Equals(xref.Hash, vl.Hash, StringComparison.InvariantCultureIgnoreCase))
                                                {
                                                    if (!addedFiles.ContainsKey(xref.Hash.Trim().ToUpper()))
                                                    {
                                                        addedFiles[xref.Hash.Trim().ToUpper()] = xref.Hash.Trim().ToUpper();
                                                        epVids.Add(vl);
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    AnimeEpisode_User epuser = null;
                                    if (dictUserRecords.ContainsKey(ep.AnimeEpisodeID))
                                        epuser = dictUserRecords[ep.AnimeEpisodeID];

                                    eps.Add(ep.ToContract(dictAniEps[ep.AniDB_EpisodeID], epVids, epuser, null));
                                }
                            }

                            ts = DateTime.Now - start;
                            logger.Info("GetEpisodesForSeries: {0} (Contracts) in {1} ms", series.GetAnime().MainTitle, ts.TotalMilliseconds);
                            */
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return eps;
    }

    [HttpGet("Episode/Old/{animeSeriesID}")]
    public List<CL_AnimeEpisode_User> GetEpisodesForSeriesOld(int animeSeriesID)
    {
        var eps = new List<CL_AnimeEpisode_User>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(1) ??
                       RepoFactory.JMMUser.GetAll().FirstOrDefault(a => a.Username == "Default");
            //HACK (We should have a default user locked)
            if (user != null)
            {
                return GetEpisodesForSeries(animeSeriesID, user.JMMUserID);
            }
            /*
                            JMMUser u

                            DateTime start = DateTime.Now;
                            AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
                            AnimeSeriesRepository repAnimeSer = new AnimeSeriesRepository();
                            CrossRef_File_EpisodeRepository repCrossRefs = new CrossRef_File_EpisodeRepository();


                            // get all the data first
                            // we do this to reduce the amount of database calls, which makes it a lot faster
                            AnimeSeries series = repAnimeSer.GetByID(animeSeriesID);
                            if (series == null) return eps;

                            List<AnimeEpisode> epList = repEps.GetBySeriesID(animeSeriesID);

                            AniDB_EpisodeRepository repAniEps = new AniDB_EpisodeRepository();
                            List<AniDB_Episode> aniEpList = repAniEps.GetByAnimeID(series.AniDB_ID);
                            Dictionary<int, AniDB_Episode> dictAniEps = new Dictionary<int, AniDB_Episode>();
                            foreach (AniDB_Episode aniep in aniEpList)
                                dictAniEps[aniep.EpisodeID] = aniep;

                            List<CrossRef_File_Episode> crossRefList = repCrossRefs.GetByAnimeID(series.AniDB_ID);




                            TimeSpan ts = DateTime.Now - start;
                            logger.Info("GetEpisodesForSeries: {0} (Database) in {1} ms", series.GetAnime().MainTitle, ts.TotalMilliseconds);


                            start = DateTime.Now;
                            foreach (AnimeEpisode ep in epList)
                            {
                                List<CrossRef_File_Episode> xrefs = new List<CrossRef_File_Episode>();
                                foreach (CrossRef_File_Episode xref in crossRefList)
                                {
                                    if (ep.AniDB_EpisodeID == xref.EpisodeID)
                                        xrefs.Add(xref);
                                }

                                if (dictAniEps.ContainsKey(ep.AniDB_EpisodeID))
                                    eps.Add(ep.ToContractOld(dictAniEps[ep.AniDB_EpisodeID]));
                            }

                            ts = DateTime.Now - start;
                            logger.Info("GetEpisodesForSeries: {0} (Contracts) in {1} ms", series.GetAnime().MainTitle, ts.TotalMilliseconds);
                            */
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return eps;
    }

    [HttpGet("File/Detailed/{episodeID}/{userID}")]
    public List<CL_VideoDetailed> GetFilesForEpisode(int episodeID, int userID)
    {
        try
        {
            var ep = RepoFactory.AnimeEpisode.GetByID(episodeID);
            if (ep != null)
            {
                var files = ep.GetVideoLocals();
                files.Sort(FileQualityFilter.CompareTo);
                return files.Select(a => a.ToClientDetailed(userID)).ToList();
            }

            return new List<CL_VideoDetailed>();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return new List<CL_VideoDetailed>();
    }

    [HttpGet("File/ForEpisode/{episodeID}/{userID}")]
    public List<CL_VideoLocal> GetVideoLocalsForEpisode(int episodeID, int userID)
    {
        var contracts = new List<CL_VideoLocal>();
        try
        {
            var ep = RepoFactory.AnimeEpisode.GetByID(episodeID);
            if (ep != null)
            {
                foreach (var vid in ep.GetVideoLocals())
                {
                    contracts.Add(vid.ToClient(userID));
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return contracts;
    }

    [HttpPost("File/Watch/{videoLocalID}/{watchedStatus}/{userID}")]
    public string ToggleWatchedStatusOnVideo(int videoLocalID, bool watchedStatus, int userID)
    {
        try
        {
            var vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
            if (vid == null)
            {
                return "Could not find video local record";
            }

            vid.ToggleWatchedStatus(watchedStatus, true, DateTime.Now, true, userID, true, true);
            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpPost("Episode/Watch/{animeEpisodeID}/{watchedStatus}/{userID}")]
    public CL_Response<CL_AnimeEpisode_User> ToggleWatchedStatusOnEpisode(int animeEpisodeID, bool watchedStatus,
        int userID)
    {
        var response = new CL_Response<CL_AnimeEpisode_User> { ErrorMessage = "", Result = null };
        try
        {
            var ep = RepoFactory.AnimeEpisode.GetByID(animeEpisodeID);
            if (ep == null)
            {
                response.ErrorMessage = "Could not find anime episode record";
                return response;
            }

            ep.ToggleWatchedStatus(watchedStatus, true, DateTime.Now, false, userID, true);
            var series = ep.GetAnimeSeries();
            series?.UpdateStats(true, false);
            series?.AnimeGroup?.TopLevelAnimeGroup?.UpdateStatsFromTopLevel(true, true);
            //StatsCache.Instance.UpdateUsingSeries(ep.GetAnimeSeries().AnimeSeriesID);

            // refresh from db

            response.Result = ep.GetUserContract(userID);

            return response;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            response.ErrorMessage = ex.Message;
            return response;
        }
    }

    [HttpPost("File/Detailed/{videoLocalID}/{userID}")]
    public CL_VideoDetailed GetVideoDetailed(int videoLocalID, int userID)
    {
        try
        {
            var vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
            if (vid == null)
            {
                return null;
            }

            return vid.ToClientDetailed(userID);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return null;
        }
    }

    [HttpGet("Episode/ForSingleFile/{videoLocalID}/{userID}")]
    public List<CL_AnimeEpisode_User> GetEpisodesForFile(int videoLocalID, int userID)
    {
        var contracts = new List<CL_AnimeEpisode_User>();
        try
        {
            var vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
            if (vid == null)
            {
                return contracts;
            }

            foreach (var ep in vid.GetAnimeEpisodes())
            {
                var eps = ep.GetUserContract(userID);
                if (eps != null)
                {
                    contracts.Add(eps);
                }
            }

            return contracts;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return contracts;
        }
    }

    /// <summary>
    ///     Get all the release groups for an episode for which the user is collecting
    /// </summary>
    /// <param name="aniDBEpisodeID"></param>
    /// <returns></returns>
    [HttpGet("AniDB/ReleaseGroup/FromEpisode/{aniDBEpisodeID}")]
    public List<CL_AniDB_GroupStatus> GetMyReleaseGroupsForAniDBEpisode(int aniDBEpisodeID)
    {
        var start = DateTime.Now;

        var relGroups = new List<CL_AniDB_GroupStatus>();

        try
        {
            var aniEp = RepoFactory.AniDB_Episode.GetByEpisodeID(aniDBEpisodeID);
            if (aniEp == null)
            {
                return relGroups;
            }

            if (aniEp.GetEpisodeTypeEnum() != EpisodeType.Episode)
            {
                return relGroups;
            }

            var series = RepoFactory.AnimeSeries.GetByAnimeID(aniEp.AnimeID);
            if (series == null)
            {
                return relGroups;
            }

            // get a list of all the release groups the user is collecting
            var userReleaseGroups = new Dictionary<int, int>();
            foreach (var ep in series.GetAnimeEpisodes())
            {
                var vids = ep.GetVideoLocals();
                var hashes = vids.Select(a => a.Hash).Distinct().ToList();
                foreach (var s in hashes)
                {
                    var vid = vids.First(a => a.Hash == s);
                    AniDB_File anifile = vid.GetAniDBFile();
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
            var grpStatuses = RepoFactory.AniDB_GroupStatus.GetByAnimeID(aniEp.AnimeID);
            foreach (var gs in grpStatuses)
            {
                if (userReleaseGroups.ContainsKey(gs.GroupID))
                {
                    if (gs.HasGroupReleasedEpisode(aniEp.EpisodeNumber))
                    {
                        var cl = gs.ToClient();
                        cl.UserCollecting = true;
                        cl.FileCount = userReleaseGroups[gs.GroupID];
                        relGroups.Add(cl);
                    }
                }
            }

            var ts = DateTime.Now - start;
            logger.Info("GetMyReleaseGroupsForAniDBEpisode  in {0} ms", ts.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return relGroups;
    }

    #endregion

    #region Groups and Series

    [HttpGet("Series/{animeSeriesID}/{userID}")]
    public CL_AnimeSeries_User GetSeries(int animeSeriesID, int userID)
    {
        try
        {
            return RepoFactory.AnimeSeries.GetByID(animeSeriesID)?.GetUserContract(userID);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return null;
    }

    [NonAction]
    public List<CL_AnimeSeries_User> GetSeriesByFolderID(int FolderID, int userID, int max)
    {
        try
        {
            var limit = 0;
            var list = new List<CL_AnimeSeries_User>();

            foreach (var vi in RepoFactory.VideoLocal.GetByImportFolder(FolderID))
            {
                foreach (var ae in GetEpisodesForFile(vi.VideoLocalID, userID))
                {
                    var ase = GetSeries(ae.AnimeSeriesID, userID);
                    if (!list.Contains(ase))
                    {
                        limit++;
                        list.Add(ase);
                        if (limit >= max)
                        {
                            break;
                        }
                    }
                }
            }

            return list;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return new List<CL_AnimeSeries_User>();
    }

    /// <summary>
    /// </summary>
    /// <param name="animeID"></param>
    /// <param name="voteValue">Must be 1 or 2 (Anime or Anime Temp(</param>
    /// <param name="voteType"></param>
    [HttpPost("AniDB/Vote/{animeID}/{voteType}")]
    public void VoteAnime(int animeID, [FromForm] decimal voteValue, int voteType)
    {
        var msg = $"Voting for anime: {animeID} - Value: {voteValue}";
        logger.Info(msg);

        // lets save to the database and assume it will work
        var thisVote =
            RepoFactory.AniDB_Vote.GetByEntityAndType(animeID, AniDBVoteType.AnimeTemp) ??
            RepoFactory.AniDB_Vote.GetByEntityAndType(animeID, AniDBVoteType.Anime);

        if (thisVote == null)
        {
            thisVote = new AniDB_Vote { EntityID = animeID };
        }

        thisVote.VoteType = voteType;

        var iVoteValue = 0;
        if (voteValue > 0)
        {
            iVoteValue = (int)(voteValue * 100);
        }
        else
        {
            iVoteValue = (int)voteValue;
        }

        msg = $"Voting for anime Formatted: {animeID} - Value: {iVoteValue}";
        logger.Info(msg);

        thisVote.VoteValue = iVoteValue;
        RepoFactory.AniDB_Vote.Save(thisVote);

        _commandFactory.CreateAndSave<CommandRequest_VoteAnime>(
            c =>
            {
                c.AnimeID = animeID;
                c.VoteType = voteType;
                c.VoteValue = voteValue;
            }
        );
    }

    [HttpDelete("AniDB/Vote/{animeID}")]
    public void VoteAnimeRevoke(int animeID)
    {
        // lets save to the database and assume it will work

        var dbVotes = RepoFactory.AniDB_Vote.GetByEntity(animeID);
        AniDB_Vote thisVote = null;
        foreach (var dbVote in dbVotes)
        {
            // we can only have anime permanent or anime temp but not both
            if (dbVote.VoteType == (int)AniDBVoteType.Anime ||
                dbVote.VoteType == (int)AniDBVoteType.AnimeTemp)
            {
                thisVote = dbVote;
            }
        }

        if (thisVote == null)
        {
            return;
        }

        _commandFactory.CreateAndSave<CommandRequest_VoteAnime>(
            c =>
            {
                c.AnimeID = animeID;
                c.VoteType = thisVote.VoteType;
                c.VoteValue = -1;
            }
        );

        RepoFactory.AniDB_Vote.Delete(thisVote.AniDB_VoteID);
    }

    /// <summary>
    ///     Set watched status on all normal episodes
    /// </summary>
    /// <param name="animeSeriesID"></param>
    /// <param name="watchedStatus"></param>
    /// <param name="maxEpisodeNumber">Use this to specify a max episode number to apply to</param>
    /// <returns></returns>
    [HttpPost("Series/Watch/{animeSeriesID}/{watchedStatus}/{maxEpisodeNumber}/{episodeType}/{userID}")]
    public string SetWatchedStatusOnSeries(int animeSeriesID, bool watchedStatus, int maxEpisodeNumber, int episodeType,
        int userID)
    {
        try
        {
            var eps = RepoFactory.AnimeEpisode.GetBySeriesID(animeSeriesID);

            SVR_AnimeSeries ser = null;
            foreach (var ep in eps)
            {
                if (ep?.AniDB_Episode == null)
                {
                    continue;
                }

                if (ep.EpisodeTypeEnum == (EpisodeType)episodeType &&
                    ep.AniDB_Episode.EpisodeNumber <= maxEpisodeNumber)
                {
                    // check if this episode is already watched
                    var currentStatus = false;
                    AnimeEpisode_User epUser = ep.GetUserRecord(userID);
                    if (epUser != null)
                    {
                        currentStatus = epUser.WatchedCount > 0 ? true : false;
                    }

                    if (currentStatus != watchedStatus)
                    {
                        logger.Info("Updating episode: {0} to {1}", ep.AniDB_Episode.EpisodeNumber, watchedStatus);
                        ep.ToggleWatchedStatus(watchedStatus, true, DateTime.Now, false, userID, false);
                    }
                }


                ser = ep.GetAnimeSeries();
            }

            // now update the stats
            if (ser != null)
            {
                ser.UpdateStats(true, true);
                ser.AnimeGroup?.TopLevelAnimeGroup?.UpdateStatsFromTopLevel(true, true);
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }
    }

    [NonAction]
    public List<CL_AnimeSeries_FileStats> GetSeriesFileStatsByFolderID(int FolderID, int userID, int max)
    {
        try
        {
            var limit = 0;
            var list = new Dictionary<int, CL_AnimeSeries_FileStats>();
            foreach (var vi in RepoFactory.VideoLocal.GetByImportFolder(FolderID))
            {
                foreach (var ae in GetEpisodesForFile(vi.VideoLocalID, userID))
                {
                    var ase = GetSeries(ae.AnimeSeriesID, userID);
                    //check if series is in list if not add it
                    if (list.TryGetValue(ase.AnimeSeriesID, out var asfs) == false)
                    {
                        limit++;
                        if (limit >= max)
                        {
                            continue;
                        }

                        asfs = new CL_AnimeSeries_FileStats
                        {
                            AnimeSeriesName = ase.AniDBAnime.AniDBAnime.MainTitle,
                            FileCount = 0,
                            FileSize = 0,
                            Folders = new List<string>(),
                            AnimeSeriesID = ase.AnimeSeriesID
                        };
                        list.Add(ase.AnimeSeriesID, asfs);
                    }

                    asfs.FileCount++;
                    asfs.FileSize += vi.FileSize;

                    //string filePath = Pri.LongPath.Path.GetDirectoryName(vi.FilePath).Replace(importLocation, "");
                    //filePath = filePath.TrimStart('\\');
                    var filePath = RepoFactory.VideoLocalPlace.GetByVideoLocal(vi.VideoLocalID)[0].FilePath;
                    if (!asfs.Folders.Contains(filePath))
                    {
                        asfs.Folders.Add(filePath);
                    }
                }
            }

            return list.Values.ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return new List<CL_AnimeSeries_FileStats>();
    }

    [HttpGet("Series/ForAnime/{animeID}/{userID}")]
    public CL_AnimeSeries_User GetSeriesForAnime(int animeID, int userID)
    {
        try
        {
            return RepoFactory.AnimeSeries.GetByAnimeID(animeID)?.GetUserContract(userID);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return null;
    }

    [HttpGet("Series/ExistingForAnime/{animeID}")]
    public bool GetSeriesExistingForAnime(int animeID)
    {
        try
        {
            var series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
            if (series == null)
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return true;
    }

    [HttpGet("Group/{userID}")]
    public List<CL_AnimeGroup_User> GetAllGroups(int userID)
    {
        var grps = new List<CL_AnimeGroup_User>();
        try
        {
            return RepoFactory.AnimeGroup.GetAll()
                .Select(a => a.GetUserContract(userID))
                .OrderBy(a => a.GroupName)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return grps;
    }

    [HttpGet("Group/AboveGroup/{animeGroupID}/{userID}")]
    public List<CL_AnimeGroup_User> GetAllGroupsAboveGroupInclusive(int animeGroupID, int userID)
    {
        var grps = new List<CL_AnimeGroup_User>();
        try
        {
            int? grpid = animeGroupID;
            while (grpid.HasValue)
            {
                grpid = null;
                var grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp != null)
                {
                    grps.Add(grp.GetUserContract(userID));
                    grpid = grp.AnimeGroupParentID;
                }
            }

            return grps;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return grps;
    }

    [HttpGet("Group/AboveSeries/{animeSeriesID}/{userID}")]
    public List<CL_AnimeGroup_User> GetAllGroupsAboveSeries(int animeSeriesID, int userID)
    {
        var grps = new List<CL_AnimeGroup_User>();
        try
        {
            var series = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
            if (series == null)
            {
                return grps;
            }

            foreach (var grp in series.AllGroupsAbove)
            {
                grps.Add(grp.GetUserContract(userID));
            }

            return grps;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return grps;
    }

    [HttpGet("Group/{animeGroupID}/{userID}")]
    public CL_AnimeGroup_User GetGroup(int animeGroupID, int userID)
    {
        try
        {
            return RepoFactory.AnimeGroup.GetByID(animeGroupID)?.GetUserContract(userID);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return null;
    }

    [HttpPost("Group/Recreate/{resume}")]
    public void RecreateAllGroups(bool resume = false)
    {
        try
        {
            new AnimeGroupCreator().RecreateAllGroups();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }
    }

    [HttpPost("Group/Rename")]
    public string RenameAllGroups()
    {
        try
        {
            SVR_AnimeGroup.RenameAllGroups();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }

        return string.Empty;
    }

    [HttpDelete("Group/{animeGroupID}/{deleteFiles}")]
    public string DeleteAnimeGroup(int animeGroupID, bool deleteFiles)
    {
        try
        {
            var grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
            if (grp == null)
            {
                return "Group does not exist";
            }

            if (grp.GetAllSeries().Count != 0)
            {
                return "Group must be empty to be deleted. Move the series out of the group first.";
            }

            grp.DeleteGroup();

            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpGet("Group/ForFilter/{groupFilterID}/{userID}/{getSingleSeriesGroups}")]
    public List<CL_AnimeGroup_User> GetAnimeGroupsForFilter(int groupFilterID, int userID, bool getSingleSeriesGroups)
    {
        var retGroups = new List<CL_AnimeGroup_User>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user == null) return retGroups;

            var gf = RepoFactory.FilterPreset.GetByID(groupFilterID);

            if (gf != null)
            {
                var evaluator = HttpContext.RequestServices.GetRequiredService<FilterEvaluator>();
                var results = evaluator.EvaluateFilter(gf, userID);
                retGroups = results.Select(a => RepoFactory.AnimeGroup.GetByID(a.Key)).Where(a => a != null).Select(a => a.GetUserContract(userID)).ToList();
            }

            if (!getSingleSeriesGroups) return retGroups;

            var nGroups = new List<CL_AnimeGroup_User>();
            foreach (var cag in retGroups)
            {
                var ng = cag.DeepCopy();
                if (cag.Stat_SeriesCount == 1)
                {
                    if (cag.DefaultAnimeSeriesID.HasValue)
                    {
                        ng.SeriesForNameOverride = RepoFactory.AnimeSeries.GetByGroupID(ng.AnimeGroupID)
                            .FirstOrDefault(a => a.AnimeSeriesID == cag.DefaultAnimeSeriesID.Value)
                            ?.GetUserContract(userID);
                    }

                    ng.SeriesForNameOverride ??= RepoFactory.AnimeSeries.GetByGroupID(ng.AnimeGroupID).FirstOrDefault()?.GetUserContract(userID);
                }

                nGroups.Add(ng);
            }

            return nGroups;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return retGroups;
    }


    /// <summary>
    ///     Can only be used when the group only has one series
    /// </summary>
    /// <param name="animeGroupID"></param>
    /// <param name="allSeries"></param>
    /// <returns></returns>
    [HttpGet("Series/ForGroup/{animeGroupID}/{userID}")]
    public static SVR_AnimeSeries GetSeriesForGroup(int animeGroupID, List<SVR_AnimeSeries> allSeries)
    {
        try
        {
            foreach (var ser in allSeries)
            {
                if (ser.AnimeGroupID == animeGroupID)
                {
                    return ser;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return null;
        }
    }

    [HttpPost("Group/{userID}")]
    public CL_Response<CL_AnimeGroup_User> SaveGroup(CL_AnimeGroup_Save_Request contract, int userID)
    {
        var contractout = new CL_Response<CL_AnimeGroup_User> { ErrorMessage = string.Empty, Result = null };
        try
        {
            SVR_AnimeGroup grp = null;
            if (contract.AnimeGroupID.HasValue && contract.AnimeGroupID != 0)
            {
                grp = RepoFactory.AnimeGroup.GetByID(contract.AnimeGroupID.Value);
                if (grp == null)
                {
                    contractout.ErrorMessage = "Could not find existing group with ID: " +
                                               contract.AnimeGroupID.Value;
                    return contractout;
                }
            }
            else
            {
                grp = new SVR_AnimeGroup
                {
                    Description = string.Empty,
                    IsManuallyNamed = 0,
                    DateTimeCreated = DateTime.Now,
                    DateTimeUpdated = DateTime.Now,
                    MissingEpisodeCount = 0,
                    MissingEpisodeCountGroups = 0,
                    OverrideDescription = 0
                };
            }

            if (string.IsNullOrEmpty(contract.GroupName))
            {
                contractout.ErrorMessage = "Must specify a group name";
                return contractout;
            }

            grp.AnimeGroupParentID = contract.AnimeGroupParentID;
            grp.Description = contract.Description;
            grp.GroupName = contract.GroupName;

            grp.IsManuallyNamed = contract.IsManuallyNamed;
            grp.OverrideDescription = 0;

            grp.ValidateMainSeries();

            RepoFactory.AnimeGroup.Save(grp, true, true);

            var userRecord = grp.GetUserRecord(userID);
            if (userRecord == null)
            {
                userRecord = new SVR_AnimeGroup_User(userID, grp.AnimeGroupID);
            }

            userRecord.IsFave = contract.IsFave;
            RepoFactory.AnimeGroup_User.Save(userRecord);

            contractout.Result = grp.GetUserContract(userID);


            return contractout;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            contractout.ErrorMessage = ex.Message;
            return contractout;
        }
    }

    [HttpPost("Series/Move/{animeSeriesID}/{newAnimeGroupID}/{userID}")]
    public CL_Response<CL_AnimeSeries_User> MoveSeries(int animeSeriesID, int newAnimeGroupID, int userID)
    {
        var contractout = new CL_Response<CL_AnimeSeries_User> { ErrorMessage = string.Empty, Result = null };
        try
        {
            // make sure the series exists
            var ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
            if (ser == null)
            {
                contractout.ErrorMessage = "Could not find existing series with ID: " + animeSeriesID;
                return contractout;
            }

            // make sure the group exists
            var grp = RepoFactory.AnimeGroup.GetByID(newAnimeGroupID);
            if (grp == null)
            {
                contractout.ErrorMessage = "Could not find existing group with ID: " + newAnimeGroupID;
                return contractout;
            }

            ser.MoveSeries(grp);

            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(ser.AniDB_ID);
            if (anime == null)
            {
                contractout.ErrorMessage = string.Format("Could not find anime record with ID: {0}", ser.AniDB_ID);
                return contractout;
            }

            contractout.Result = ser.GetUserContract(userID);

            return contractout;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            contractout.ErrorMessage = ex.Message;
            return contractout;
        }
    }

    [HttpPost("Series/{userID}")]
    public CL_Response<CL_AnimeSeries_User> SaveSeries(CL_AnimeSeries_Save_Request contract, int userID)
    {
        var contractout = new CL_Response<CL_AnimeSeries_User> { ErrorMessage = string.Empty, Result = null };
        try
        {
            SVR_AnimeSeries ser = null;

            int? oldGroupID = null;
            if (contract.AnimeSeriesID.HasValue)
            {
                ser = RepoFactory.AnimeSeries.GetByID(contract.AnimeSeriesID.Value);
                if (ser == null)
                {
                    contractout.ErrorMessage = "Could not find existing series with ID: " +
                                               contract.AnimeSeriesID.Value;
                    return contractout;
                }

                // check if we are moving a series
                oldGroupID = ser.AnimeGroupID;
            }
            else
            {
                ser = new SVR_AnimeSeries
                {
                    DateTimeCreated = DateTime.Now,
                    DefaultAudioLanguage = string.Empty,
                    DefaultSubtitleLanguage = string.Empty,
                    MissingEpisodeCount = 0,
                    MissingEpisodeCountGroups = 0,
                    LatestLocalEpisodeNumber = 0,
                    SeriesNameOverride = string.Empty
                };
            }


            ser.AnimeGroupID = contract.AnimeGroupID;
            ser.AniDB_ID = contract.AniDB_ID;
            ser.DefaultAudioLanguage = contract.DefaultAudioLanguage;
            ser.DefaultSubtitleLanguage = contract.DefaultSubtitleLanguage;
            ser.DateTimeUpdated = DateTime.Now;
            ser.SeriesNameOverride = contract.SeriesNameOverride;
            ser.DefaultFolder = contract.DefaultFolder;

            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(ser.AniDB_ID);
            if (anime == null)
            {
                contractout.ErrorMessage = $"Could not find anime record with ID: {ser.AniDB_ID}";
                return contractout;
            }

            // update stats for groups
            //ser.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true ,true, true);

            //Update and Save
            ser.UpdateStats(true, true);
            ser.AnimeGroup?.TopLevelAnimeGroup?.UpdateStatsFromTopLevel(true, true);

            if (oldGroupID.HasValue)
            {
                var grp = RepoFactory.AnimeGroup.GetByID(oldGroupID.Value);
                grp?.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true);
            }

            contractout.Result = ser.GetUserContract(userID);
            return contractout;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            contractout.ErrorMessage = ex.Message;
            return contractout;
        }
    }

    [HttpPost("Series/CreateFromAnime/{animeID}/{userID}/{animeGroupID?}/{forceOverwrite}")]
    public CL_Response<CL_AnimeSeries_User> CreateSeriesFromAnime(int animeID, int? animeGroupID, int userID,
        bool forceOverwrite)
    {
        var response = new CL_Response<CL_AnimeSeries_User> { Result = null, ErrorMessage = string.Empty };
        try
        {
            if (animeGroupID.HasValue && animeGroupID.Value > 0)
            {
                var grp = RepoFactory.AnimeGroup.GetByID(animeGroupID.Value);
                if (grp == null)
                {
                    response.ErrorMessage = "Could not find the specified group";
                    return response;
                }
            }

            // make sure a series doesn't already exists for this anime
            var ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
            if (ser != null && !forceOverwrite)
            {
                response.ErrorMessage = "A series already exists for this anime";
                return response;
            }

            // make sure the anime exists first
            var settings = _settingsProvider.GetSettings();
            var command = _commandFactory.Create<CommandRequest_GetAnimeHTTP>(c =>
            {
                c.AnimeID = animeID;
                c.ForceRefresh = false;
                c.DownloadRelations = settings.AutoGroupSeries ||
                                      settings.AniDb.DownloadRelatedAnime;
                c.CreateSeriesEntry = true;
                c.BubbleExceptions = true;
            });
            command.ProcessCommand();
            var anime = command.Result;

            if (anime == null)
            {
                response.ErrorMessage = "Could not get anime information from AniDB";
                return response;
            }

            ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

            // check if we have any group status data for this associated anime
            // if not we will download it now
            if (RepoFactory.AniDB_GroupStatus.GetByAnimeID(anime.AnimeID).Count == 0)
            {
                _commandFactory.CreateAndSave<CommandRequest_GetReleaseGroupStatus>(c => c.AnimeID = anime.AnimeID);
            }

            response.Result = ser.GetUserContract(userID);
            return response;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            response.ErrorMessage = ex.Message;
        }

        return response;
    }

    [HttpPost("AniDB/Anime/Update/{animeID}")]
    public string UpdateAnimeData(int animeID)
    {
        try
        {
            _commandFactory.CreateAndSave<CommandRequest_GetAnimeHTTP_Force>(c =>
            {
                c.AnimeID = animeID;
                c.DownloadRelations = false;
            });
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return string.Empty;
    }

    [HttpPost("AniDB/Anime/GetUpdated/{animeID}")]
    public CL_AniDB_AnimeDetailed GetUpdatedAnimeData(int animeID)
    {
        try
        {
            var command = _commandFactory.Create<CommandRequest_GetAnimeHTTP>(c =>
            {
                c.AnimeID = animeID;
                c.ForceRefresh = true;
                c.DownloadRelations = false;
                c.BubbleExceptions = true;
            });
            command.ProcessCommand();
            var anime = command.Result;

            // update group status information
            _commandFactory.CreateAndSave<CommandRequest_GetReleaseGroupStatus>(
                c =>
                {
                    c.AnimeID = animeID;
                    c.ForceRefresh = true;
                }
            );

            return anime?.Contract;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return null;
    }

    [HttpPost("AniDB/Anime/ExternalLinksFlag/{animeID}/{flags}")]
    public void UpdateAnimeDisableExternalLinksFlag(int animeID, int flags)
    {
        logger.Trace("UpdateAnimeDisableExternalLinksFlag is deprecated.");
    }

    [HttpPost("Group/DefaultSeries/{animeGroupID}/{animeSeriesID}")]
    public void SetDefaultSeriesForGroup(int animeGroupID, int animeSeriesID)
    {
        try
        {
            var grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
            if (grp == null)
            {
                return;
            }

            var ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
            if (ser == null)
            {
                return;
            }

            grp.SetMainSeries(ser);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }
    }

    [HttpDelete("Group/DefaultSeries/{animeGroupID}")]
    public void RemoveDefaultSeriesForGroup(int animeGroupID)
    {
        try
        {
            var grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
            if (grp == null)
            {
                return;
            }

            grp.SetMainSeries(null);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }
    }

    [HttpGet("Group/ForSeries/{animeSeriesID}/{userID}")]
    public CL_AnimeGroup_User GetTopLevelGroupForSeries(int animeSeriesID, int userID)
    {
        try
        {
            return RepoFactory.AnimeSeries.GetByID(animeSeriesID)?.TopLevelAnimeGroup?.GetUserContract(userID);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return null;
    }

    [HttpPost("AniDB/Anime/Ignore/{animeID}/{ignoreType}/{userID}")]
    public void IgnoreAnime(int animeID, int ignoreType, int userID)
    {
        try
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            if (anime == null)
            {
                return;
            }

            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user == null)
            {
                return;
            }

            var ignore = RepoFactory.IgnoreAnime.GetByAnimeUserType(animeID, userID, ignoreType);
            if (ignore != null)
            {
                return; // record already exists
            }

            ignore = new IgnoreAnime { AnimeID = animeID, IgnoreType = ignoreType, JMMUserID = userID };
            RepoFactory.IgnoreAnime.Save(ignore);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }
    }

    [HttpGet("AniDB/Anime/Similar/{animeID}/{userID}")]
    public List<CL_AniDB_Anime_Similar> GetSimilarAnimeLinks(int animeID, int userID)
    {
        var links = new List<CL_AniDB_Anime_Similar>();
        try
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            if (anime == null)
            {
                return links;
            }

            var juser = RepoFactory.JMMUser.GetByID(userID);
            if (juser == null)
            {
                return links;
            }


            foreach (var link in anime.GetSimilarAnime())
            {
                var animeLink = RepoFactory.AniDB_Anime.GetByAnimeID(link.SimilarAnimeID);
                if (animeLink != null)
                {
                    if (!juser.AllowedAnime(animeLink))
                    {
                        continue;
                    }
                }

                // check if this anime has a series
                var ser = RepoFactory.AnimeSeries.GetByAnimeID(link.SimilarAnimeID);

                links.Add(link.ToClient(animeLink, ser, userID));
            }

            return links;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return links;
        }
    }

    [HttpGet("AniDB/Anime/Relation/{animeID}/{userID}")]
    public List<CL_AniDB_Anime_Relation> GetRelatedAnimeLinks(int animeID, int userID)
    {
        var links = new List<CL_AniDB_Anime_Relation>();
        try
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            if (anime == null)
            {
                return links;
            }

            var juser = RepoFactory.JMMUser.GetByID(userID);
            if (juser == null)
            {
                return links;
            }


            foreach (AniDB_Anime_Relation link in anime.GetRelatedAnime())
            {
                var animeLink = RepoFactory.AniDB_Anime.GetByAnimeID(link.RelatedAnimeID);
                if (animeLink != null)
                {
                    if (!juser.AllowedAnime(animeLink))
                    {
                        continue;
                    }
                }

                // check if this anime has a series
                var ser = RepoFactory.AnimeSeries.GetByAnimeID(link.RelatedAnimeID);

                links.Add(link.ToClient(animeLink, ser, userID));
            }

            return links;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return links;
        }
    }

    /// <summary>
    ///     Delete a series, and everything underneath it (episodes, files)
    /// </summary>
    /// <param name="animeSeriesID"></param>
    /// <param name="deleteFiles">also delete the physical files</param>
    /// <returns></returns>
    [HttpDelete("Series/{animeSeriesID}/{deleteFiles}/{deleteParentGroup}")]
    public string DeleteAnimeSeries(int animeSeriesID, bool deleteFiles, bool deleteParentGroup)
    {
        try
        {
            var ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
            if (ser == null)
            {
                return "Series does not exist";
            }

            var animeGroupID = ser.AnimeGroupID;
            var service = HttpContext.RequestServices.GetRequiredService<VideoLocal_PlaceService>();

            foreach (var ep in ser.GetAnimeEpisodes())
            {
                foreach (var vid in ep.GetVideoLocals())
                {
                    var places = vid.Places;
                    for (var index = 0; index < places.Count; index++)
                    {
                        var place = places[index];
                        if (deleteFiles)
                        {
                            try
                            {
                                service.RemoveRecordAndDeletePhysicalFile(place, index >= places.Count - 1);
                            }
                            catch (Exception e)
                            {
                                return e.Message;
                            }
                        }
                        else
                        {
                            service.RemoveRecord(place);
                        }
                    }
                }

                RepoFactory.AnimeEpisode.Delete(ep.AnimeEpisodeID);
            }

            RepoFactory.AnimeSeries.Delete(ser.AnimeSeriesID);

            // finally update stats
            var grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
            if (grp != null)
            {
                if (grp.GetAllSeries().Count == 0)
                {
                    DeleteAnimeGroup(grp.AnimeGroupID, false);
                }
                else
                {
                    grp.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true);
                }
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpGet("AniDB/Anime/{animeID}")]
    public CL_AniDB_Anime GetAnime(int animeID)
    {
        try
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            return anime?.Contract.AniDBAnime;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return null;
    }

    [HttpGet("AniDB/Anime")]
    public List<CL_AniDB_Anime> GetAllAnime()
    {
        try
        {
            return RepoFactory.AniDB_Anime.GetAll().Select(a => a.Contract.AniDBAnime).ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return new List<CL_AniDB_Anime>();
    }

    [HttpGet("AniDB/Anime/Rating/{collectionState}/{watchedState}/{ratingVotedState}/{userID}")]
    public List<CL_AnimeRating> GetAnimeRatings(int collectionState, int watchedState, int ratingVotedState, int userID)
    {
        var contracts = new List<CL_AnimeRating>();

        try
        {
            var series = RepoFactory.AnimeSeries.GetAll();
            var dictSeries = new Dictionary<int, SVR_AnimeSeries>();
            foreach (var ser in series)
            {
                dictSeries[ser.AniDB_ID] = ser;
            }

            var _collectionState = (RatingCollectionState)collectionState;
            var _watchedState = (RatingWatchedState)watchedState;
            var _ratingVotedState = (RatingVotedState)ratingVotedState;

            var animes = RepoFactory.AniDB_Anime.GetAll();

            // user votes
            var allVotes = RepoFactory.AniDB_Vote.GetAll();

            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user == null)
            {
                return contracts;
            }

            var i = 0;


            foreach (var anime in animes)
            {
                i++;

                // evaluate collection states
                if (_collectionState == RatingCollectionState.AllEpisodesInMyCollection)
                {
                    if (!anime.GetFinishedAiring())
                    {
                        continue;
                    }

                    if (!dictSeries.ContainsKey(anime.AnimeID))
                    {
                        continue;
                    }

                    if (dictSeries[anime.AnimeID].MissingEpisodeCount > 0)
                    {
                        continue;
                    }
                }

                if (_collectionState == RatingCollectionState.InMyCollection)
                {
                    if (!dictSeries.ContainsKey(anime.AnimeID))
                    {
                        continue;
                    }
                }

                if (_collectionState == RatingCollectionState.NotInMyCollection)
                {
                    if (dictSeries.ContainsKey(anime.AnimeID))
                    {
                        continue;
                    }
                }

                if (!user.AllowedAnime(anime))
                {
                    continue;
                }

                // evaluate watched states
                if (_watchedState == RatingWatchedState.AllEpisodesWatched)
                {
                    if (!dictSeries.ContainsKey(anime.AnimeID))
                    {
                        continue;
                    }

                    AnimeSeries_User userRec = dictSeries[anime.AnimeID].GetUserRecord(userID);
                    if (userRec == null)
                    {
                        continue;
                    }

                    if (userRec.UnwatchedEpisodeCount > 0)
                    {
                        continue;
                    }
                }

                if (_watchedState == RatingWatchedState.NotWatched)
                {
                    if (dictSeries.ContainsKey(anime.AnimeID))
                    {
                        AnimeSeries_User userRec = dictSeries[anime.AnimeID].GetUserRecord(userID);
                        if (userRec != null)
                        {
                            if (userRec.UnwatchedEpisodeCount == 0)
                            {
                                continue;
                            }
                        }
                    }
                }

                // evaluate voted states
                if (_ratingVotedState == RatingVotedState.Voted)
                {
                    var voted = false;
                    foreach (var vote in allVotes)
                    {
                        if (vote.EntityID == anime.AnimeID &&
                            (vote.VoteType == (int)AniDBVoteType.Anime ||
                             vote.VoteType == (int)AniDBVoteType.AnimeTemp))
                        {
                            voted = true;
                            break;
                        }
                    }

                    if (!voted)
                    {
                        continue;
                    }
                }

                if (_ratingVotedState == RatingVotedState.NotVoted)
                {
                    var voted = false;
                    foreach (var vote in allVotes)
                    {
                        if (vote.EntityID == anime.AnimeID &&
                            (vote.VoteType == (int)AniDBVoteType.Anime ||
                             vote.VoteType == (int)AniDBVoteType.AnimeTemp))
                        {
                            voted = true;
                            break;
                        }
                    }

                    if (voted)
                    {
                        continue;
                    }
                }

                var contract = new CL_AnimeRating { AnimeID = anime.AnimeID, AnimeDetailed = anime.Contract };
                if (dictSeries.ContainsKey(anime.AnimeID))
                {
                    contract.AnimeSeries = dictSeries[anime.AnimeID].GetUserContract(userID);
                }

                contracts.Add(contract);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return contracts;
    }

    [HttpGet("AniDB/Anime/Detailed")]
    public List<CL_AniDB_AnimeDetailed> GetAllAnimeDetailed()
    {
        try
        {
            return RepoFactory.AniDB_Anime.GetAll().Select(a => a.Contract).ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return new List<CL_AniDB_AnimeDetailed>();
    }

    [HttpGet("Series/{userID}")]
    public List<CL_AnimeSeries_User> GetAllSeries(int userID)
    {
        try
        {
            return RepoFactory.AnimeSeries.GetAll().Select(a => a.GetUserContract(userID)).ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return new List<CL_AnimeSeries_User>();
    }

    [HttpGet("AniDB/Anime/Detailed/{animeID}")]
    public CL_AniDB_AnimeDetailed GetAnimeDetailed(int animeID)
    {
        try
        {
            return RepoFactory.AniDB_Anime.GetByAnimeID(animeID)?.Contract;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return null;
        }
    }

    [HttpGet("Group/SubGroup/{animeGroupID}/{userID}")]
    public List<CL_AnimeGroup_User> GetSubGroupsForGroup(int animeGroupID, int userID)
    {
        var retGroups = new List<CL_AnimeGroup_User>();
        try
        {
            var grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
            if (grp == null)
            {
                return retGroups;
            }

            foreach (var grpChild in grp.GetChildGroups())
            {
                var ugrp = grpChild.GetUserContract(userID);
                if (ugrp != null)
                {
                    retGroups.Add(ugrp);
                }
            }

            return retGroups;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return retGroups;
    }

    [HttpGet("Series/ForGroup/{animeGroupID}/{userID}")]
    public List<CL_AnimeSeries_User> GetSeriesForGroup(int animeGroupID, int userID)
    {
        var series = new List<CL_AnimeSeries_User>();
        try
        {
            var grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
            if (grp == null)
            {
                return series;
            }

            foreach (var ser in grp.GetSeries())
            {
                var s = ser.GetUserContract(userID);
                if (s != null)
                {
                    series.Add(s);
                }
            }

            return series;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return series;
        }
    }

    [HttpGet("Series/ForGroupRecursive/{animeGroupID}/{userID}")]
    public List<CL_AnimeSeries_User> GetSeriesForGroupRecursive(int animeGroupID, int userID)
    {
        var series = new List<CL_AnimeSeries_User>();
        try
        {
            var grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
            if (grp == null)
            {
                return series;
            }

            foreach (var ser in grp.GetAllSeries())
            {
                var s = ser.GetUserContract(userID);
                if (s != null)
                {
                    series.Add(s);
                }
            }

            return series;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return series;
        }
    }

    #endregion

    #region Group Filters

    [HttpPost("GroupFilter")]
    public CL_Response<CL_GroupFilter> SaveGroupFilter(CL_GroupFilter contract)
    {
        var response = new CL_Response<CL_GroupFilter> { ErrorMessage = string.Empty, Result = null };

        // Process the group
        FilterPreset gf = null;
        if (contract.GroupFilterID != 0)
        {
            gf = RepoFactory.FilterPreset.GetByID(contract.GroupFilterID);
            if (gf == null)
            {
                response.ErrorMessage = "Could not find existing Group Filter with ID: " +
                                        contract.GroupFilterID;
                return response;
            }
        }

        var legacyConverter = HttpContext.RequestServices.GetRequiredService<LegacyFilterConverter>();
        var newFilter = legacyConverter.FromClient(contract);
        if (gf == null)
        {
            gf = newFilter;
        }
        else
        {
            gf.Name = newFilter.Name;
            gf.Hidden = newFilter.Hidden;
            gf.ApplyAtSeriesLevel = newFilter.ApplyAtSeriesLevel;
            gf.Expression = newFilter.Expression;
            gf.SortingExpression = newFilter.SortingExpression;
        }

        RepoFactory.FilterPreset.Save(gf);

        response.Result = legacyConverter.ToClient(gf);
        return response;
    }

    [HttpDelete("GroupFilter/{groupFilterID}")]
    public string DeleteGroupFilter(int groupFilterID)
    {
        try
        {
            var gf = RepoFactory.FilterPreset.GetByID(groupFilterID);
            if (gf == null) return "Group Filter not found";

            RepoFactory.FilterPreset.Delete(groupFilterID);

            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpGet("GroupFilter/Detailed/{groupFilterID}/{userID}")]
    public CL_GroupFilterExtended GetGroupFilterExtended(int groupFilterID, int userID)
    {
        try
        {
            var gf = RepoFactory.FilterPreset.GetByID(groupFilterID);
            if (gf == null)
            {
                return null;
            }

            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user == null)
            {
                return null;
            }

            var legacyConverter = HttpContext.RequestServices.GetRequiredService<LegacyFilterConverter>();
            var model = legacyConverter.ToClient(gf);
            return new CL_GroupFilterExtended
            {
                GroupFilter = model,
                GroupCount = model.Groups[userID].Count,
                SeriesCount = model.Series[userID].Count
            };
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return null;
    }

    [HttpGet("GroupFilter/Detailed/ForUser/{userID}")]
    public List<CL_GroupFilterExtended> GetAllGroupFiltersExtended(int userID)
    {
        var gfs = new List<CL_GroupFilterExtended>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user == null)
            {
                return gfs;
            }

            var allGfs = RepoFactory.FilterPreset.GetAll();
            var legacyConverter = HttpContext.RequestServices.GetRequiredService<LegacyFilterConverter>();
            gfs = legacyConverter.ToClient(allGfs).Select(a => new CL_GroupFilterExtended
            {
                GroupFilter = a, GroupCount = a.Groups[userID].Count
            }).ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return gfs;
    }

    [HttpGet("GroupFilter/Detailed/ForUser/{userID}/{gfparentid}")]
    public List<CL_GroupFilterExtended> GetGroupFiltersExtended(int userID, int gfparentid = 0)
    {
        var gfs = new List<CL_GroupFilterExtended>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user == null)
            {
                return gfs;
            }

            var allGfs = gfparentid == 0
                ? RepoFactory.FilterPreset.GetTopLevel()
                : RepoFactory.FilterPreset.GetByParentID(gfparentid);
            var legacyConverter = HttpContext.RequestServices.GetRequiredService<LegacyFilterConverter>();
            gfs = legacyConverter.ToClient(allGfs).Select(a => new CL_GroupFilterExtended
            {
                GroupFilter = a, GroupCount = a.Groups.FirstOrDefault().Value.Count
            }).ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return gfs;
    }

    [HttpGet("GroupFilter")]
    public List<CL_GroupFilter> GetAllGroupFilters()
    {
        var gfs = new List<CL_GroupFilter>();
        try
        {
            var start = DateTime.Now;

            var allGfs = RepoFactory.FilterPreset.GetAll();
            var ts = DateTime.Now - start;
            logger.Info("GetAllGroupFilters (Database) in {0} ms", ts.TotalMilliseconds);

            var legacyConverter = HttpContext.RequestServices.GetRequiredService<LegacyFilterConverter>();
            gfs = legacyConverter.ToClient(allGfs)
                .Where(a => a != null)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return gfs;
    }

    [HttpGet("GroupFilter/Parent/{gfparentid}")]
    public List<CL_GroupFilter> GetGroupFilters(int gfparentid = 0)
    {
        var gfs = new List<CL_GroupFilter>();
        try
        {
            var start = DateTime.Now;

            var allGfs = gfparentid == 0 ? RepoFactory.FilterPreset.GetTopLevel() : RepoFactory.FilterPreset.GetByParentID(gfparentid);
            var ts = DateTime.Now - start;
            logger.Info("GetAllGroupFilters (Database) in {0} ms", ts.TotalMilliseconds);
            var legacyConverter = HttpContext.RequestServices.GetRequiredService<LegacyFilterConverter>();
            gfs = legacyConverter.ToClient(allGfs)
                .Where(a => a != null)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return gfs;
    }

    [HttpGet("GroupFilter/{gf}")]
    public CL_GroupFilter GetGroupFilter(int gf)
    {
        try
        {
            var legacyConverter = HttpContext.RequestServices.GetRequiredService<LegacyFilterConverter>();
            return legacyConverter.ToClient(RepoFactory.FilterPreset.GetByID(gf));
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return null;
    }

    [HttpPost("GroupFilter/Evaluate")]
    public CL_GroupFilter EvaluateGroupFilter(CL_GroupFilter contract)
    {
        try
        {
            var legacyConverter = HttpContext.RequestServices.GetRequiredService<LegacyFilterConverter>();
            var filter = legacyConverter.FromClient(contract);
            var model = legacyConverter.ToClient(filter);
            return model;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return new CL_GroupFilter();
        }
    }

    #endregion

    #region Playlists

    [HttpGet("Playlist")]
    public List<Playlist> GetAllPlaylists()
    {
        try
        {
            return RepoFactory.Playlist.GetAll().ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return new List<Playlist>();
    }

    [HttpPost("Playlist")]
    public CL_Response<Playlist> SavePlaylist(Playlist contract)
    {
        var contractRet = new CL_Response<Playlist> { ErrorMessage = string.Empty };
        try
        {
            // Process the playlist
            Playlist pl = null;
            if (contract.PlaylistID != 0)
            {
                pl = RepoFactory.Playlist.GetByID(contract.PlaylistID);
                if (pl == null)
                {
                    contractRet.ErrorMessage = "Could not find existing Playlist with ID: " +
                                               contract.PlaylistID;
                    return contractRet;
                }
            }
            else
            {
                pl = new Playlist();
            }

            if (string.IsNullOrEmpty(contract.PlaylistName))
            {
                contractRet.ErrorMessage = "Playlist must have a name";
                return contractRet;
            }

            pl.DefaultPlayOrder = contract.DefaultPlayOrder;
            pl.PlaylistItems = contract.PlaylistItems;
            pl.PlaylistName = contract.PlaylistName;
            pl.PlayUnwatched = contract.PlayUnwatched;
            pl.PlayWatched = contract.PlayWatched;

            RepoFactory.Playlist.Save(pl);

            contractRet.Result = pl;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            contractRet.ErrorMessage = ex.Message;
            return contractRet;
        }

        return contractRet;
    }

    [HttpDelete("Playlist/{playlistID}")]
    public string DeletePlaylist(int playlistID)
    {
        try
        {
            var pl = RepoFactory.Playlist.GetByID(playlistID);
            if (pl == null)
            {
                return "Playlist not found";
            }

            RepoFactory.Playlist.Delete(playlistID);

            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpGet("Playlist/{playlistID}")]
    public Playlist GetPlaylist(int playlistID)
    {
        try
        {
            return RepoFactory.Playlist.GetByID(playlistID);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return null;
        }
    }

    #endregion

    #region Custom Tags

    [HttpGet("CustomTag")]
    public List<CustomTag> GetAllCustomTags()
    {
        try
        {
            return RepoFactory.CustomTag.GetAll().ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return new List<CustomTag>();
        }
    }

    [HttpPost("CustomTag/CrossRef")]
    public CL_Response<CrossRef_CustomTag> SaveCustomTagCrossRef(CrossRef_CustomTag contract)
    {
        var contractRet = new CL_Response<CrossRef_CustomTag> { ErrorMessage = string.Empty };
        try
        {
            // this is an update
            CrossRef_CustomTag xref = null;
            if (contract.CrossRef_CustomTagID != 0)
            {
                contractRet.ErrorMessage = "Updates are not allowed";
                return contractRet;
            }

            xref = new CrossRef_CustomTag
            {
                CrossRefID = contract.CrossRefID,
                CrossRefType = contract.CrossRefType,
                CustomTagID = contract.CustomTagID
            };

            //TODO: Custom Tags - check if the CustomTagID is valid
            //TODO: Custom Tags - check if the CrossRefID is valid


            RepoFactory.CrossRef_CustomTag.Save(xref);

            contractRet.Result = xref;
            SVR_AniDB_Anime.UpdateStatsByAnimeID(contract.CrossRefID);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            contractRet.ErrorMessage = ex.Message;
            return contractRet;
        }

        return contractRet;
    }

    [HttpDelete("CustomTag/CrossRef/{xrefID}")]
    public string DeleteCustomTagCrossRefByID(int xrefID)
    {
        try
        {
            var pl = RepoFactory.CrossRef_CustomTag.GetByID(xrefID);
            if (pl == null)
            {
                return "Custom Tag not found";
            }

            RepoFactory.CrossRef_CustomTag.Delete(xrefID);

            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpDelete("CustomTag/CrossRef/{customTagID}/{crossRefType}/{crossRefID}")]
    public string DeleteCustomTagCrossRef(int customTagID, int crossRefType, int crossRefID)
    {
        try
        {
            var xrefs =
                RepoFactory.CrossRef_CustomTag.GetByUniqueID(customTagID, crossRefType, crossRefID);

            if (xrefs == null || xrefs.Count == 0)
            {
                return "Custom Tag not found";
            }

            RepoFactory.CrossRef_CustomTag.Delete(xrefs[0].CrossRef_CustomTagID);
            SVR_AniDB_Anime.UpdateStatsByAnimeID(crossRefID);
            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpPost("CustomTag")]
    public CL_Response<CustomTag> SaveCustomTag(CustomTag contract)
    {
        var contractRet = new CL_Response<CustomTag> { ErrorMessage = string.Empty };
        try
        {
            // this is an update
            CustomTag ctag = null;
            if (contract.CustomTagID != 0)
            {
                ctag = RepoFactory.CustomTag.GetByID(contract.CustomTagID);
                if (ctag == null)
                {
                    contractRet.ErrorMessage = "Could not find existing custom tag with ID: " +
                                               contract.CustomTagID;
                    return contractRet;
                }
            }
            else
            {
                ctag = new CustomTag();
            }

            if (string.IsNullOrEmpty(contract.TagName))
            {
                contractRet.ErrorMessage = "Custom Tag must have a name";
                return contractRet;
            }

            ctag.TagName = contract.TagName;
            ctag.TagDescription = contract.TagDescription;

            RepoFactory.CustomTag.Save(ctag);

            contractRet.Result = ctag;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            contractRet.ErrorMessage = ex.Message;
            return contractRet;
        }

        return contractRet;
    }

    [HttpDelete("CustomTag/{customTagID}")]
    public string DeleteCustomTag(int customTagID)
    {
        try
        {
            var pl = RepoFactory.CustomTag.GetByID(customTagID);
            if (pl == null)
            {
                return "Custom Tag not found";
            }

            // first get a list of all the anime that referenced this tag
            var xrefs = RepoFactory.CrossRef_CustomTag.GetByCustomTagID(customTagID);

            RepoFactory.CustomTag.Delete(customTagID);

            // update cached data for any anime that were affected
            foreach (var xref in xrefs)
            {
                SVR_AniDB_Anime.UpdateStatsByAnimeID(xref.CrossRefID);
            }


            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }
    }

    [HttpGet("CustomTag/{customTagID}")]
    public CustomTag GetCustomTag(int customTagID)
    {
        try
        {
            return RepoFactory.CustomTag.GetByID(customTagID);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return null;
        }
    }

    #endregion

    #region Users

    [HttpGet("User")]
    public List<JMMUser> GetAllUsers()
    {
        try
        {
            return RepoFactory.JMMUser.GetAll().Cast<JMMUser>().ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return new List<JMMUser>();
        }
    }

    [HttpPost("User/{username}")]
    public JMMUser AuthenticateUser(string username, [FromForm] string password)
    {
        try
        {
            username = username.Replace("+", " ");
            return RepoFactory.JMMUser.AuthenticateUser(username, password);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return null;
        }
    }

    [HttpPost("User/ChangePassword/{userID}")]
    public string ChangePassword(int userID, [FromForm] string newPassword)
    {
        return ChangePassword(userID, newPassword, true);
    }

    [NonAction]
    public string ChangePassword(int userID, string newPassword, bool revokeapikey)
    {
        try
        {
            var jmmUser = RepoFactory.JMMUser.GetByID(userID);
            if (jmmUser == null)
            {
                return "User not found";
            }

            jmmUser.Password = Digest.Hash(newPassword);
            RepoFactory.JMMUser.Save(jmmUser);
            if (revokeapikey)
            {
                RepoFactory.AuthTokens.DeleteAllWithUserID(jmmUser.JMMUserID);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }

        return string.Empty;
    }

    [HttpPost("User")]
    public string SaveUser(JMMUser user)
    {
        try
        {
            var existingUser = false;
            var updateStats = false;
            var updateGf = false;
            SVR_JMMUser jmmUser = null;
            if (user.JMMUserID != 0)
            {
                jmmUser = RepoFactory.JMMUser.GetByID(user.JMMUserID);
                if (jmmUser == null)
                {
                    return "User not found";
                }

                existingUser = true;
            }
            else
            {
                jmmUser = new SVR_JMMUser();
                updateStats = true;
                updateGf = true;
            }

            if (existingUser && jmmUser.IsAniDBUser != user.IsAniDBUser)
            {
                updateStats = true;
            }

            var hcat = string.Join(",", user.HideCategories);
            if (jmmUser.HideCategories != hcat)
            {
                updateGf = true;
            }

            jmmUser.HideCategories = hcat;
            jmmUser.IsAniDBUser = user.IsAniDBUser;
            jmmUser.IsTraktUser = user.IsTraktUser;
            jmmUser.IsAdmin = user.IsAdmin;
            jmmUser.Username = user.Username;
            jmmUser.CanEditServerSettings = user.CanEditServerSettings;
            jmmUser.PlexUsers = user.PlexUsers;
            jmmUser.PlexToken = user.PlexToken;
            if (string.IsNullOrEmpty(user.Password))
            {
                jmmUser.Password = string.Empty;
            }
            else
            {
                // Additional check for hashed password, if not hashed we hash it
                if (user.Password.Length < 64)
                {
                    jmmUser.Password = Digest.Hash(user.Password);
                }
                else
                {
                    jmmUser.Password = user.Password;
                }
            }

            // make sure that at least one user is an admin
            if (jmmUser.IsAdmin == 0)
            {
                var adminExists = false;
                var users = RepoFactory.JMMUser.GetAll();
                foreach (var userOld in users)
                {
                    if (userOld.IsAdmin == 1)
                    {
                        if (existingUser)
                        {
                            if (userOld.JMMUserID != jmmUser.JMMUserID)
                            {
                                adminExists = true;
                            }
                        }
                        else
                        {
                            //one admin account is needed
                            adminExists = true;
                            break;
                        }
                    }
                }

                if (!adminExists)
                {
                    return "At least one user must be an administrator";
                }
            }

            RepoFactory.JMMUser.Save(jmmUser);

            // update stats
            if (updateStats)
            {
                foreach (var ser in RepoFactory.AnimeSeries.GetAll())
                {
                    ser.QueueUpdateStats();
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }

        return string.Empty;
    }

    [HttpDelete("User")]
    public string DeleteUser(int userID)
    {
        try
        {
            var jmmUser = RepoFactory.JMMUser.GetByID(userID);
            if (jmmUser == null)
            {
                return "User not found";
            }

            // make sure that at least one user is an admin
            if (jmmUser.IsAdmin == 1)
            {
                var adminExists = false;
                var users = RepoFactory.JMMUser.GetAll();
                foreach (var userOld in users)
                {
                    if (userOld.IsAdmin == 1)
                    {
                        if (userOld.JMMUserID != jmmUser.JMMUserID)
                        {
                            adminExists = true;
                        }
                    }
                }

                if (!adminExists)
                {
                    return "At least one user must be an administrator";
                }
            }

            RepoFactory.JMMUser.Delete(userID);

            // delete all user records
            RepoFactory.AnimeSeries_User.Delete(RepoFactory.AnimeSeries_User.GetByUserID(userID));
            RepoFactory.AnimeGroup_User.Delete(RepoFactory.AnimeGroup_User.GetByUserID(userID));
            RepoFactory.AnimeEpisode_User.Delete(RepoFactory.AnimeEpisode_User.GetByUserID(userID));
            RepoFactory.VideoLocalUser.Delete(RepoFactory.VideoLocalUser.GetByUserID(userID));
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }

        return string.Empty;
    }

    #endregion

    #region Import Folders

    [HttpGet("Folder")]
    public List<ImportFolder> GetImportFolders()
    {
        try
        {
            return RepoFactory.ImportFolder.GetAll().Cast<ImportFolder>().ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return new List<ImportFolder>();
    }

    [HttpPost("Folder")]
    public CL_Response<ImportFolder> SaveImportFolder(ImportFolder contract)
    {
        var folder = new CL_Response<ImportFolder>();
        try
        {
            folder.Result = RepoFactory.ImportFolder.SaveImportFolder(contract);
        }
        catch (Exception e)
        {
            logger.Error(e);
            folder.ErrorMessage = e.Message;
        }

        return folder;
    }

    [HttpDelete("Folder/{importFolderID}")]
    public string DeleteImportFolder(int importFolderID)
    {
        Utils.ShokoServer.DeleteImportFolder(importFolderID);
        return string.Empty;
    }

    #endregion
}
