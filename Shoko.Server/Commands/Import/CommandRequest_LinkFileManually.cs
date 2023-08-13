using System;
using System.Linq;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Models;
using Shoko.Server.Models.CrossReferences;
using Shoko.Server.Models.Internal;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Server.Enums;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands;

[Serializable]
[Command(CommandRequestType.LinkFileManually)]
public class CommandRequest_LinkFileManually : CommandRequestImplementation
{
    private readonly ICommandRequestFactory _commandFactory;
    private readonly IServerSettings _settings;
    public int VideoId { get; set; }
    public int EpisodeId { get; set; }
    public int Percentage { get; set; }

    private Shoko_Episode Episode;

    private Shoko_Video Video;

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority3;

    public override QueueStateStruct PrettyDescription
    {
        get
        {
            if (Video != null && Episode != null)
            {
                return new QueueStateStruct
                {
                    message = "Linking File: {0} to Episode: {1}",
                    queueState = QueueStateEnum.LinkFileManually,
                    extraParams = new[] { Video.FileName, Episode.Title }
                };
            }

            return new QueueStateStruct
            {
                message = "Linking File: {0} to Episode: {1}",
                queueState = QueueStateEnum.LinkFileManually,
                extraParams = new[] { VideoId.ToString(), EpisodeId.ToString() }
            };
        }
    }

    protected override void Process()
    {
        var xref = new CR_Video_Episode
        {
            Hash = Video.ED2KHash,
            FileName = Video.FileName,
            FileSize = Video.FileSize,
            CrossRefSource = (int)CrossRefSource.User,
            AnimeID = Episode.AniDB_Episode.AnimeID,
            EpisodeID = Episode.AniDB_EpisodeID,
            Percentage = Percentage is > 0 and <= 100 ? Percentage : 100,
            EpisodeOrder = 1
        };

        RepoFactory.CR_Video_Episode.Save(xref);

        ProcessFileQualityFilter();

        Video.Places.ForEach(a => { a.RenameAndMoveAsRequired(); });

        // Set the import date.
        Video.DateTimeImported = DateTime.Now;
        RepoFactory.Shoko_Video.Save(Video);

        var ser = Episode.GetAnimeSeries();
        ser.EpisodeAddedDate = DateTime.Now;
        RepoFactory.Shoko_Series.Save(ser, false, true);

        //Update will re-save
        ser.QueueUpdateStats();

        foreach (var grp in ser.AllGroupsAbove)
        {
            grp.EpisodeAddedDate = DateTime.Now;
            RepoFactory.Shoko_Group.Save(grp, false, false);
        }

        ShokoEventHandler.Instance.OnFileMatched(Video.GetPreferredLocation());

        if (_settings.AniDb.MyList_AddFiles)
        {
            var cmdAddFile = _commandFactory.Create<CommandRequest_AddFileToMyList>(c => c.Hash = Video.Hash);
            cmdAddFile.Save();
        }
    }

    private void ProcessFileQualityFilter()
    {
        if (!_settings.FileQualityFilterEnabled) return;

        var videoLocals = Episode.GetVideos().ToList();
        if (videoLocals == null) return;

        videoLocals.Sort(FileQualityFilter.CompareTo);
        var keep = videoLocals.Take(_settings.FileQualityPreferences.MaxNumberOfFilesToKeep).ToList();
        foreach (var vl2 in keep) videoLocals.Remove(vl2);

        if (videoLocals.Contains(Video)) videoLocals.Remove(Video);

        videoLocals = videoLocals.Where(FileQualityFilter.ShouldRemoveVideo).ToList();

        foreach (var toDelete in videoLocals) toDelete.Places.ForEach(a => a.RemoveRecordAndDeletePhysicalFile());
    }

    /// <summary>
    /// This should generate a unique key for a command
    /// It will be used to check whether the command has already been queued before adding it
    /// </summary>
    public override void GenerateCommandID()
    {
        CommandID = $"CommandRequest_LinkFileManually_{VideoId}_{EpisodeId}";
    }

    public override bool LoadFromDBCommand(CommandRequest cq)
    {
        CommandID = cq.CommandID;
        CommandRequestID = cq.CommandRequestID;
        Priority = cq.Priority;
        CommandDetails = cq.CommandDetails;
        DateTimeUpdated = cq.DateTimeUpdated;

        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        VideoId = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkFileManually", "VideoId"));
        EpisodeId = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkFileManually", "EpisodeId"));
        Percentage = int.Parse(TryGetProperty(docCreator, "CommandRequest_LinkFileManually", "Percentage"));
        Video = RepoFactory.Shoko_Video.GetByID(VideoId);
        if (Video == null)
        {
            Logger.LogWarning("VideoLocal object {VideoLocalID} not found", VideoId);
            return false;
        }

        Episode = RepoFactory.Shoko_Episode.GetByID(EpisodeId);
        if (Episode?.Series == null)
        {
            Logger.LogWarning("Local Episode or Series object {EpisodeID} not found", EpisodeId);
            return false;
        }

        return true;
    }

    public override CommandRequest ToDatabaseObject()
    {
        GenerateCommandID();

        var cq = new CommandRequest
        {
            CommandID = CommandID,
            CommandType = CommandType,
            Priority = Priority,
            CommandDetails = ToXML(),
            DateTimeUpdated = DateTime.Now
        };
        return cq;
    }

    public CommandRequest_LinkFileManually(ILoggerFactory loggerFactory, ICommandRequestFactory commandFactory, ISettingsProvider settingsProvider) :
        base(loggerFactory)
    {
        _commandFactory = commandFactory;
        _settings = settingsProvider.GetSettings();
    }

    protected CommandRequest_LinkFileManually()
    {
    }
}
