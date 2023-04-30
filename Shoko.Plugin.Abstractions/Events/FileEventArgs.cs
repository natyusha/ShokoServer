using System;
using Shoko.Plugin.Abstractions.DataModels;

#nullable enable
namespace Shoko.Plugin.Abstractions.Events;

public class FileEventArgs : EventArgs
{
    /// <summary>
    /// The relative path of the file from the ImportFolder base location
    /// </summary>
    public string RelativePath { get; set; }

    /// <summary>
    /// The import folder that the file is in
    /// </summary>
    public IImportFolder ImportFolder { get; set; }

    /// <summary>
    /// The video for the file location related to this particular event.
    /// </summary>
    public IShokoVideo Video { get; set; }

    /// <summary>
    /// Either the best matching location, or the location for this particular event.
    /// </summary>
    public IShokoVideoFileLocation FileLocation { get; set; }

    public FileEventArgs(IShokoVideoFileLocation fileLocation) : base()
    {
        ImportFolder = fileLocation.ImportFolder;
        RelativePath = fileLocation.RelativePath;
        Video = fileLocation.Video;
        FileLocation = fileLocation;
    }

    public FileEventArgs(IShokoVideoFileLocation fileLocation, string relativePath, IImportFolder importFolder) : base()
    {
        ImportFolder = importFolder;
        RelativePath = relativePath;
        Video = fileLocation.Video;
        FileLocation = fileLocation;
    }
}
