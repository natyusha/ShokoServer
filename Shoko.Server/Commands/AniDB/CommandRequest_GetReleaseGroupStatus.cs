﻿using System;
using System.Linq;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands.AniDB;

[Serializable]
[Command(CommandRequestType.AniDB_GetReleaseGroupStatus)]
public class CommandRequest_GetReleaseGroupStatus : CommandRequestImplementation
{
    private readonly IRequestFactory _requestFactory;
    private readonly ICommandRequestFactory _commandFactory;
    private readonly ISettingsProvider _settingsProvider;
    public virtual int AnimeID { get; set; }
    public virtual bool ForceRefresh { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority5;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Getting group status info from UDP API for Anime: {0}",
        queueState = QueueStateEnum.GetReleaseGroup,
        extraParams = new[] { AnimeID.ToString() }
    };

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_GetReleaseGroupStatus: {AnimeID}", AnimeID);

        // only get group status if we have an associated series
        var series = RepoFactory.AnimeSeries.GetByAnimeID(AnimeID);
        if (series == null) return;

        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
        if (anime == null) return;

        // don't get group status if the anime has already ended more than 50 days ago
        if (ShouldSkip(anime))
        {
            Logger.LogInformation("Skipping group status command because anime has already ended: {AnimeID}",
                AnimeID);
            return;
        }

        var request = _requestFactory.Create<RequestReleaseGroupStatus>(r => r.AnimeID = AnimeID);
        var response = request.Execute();
        if (response.Response == null) return;

        var maxEpisode = response.Response.Max(a => a.LastEpisodeNumber);

        // delete existing records
        RepoFactory.AniDB_GroupStatus.DeleteForAnime(AnimeID);

        // save the records
        var toSave = response.Response.Select(
            raw => new AniDB_GroupStatus
            {
                AnimeID = raw.AnimeID,
                GroupID = raw.GroupID,
                GroupName = raw.GroupName,
                CompletionState = (int)raw.CompletionState,
                LastEpisodeNumber = raw.LastEpisodeNumber,
                Rating = raw.Rating,
                Votes = raw.Votes,
                EpisodeRange = string.Join(',', raw.ReleasedEpisodes)
            }
        ).ToArray();
        RepoFactory.AniDB_GroupStatus.Save(toSave);

        var settings = _settingsProvider.GetSettings();
        if (maxEpisode > 0)
        {
            // update the anime with a record of the latest subbed episode
            anime.LatestEpisodeNumber = maxEpisode;
            RepoFactory.AniDB_Anime.Save(anime, false);

            // check if we have this episode in the database
            // if not get it now by updating the anime record
            var eps = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeNumber(AnimeID, maxEpisode);
            if (eps.Count == 0)
            {
                _commandFactory.CreateAndSave<CommandRequest_GetAnimeHTTP>(c =>
                {
                    c.AnimeID = AnimeID;
                    c.ForceRefresh = true;
                    c.CreateSeriesEntry = settings.AniDb.AutomaticallyImportSeries;
                });
            }

            // update the missing episode stats on groups and children
            series.QueueUpdateStats();
        }

        if (settings.AniDb.DownloadReleaseGroups && response is { Response.Count: > 0 })
        {
            // shouldn't need the where, but better safe than sorry.
            response.Response.DistinctBy(a => a.GroupID).Where(a => a.GroupID != 0).ForEach(a =>
                _commandFactory.CreateAndSave<CommandRequest_GetReleaseGroup>(c => c.GroupID = a.GroupID));
        }
    }

    private bool ShouldSkip(SVR_AniDB_Anime anime)
    {
        if (ForceRefresh)
        {
            return false;
        }

        if (!anime.EndDate.HasValue)
        {
            return false;
        }

        if (anime.EndDate.Value >= DateTime.Now)
        {
            return false;
        }

        var ts = DateTime.Now - anime.EndDate.Value;
        if (!(ts.TotalDays > 50))
        {
            return false;
        }

        // don't skip if we have never downloaded this info before
        var grpStatuses = RepoFactory.AniDB_GroupStatus.GetByAnimeID(AnimeID);
        return grpStatuses is { Count: > 0 };
    }

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_GetReleaseGroupStatus_{AnimeID}";
    }

    protected override bool Load()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        AnimeID = int.Parse(docCreator.TryGetProperty("CommandRequest_GetReleaseGroupStatus", "AnimeID"));
        ForceRefresh =
            bool.Parse(docCreator.TryGetProperty("CommandRequest_GetReleaseGroupStatus", "ForceRefresh"));

        return true;
    }

    public CommandRequest_GetReleaseGroupStatus(ILoggerFactory loggerFactory, IRequestFactory requestFactory,
        ICommandRequestFactory commandFactory, ISettingsProvider settingsProvider) : base(loggerFactory)
    {
        _requestFactory = requestFactory;
        _commandFactory = commandFactory;
        _settingsProvider = settingsProvider;
    }

    protected CommandRequest_GetReleaseGroupStatus()
    {
    }
}
