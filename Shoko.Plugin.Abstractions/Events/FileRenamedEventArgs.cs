using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.Shoko;

namespace Shoko.Plugin.Abstractions.Events;

public class FileRenamedEventArgs : FileEventArgs
{
    /// <summary>
    /// The new file name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// The old file name.
    /// </summary>
    public string OldFileName { get; set; }

    public FileRenamedEventArgs(IShokoVideoLocation fileLocation, IImportFolder importFolder, string newFileName, string oldFileName) : base(fileLocation)
    {
        ImportFolder = importFolder;
        FileName = newFileName;
        OldFileName = oldFileName;
    }
}
