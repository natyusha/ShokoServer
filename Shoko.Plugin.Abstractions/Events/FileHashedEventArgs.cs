using Shoko.Plugin.Abstractions.Models;

namespace Shoko.Plugin.Abstractions.Events;

public class FileHashedEventArgs : FileEventArgs
{
    public FileHashedEventArgs(IShokoVideoFileLocation fileLocation) : base(fileLocation) { }
    public FileHashedEventArgs(IShokoVideoFileLocation fileLocation, string relativePath, IImportFolder importFolder) : base(fileLocation, relativePath, importFolder) { }
}
