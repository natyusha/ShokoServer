
using System;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
[Command(CommandRequestType.TMDB_Show_DownloadImages)]
public class CommandRequest_TMDB_Show_DownloadImages : CommandRequestImplementation
{
    [XmlIgnore, JsonIgnore]
    private readonly TMDBHelper _helper;

    public virtual int TmdbShowID { get; set; }

    public virtual bool ForceDownload { get; set; } = true;

    public virtual string ShowTitle { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

    public override QueueStateStruct PrettyDescription =>
        new()
        {
            message = "Downloadimg Images for TMDB Show: {0}",
            queueState = QueueStateEnum.GettingTvDBSeries,
            extraParams = new[] { string.IsNullOrEmpty(ShowTitle) ? TmdbShowID.ToString() : $"{ShowTitle} ({TmdbShowID})" }
        };

    public override void PostInit()
    {
        ShowTitle ??= RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID)?.EnglishTitle;
    }

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_TMDB_Show_DownloadImages: {TmdbShowId}", TmdbShowID);
        Task.Run(() => _helper.DownloadShowImages(TmdbShowID, ForceDownload))
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_TMDB_Show_DownloadImages_{TmdbShowID}";
    }

    protected override bool Load()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        TmdbShowID = int.Parse(docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Show_DownloadImages), nameof(TmdbShowID)));
        ForceDownload = bool.Parse(docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Show_DownloadImages), nameof(ForceDownload)));
        ShowTitle = docCreator.TryGetProperty(nameof(CommandRequest_TMDB_Show_DownloadImages), nameof(ShowTitle));

        return true;
    }

    public CommandRequest_TMDB_Show_DownloadImages(ILoggerFactory loggerFactory, TMDBHelper helper) : base(loggerFactory)
    {
        _helper = helper;
    }

    protected CommandRequest_TMDB_Show_DownloadImages()
    {
    }
}
