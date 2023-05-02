using Shoko.Plugin.Abstractions.Models;

namespace Shoko.Plugin.Abstractions.Events;

public class FileDeletedEventArgs : FileEventArgs
{
    public FileDeletedEventArgs(IShokoVideoFileLocation fileLocation, string relativePath, IImportFolder importFolder) : base(fileLocation, relativePath, importFolder) { }
}
