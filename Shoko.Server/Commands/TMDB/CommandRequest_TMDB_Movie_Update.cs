using System;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.TMDB_Movie_Update)]
public class CommandRequest_TMDB_Movie_Update : CommandRequestImplementation
{
    private readonly TMDBHelper _helper;
    public virtual int TmdbMovieID { get; set; }

    public virtual bool DownloadImages { get; set; }

    public virtual bool ForceRefresh { get; set; }

    public virtual string MovieTitle { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Updating TMDB Movie: {0}",
        queueState = QueueStateEnum.GettingTvDBSeries,
        extraParams = new[] { string.IsNullOrEmpty(MovieTitle) ? TmdbMovieID.ToString() : $"{MovieTitle} ({TmdbMovieID})" }
    };

    public override void PostInit()
    {
        MovieTitle = RepoFactory.MovieDb_Movie.GetByOnlineID(TmdbMovieID)?.MovieName;
    }

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_TMDB_Movie_Update: {TmdbMovieId}", TmdbMovieID);
        _helper.UpdateMovie(TmdbMovieID, ForceRefresh, DownloadImages);
    }

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_TMDB_Movie_Update_{TmdbMovieID}";
    }

    protected override bool Load()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        TmdbMovieID = int.Parse(docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Movie_Update), nameof(TmdbMovieID)));
        ForceRefresh = bool.Parse(docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Movie_Update), nameof(ForceRefresh)));
        DownloadImages = bool.Parse(docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Movie_Update), nameof(DownloadImages)));
        MovieTitle = docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Movie_Update), nameof(MovieTitle));

        return true;
    }

    public CommandRequest_TMDB_Movie_Update(ILoggerFactory loggerFactory, TMDBHelper helper) : base(loggerFactory)
    {
        _helper = helper;
    }

    protected CommandRequest_TMDB_Movie_Update()
    {
    }
}
