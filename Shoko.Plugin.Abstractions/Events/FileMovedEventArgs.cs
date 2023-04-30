using Shoko.Plugin.Abstractions.DataModels;

#nullable enable
namespace Shoko.Plugin.Abstractions.Events;

public class FileMovedEventArgs : FileEventArgs
{
    /// <summary>
    /// The old relative path relative to the root of the old import folder.
    /// /// </summary>
    public string OldRelativePath { get; set; }

    /// <summary>
    /// The old import folder for the file location.
    /// </summary>
    public IImportFolder OldImportFolder { get; set; }

    public FileMovedEventArgs(IShokoVideoFileLocation fileLocation, string newRelativePath, IImportFolder newImportFolder, string oldRelativePath, IImportFolder oldImportFolder) : base(fileLocation, newRelativePath, newImportFolder)
    {
        OldRelativePath = oldRelativePath;
        OldImportFolder = oldImportFolder;
    }
}
