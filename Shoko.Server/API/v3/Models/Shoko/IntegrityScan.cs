
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.Enums;
using Shoko.Server.Models.Internal;

namespace Shoko.Server.API.v3.Models.Shoko;

public class IntegrityScan
{
    public int ID { get; }
    
    [JsonConverter(typeof(StringEnumConverter))]
    public ScanStatus Status { get; }

    public ISet<int> ImportFolders { get; }

    public DateTime CreatedAt { get; }

    public IntegrityScan(Scan scan)
    {
        ID = scan.Id;
        Status = scan.Status;
        ImportFolders = scan.ImportFolders;
        CreatedAt = scan.CreatedAt;
    }
    
    public static class Input
    {
        public class NewIntegrityScanBody
        {
            [Required]
            [MinLength(1)]
            public HashSet<int> ImportFolders { get; set; } = new();
        }
    }
    
    public class IntegrityScanFile
    {
        public int ID { get; }
        
        public int ScanID { get; }
        
        public DateTime? CheckedAt { get; }
        
        public DateTime CreatedAt { get; }
    }
}
