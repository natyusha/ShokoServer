using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.Internal;

public class Scan
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public HashSet<int> ImportFolders { get; set; }

    public ScanStatus Status { get; set; }

    public string StatusText
    {
        get => Status switch
        {
            ScanStatus.Finish => "Finished",
            ScanStatus.Running => "Running",
            _ => "Standby",
        };
    }
}
