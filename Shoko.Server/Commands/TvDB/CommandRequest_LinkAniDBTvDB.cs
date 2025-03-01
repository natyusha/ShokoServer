﻿using System;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Models;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands.TvDB;

[Serializable]
[Command(CommandRequestType.LinkAniDBTvDB)]
public class CommandRequest_LinkAniDBTvDB : CommandRequestImplementation
{
    private readonly TvDBApiHelper _helper;
    public virtual int AnimeID { get; set; }
    public virtual int TvDBID { get; set; }
    public virtual bool AdditiveLink { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority5;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Updating Changed TvDB association: {0}",
        queueState = QueueStateEnum.LinkAniDBTvDB,
        extraParams = new[] { AnimeID.ToString() }
    };

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_LinkAniDBTvDB: {0}", AnimeID);
        _helper.LinkAniDBTvDB(AnimeID, TvDBID, AdditiveLink);
        SVR_AniDB_Anime.UpdateStatsByAnimeID(AnimeID);
    }

    public override void GenerateCommandID()
    {
        CommandID =
            $"CommandRequest_LinkAniDBTvDB_{AnimeID}_{TvDBID}";
    }

    protected override bool Load()
    {
        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        AnimeID = int.Parse(docCreator.TryGetProperty("CommandRequest_LinkAniDBTvDB", "animeID"));
        TvDBID = int.Parse(docCreator.TryGetProperty("CommandRequest_LinkAniDBTvDB", "tvDBID"));
        AdditiveLink = bool.Parse(docCreator.TryGetProperty("CommandRequest_LinkAniDBTvDB", "additiveLink"));

        return true;
    }

    public CommandRequest_LinkAniDBTvDB(ILoggerFactory loggerFactory, TvDBApiHelper helper) : base(loggerFactory)
    {
        _helper = helper;
    }

    protected CommandRequest_LinkAniDBTvDB()
    {
    }
}
