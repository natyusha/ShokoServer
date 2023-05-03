using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.Shoko;

namespace Shoko.Plugin.Abstractions.Events;

public class FileHashedEventArgs : FileEventArgs
{
    public FileHashedEventArgs(IShokoVideoLocation fileLocation) : base(fileLocation) { }
    public FileHashedEventArgs(IShokoVideoLocation fileLocation, string relativePath, IImportFolder importFolder) : base(fileLocation, relativePath, importFolder) { }
}
