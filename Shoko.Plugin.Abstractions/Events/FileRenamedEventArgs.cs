using Shoko.Plugin.Abstractions.Models;

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

    public FileRenamedEventArgs(IShokoVideoFileLocation fileLocation, string newFileName, string oldFileName) : base(fileLocation)
    {
        FileName = newFileName;
        OldFileName = oldFileName;
    }
}
