﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Quartz;
using QuartzJobFactory.Attributes;
using Shoko.Server.Scheduling.Acquisition.Attributes;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[JobKeyMember("DeleteImportFolder")]
[JobKeyGroup(JobKeyGroup.Actions)]
internal class DeleteImportFolderJob : IJob
{
    public int ImportFolderID { get; set; }

    public Task Execute(IJobExecutionContext context)
    {
        try
        {
            Importer.DeleteImportFolder(ImportFolderID);
        }
        catch (Exception ex)
        {
            //logger.Error(ex, ex.ToString());
            // do you want the job to refire?
            throw new JobExecutionException(msg: "", refireImmediately: false, cause: ex);
        }

        return Task.CompletedTask;
    }
}
