using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Events;

namespace Shoko.Plugin.Abstractions;

/// <summary>
/// A renamer must implement this to be called
/// </summary>
public interface IRenamer
{
    string GetFilename(RenameEventArgs args);

    (IImportFolder destination, string subfolder) GetDestination(MoveEventArgs args);
}
