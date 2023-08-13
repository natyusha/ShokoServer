using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Shoko.Commons.Notification;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Internal;

public class ImportFolder : IImportFolder
{
    #region Database Columns

    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    private string? _path { get; set; }

    public string Path
    {
        get => _path ?? string.Empty;
        set
        {
            var newValue = value;
            if (newValue != null)
            {
                if (newValue.EndsWith(":"))
                {
                    newValue += System.IO.Path.DirectorySeparatorChar;
                }

                if (newValue.Length > 0 && newValue.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
                {
                    while (newValue.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
                    {
                        newValue = newValue[0..^1];
                    }
                }

                newValue += System.IO.Path.DirectorySeparatorChar;
            }

            _path = newValue;
        }
    }

    public bool IsWatched { get; set; }

    public bool IsDropSource { get; set; }

    public bool IsDropDestination { get; set; }

    #endregion

    #region Helpers

    public IReadOnlyList<Shoko_Video> GetVideos() =>
        RepoFactory.Shoko_Video.GetByImportFolderId(Id);

    public IReadOnlyList<Shoko_Video_Location> GetVideoLocations() =>
        RepoFactory.Shoko_Video_Location.GetByImportFolderId(Id);

    public DirectoryInfo? GetDirectoryInfo() =>
        Directory.Exists(Path) ? new DirectoryInfo(Path) : null;

    public override string ToString() =>
        string.Format("{0} - {1} ({2})", Name, Path, Id);

    #endregion

    #region IImportFolder

    ImportFolderType IImportFolder.Type
    {
        get
        {
            var type = ImportFolderType.Excluded;
            if (IsDropSource)
                type |= ImportFolderType.Source;
            if (IsDropDestination)
                type |= ImportFolderType.Destination;
            return type;
        }
    }

    #endregion
}
