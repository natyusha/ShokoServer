using System;
using System.IO;
using Shoko.Plugin.Abstractions.Models;

namespace Shoko.Plugin.Abstractions.Events;

public class FileDetectedEventArgs : EventArgs
{
    /// <summary>
    /// The relative path of the file from the root of the import folder.
    /// </summary>
    public string RelativePath { get; set; }

    /// <summary>
    /// The import folder that the file is in
    /// </summary>
    public IImportFolder ImportFolder { get; set; }

    /// <summary>
    /// FileInfo for the file, since this event is fired before a file location
    /// has been assigned to the on-disk file.
    /// </summary>
    public FileInfo FileInfo { get; set; }

    public FileDetectedEventArgs(FileInfo fileInfo, string relativePath, IImportFolder importFolder) : base()
    {
        RelativePath = relativePath;
        ImportFolder = importFolder;
        FileInfo = fileInfo;
    }
}
