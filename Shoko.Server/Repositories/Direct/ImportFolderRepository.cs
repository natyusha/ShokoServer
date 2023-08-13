using System;
using System.IO;
using System.Linq;
using Shoko.Server.Models.Internal;
using Shoko.Server.Utilities;

namespace Shoko.Server.Repositories.Cached;

public class ImportFolderRepository : BaseCachedRepository<ImportFolder, int>
{
    protected override int SelectKey(ImportFolder entity)
    {
        return entity.Id;
    }

    public override void PopulateIndexes()
    {
    }

    public override void RegenerateDb()
    {
    }

    public (ImportFolder? importFolder, string relativePath) GetFromAbsolutePath(string fullPath)
    {
        var importFolders = GetAll();

        // TODO make sure that import folders are not sub folders of each other
        // TODO make sure import folders do not contain a trailing "\"
        foreach (var folder in importFolders)
        {
            var importLocation = folder.Path;
            var importLocationFull = importLocation.TrimEnd(Path.DirectorySeparatorChar);

            // add back the trailing back slashes
            importLocationFull += $"{Path.DirectorySeparatorChar}";

            importLocation = importLocation.TrimEnd(Path.DirectorySeparatorChar);
            if (fullPath.StartsWith(importLocationFull, StringComparison.InvariantCultureIgnoreCase))
            {
                var filePath = fullPath.Replace(importLocation, string.Empty);
                filePath = filePath.TrimStart(Path.DirectorySeparatorChar);
                return (folder, filePath);
            }
        }

        return (null, string.Empty);
    }

    public ImportFolder GetByImportLocation(string importloc)
    {
        return ReadLock(() => Cache.Values.FirstOrDefault(a =>
            a.Path?.Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar)
                .Equals(
                    importloc?.Replace('\\', Path.DirectorySeparatorChar)
                        .Replace('/', Path.DirectorySeparatorChar)
                        .TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.InvariantCultureIgnoreCase) ?? false));
    }

    public ImportFolder SaveImportFolder(ImportFolder folder)
    {
        ImportFolder ns;
        if (folder.Id > 0)
        {
            // update
            ns = GetByID(folder.Id);
            if (ns == null)
            {
                throw new Exception($"Could not find Import Folder ID: {folder.Id}");
            }
        }
        else
        {
            // create
            ns = new ImportFolder();
        }

        if (string.IsNullOrEmpty(folder.Name))
        {
            throw new Exception("Must specify an Import Folder name");
        }

        if (string.IsNullOrEmpty(folder.Path))
        {
            throw new Exception("Must specify an Import Folder location");
        }

        if (!Directory.Exists(folder.Path))
        {
            throw new Exception("Cannot find Import Folder location");
        }

        if (folder.Id == 0)
        {
            var nsTemp =
                GetByImportLocation(folder.Path);
            if (nsTemp != null)
            {
                throw new Exception("Another entry already exists for the specified Import Folder location");
            }
        }

        ns.Name = folder.Name;
        ns.Path = folder.Path;
        ns.IsDropDestination = folder.IsDropDestination;
        ns.IsDropSource = folder.IsDropSource;
        ns.IsWatched = folder.IsWatched;

        Save(ns);

        Utils.MainThreadDispatch(() => { ServerInfo.Instance.RefreshImportFolders(); });
        Utils.ShokoServer.StopWatchingFiles();
        Utils.ShokoServer.StartWatchingFiles();

        return ns;
    }
}
