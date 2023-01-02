﻿using System;
using System.ComponentModel;
using System.Threading;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands;

public class CommandProcessorImages : CommandProcessor
{
    protected override void UpdateQueueCount()
    {
        QueueCount = RepoFactory.CommandRequest.GetQueuedCommandCountImages();
    }

    protected override string QueueType { get; } = "Image";

    protected override void UpdatePause(bool pauseState)
    {
        ServerInfo.Instance.ImagesQueuePaused = pauseState;
        ServerInfo.Instance.ImagesQueueRunning = !pauseState;
    }

    public override void Init(IServiceProvider provider)
    {
        base.Init(provider);
        QueueState = new QueueStateStruct
        {
            message = "Starting image downloading command worker",
            queueState = QueueStateEnum.StartingImages,
            extraParams = new string[0]
        };
    }

    protected override void WorkerCommands_DoWork(object sender, DoWorkEventArgs e)
    {
        while (true)
        {
            try
            {
                if (WorkerCommands.CancellationPending) return;

                // if paused we will sleep for 5 seconds, and the try again
                if (Paused)
                {
                    try
                    {
                        if (WorkerCommands.CancellationPending) return;
                    }
                    catch
                    {
                        // ignore
                    }

                    Thread.Sleep(200);
                    continue;
                }

                var crdb = RepoFactory.CommandRequest.GetNextDBCommandRequestImages();
                if (crdb == null) return;

                if (WorkerCommands.CancellationPending) return;

                var icr = CommandHelper.GetCommand(ServiceProvider, crdb);
                if (icr != null)
                {
                    if (WorkerCommands.CancellationPending) return;

                    QueueState = icr.PrettyDescription;

                    try
                    {
                        CurrentCommand = crdb;
                        icr.ProcessCommand();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "ProcessCommand exception: {CommandID}", crdb.CommandID);
                    }
                    finally
                    {
                        CurrentCommand = null;
                    }
                }

                RepoFactory.CommandRequest.Delete(crdb.CommandRequestID);
                UpdateQueueCount();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, "Error Processing Commands");
            }
        }
    }
}
