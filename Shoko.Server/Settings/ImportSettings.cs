using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Shoko.Models.Enums;

namespace Shoko.Server.Settings;

public class ImportSettings
{
    public HasherSettings Hasher { get; set; } = new();
    public List<string> VideoExtensions { get; set; } = new()
    {
        "MKV",
        "AVI",
        "MP4",
        "MOV",
        "OGM",
        "WMV",
        "MPG",
        "MPEG",
        "MK3D",
        "M4V"
    };

    public List<string> Exclude { get; set; } = new()
    {
        @"[\\\/]\$RECYCLE\.BIN[\\\/]", @"[\\\/]\.Recycle\.Bin[\\\/]", @"[\\\/]\.Trash-\d+[\\\/]"
    };

    public bool RunOnStart { get; set; } = false;

    /// <summary>
    /// Max auto-scan attempts per file for unrecognized files.
    /// </summary>
    [Range(0, 100)]
    public int MaxAutoScanAttemptsPerFile { get; set; } = 15;

    public bool ScanDropFoldersOnStart { get; set; } = false;

    [JsonIgnore]
    [Obsolete("Use Hasher.CRC")]
    public bool Hash_CRC32
    {
        get => Hasher.CRC;
        set => Hasher.CRC = value;
    }

    [Obsolete("Use Hasher.MD5")]
    [JsonIgnore]
    public bool Hash_MD5
    {
        get => Hasher.MD5;
        set => Hasher.MD5 = value;
    }

    [Obsolete("Use Hasher.SHA1")]
    [JsonIgnore]
    public bool Hash_SHA1
    {
        get => Hasher.SHA1;
        set => Hasher.SHA1 = value;
    }

    public bool UseExistingFileWatchedStatus { get; set; } = true;

    public bool AutomaticallyDeleteDuplicatesOnImport { get; set; } = false;

    public bool FileLockChecking { get; set; } = true;

    public bool AggressiveFileLockChecking { get; set; } = true;

    public int FileLockWaitTimeMS { get; set; } = 4000;

    public int AggressiveFileLockWaitTimeSeconds { get; set; } = 8;

    public bool SkipDiskSpaceChecks { get; set; }

    public bool RenameThenMove { get; set; }

    public bool RenameOnImport { get; set; } = false;
    public bool MoveOnImport { get; set; } = false;

    public string MediaInfoPath { get; set; }

    public int MediaInfoTimeoutMinutes { get; set; } = 5;
}
