using Shoko.Plugin.Abstractions.DataModels;

#nullable enable
namespace Shoko.Plugin.Abstractions.Events;

public class FileDeletedEventArgs : FileEventArgs
{
    public FileDeletedEventArgs(IShokoVideoFileLocation fileLocation, string relativePath, IImportFolder importFolder) : base(fileLocation, relativePath, importFolder) { }
}
