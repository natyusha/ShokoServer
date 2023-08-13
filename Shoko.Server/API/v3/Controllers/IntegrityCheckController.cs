using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models.Internal;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class IntegrityCheckController : BaseController
{
    [HttpPost]
    public ActionResult<IntegrityScan> AddScan([FromBody] IntegrityScan.Input.NewIntegrityScanBody body)
    {
        var scan = new Scan
        {
            Status = ScanStatus.Standby,
            ImportFolders = body.ImportFolders,
            CreatedAt = DateTime.Now
        };

        RepoFactory.Scan.Save(s);
        var files = scan.ImportFolders
            .SelectMany(a => RepoFactory.Shoko_Video_Location.GetByImportFolderId(a))
            .Select(p => new { p, v = p.Video })
            .Select(t => new ScanFile(scan, t.v, t.p))
            .ToList();
        RepoFactory.ScanFile.Save(files);

        return new IntegrityScan(scan);
    }

    [HttpGet("{id}/Start")]
    public ActionResult StartScan(int id)
    {
        return Ok();
    }

    public IntegrityCheckController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
