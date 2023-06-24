using System;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.Shoko;

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
    public IShokoVideoLocation VidoLocation { get; set; }

    public FileEventArgs(IShokoVideoLocation fileLocation) : base()
    {
        ImportFolder = fileLocation.ImportFolder;
        RelativePath = fileLocation.RelativePath;
        Video = fileLocation.Video;
        VidoLocation = fileLocation;
    }

    public FileEventArgs(IShokoVideoLocation fileLocation, string relativePath, IImportFolder importFolder) : base()
    {
        ImportFolder = importFolder;
        RelativePath = relativePath;
        Video = fileLocation.Video;
        VidoLocation = fileLocation;
    }
}
