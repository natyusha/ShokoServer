﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.Implementations;
using Shoko.Server.Repositories;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Server.Utilities;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models.Shoko;

namespace Shoko.Server;

public class RenameFileHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static IDictionary<string, (Type type, string description)> Renamers { get; } =
        new Dictionary<string, (Type type, string description)>();

    private static IRenameScript _getRenameScript(string name)
    {
        var script = RepoFactory.RenameScript.GetByName(name) ?? RepoFactory.RenameScript.GetDefaultScript();
        if (script == null)
        {
            return null;
        }

        return new RenameScriptImpl(script.RenamerType, script.Script, script.ExtraData);
    }

    private static IRenameScript _getRenameScriptWithFallback(string name)
    {
        var script = RepoFactory.RenameScript.GetByName(name) ?? RepoFactory.RenameScript.GetDefaultOrFirst();
        if (script == null)
        {
            return null;
        }

        return new RenameScriptImpl(script.RenamerType, script.Script, script.ExtraData);
    }

    public static string GetFilename(IShokoVideoLocation videoLocation, string scriptName)
    {
        var result = Path.GetFileName(videoLocation.RelativePath);
        var script = _getRenameScript(scriptName);
        var args = new RenameEventArgs(videoLocation, script);
        foreach (var renamer in GetPluginRenamersSorted(script?.Type))
        {
            try
            {
                // get filename from plugin
                var res = renamer.GetFilename(args);
                // if the plugin said to cancel, then do so
                if (args.Cancel)
                {
                    return null;
                }

                // if the plugin returned no name, then defer
                if (string.IsNullOrEmpty(res))
                {
                    continue;
                }

                return res;
            }
            catch (Exception e)
            {
                if (!Utils.SettingsProvider.GetSettings().Plugins.DeferOnError || args.Cancel)
                {
                    throw;
                }

                Logger.Warn(
                    $"Renamer: {renamer.GetType().Name} threw an error while renaming, deferring to next renamer. Filename: \"{result}\" Error message: \"{e.Message}\"");
            }
        }

        return result;
    }

    public static (IImportFolder, string) GetDestination(IImportFolder importFolder, IShokoVideoLocation videoLocation, string scriptName)
    {
        var script = _getRenameScriptWithFallback(scriptName);
        var availableFolders = RepoFactory.ImportFolder.GetAll()
            .Cast<IImportFolder>()
            .Where(a => a.Type != ImportFolderType.Excluded)
            .ToList();
        var args = new MoveEventArgs(availableFolders, importFolder, videoLocation, script);
        foreach (var renamer in GetPluginRenamersSorted(script?.Type))
        {
            try
            {
                // get destination from renamer
                var (destFolder, destPath) = renamer.GetDestination(args);
                // if the renamer has said to cancel, then return null
                if (args.Cancel)
                {
                    return (null, null);
                }

                // if no path was specified, then defer
                if (string.IsNullOrEmpty(destPath) || destFolder == null)
                {
                    continue;
                }

                if (Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar)
                {
                    destPath = destPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                }

                destPath = RemoveFilename(videoLocation.RelativePath, destPath);

                var importFolder = RepoFactory.ImportFolder.GetByImportLocation(destFolder.Path);
                if (importFolder == null)
                {
                    Logger.Error(
                        $"Renamer returned a Destination Import Folder, but it could not be found. The offending plugin was: {renamer.GetType().GetAssemblyName()} with renamer: {renamer.GetType().Name}");
                    continue;
                }

                return (importFolder, destPath);
            }
            catch (Exception e)
            {
                if (!Utils.SettingsProvider.GetSettings().Plugins.DeferOnError || args.Cancel)
                {
                    throw;
                }

                Logger.Warn(
                    $"Renamer: {renamer.GetType().Name} threw an error while moving, deferring to next renamer. Path: \"{videoLocation.Path}\" Error message: \"{e.Message}\"");
            }
        }

        return (null, null);
    }

    private static string RemoveFilename(string filePath, string destPath)
    {
        var name = Path.DirectorySeparatorChar + Path.GetFileName(filePath);
        var last = destPath.LastIndexOf(Path.DirectorySeparatorChar);

        if (last <= -1 || last >= destPath.Length - 1)
        {
            return destPath;
        }

        var end = destPath[last..];
        if (end.Equals(name, StringComparison.Ordinal))
        {
            destPath = destPath[..last];
        }

        return destPath;
    }

    internal static void FindRenamers(IList<Assembly> assemblies)
    {
        var allTypes = assemblies.SelectMany(a =>
        {
            try
            {
                return a.GetTypes();
            }
            catch
            {
                return Type.EmptyTypes;
            }
        }).Where(a => a.GetInterfaces().Contains(typeof(IRenamer))).ToList();

        foreach (var implementation in allTypes)
        {
            var attributes = implementation.GetCustomAttributes<RenamerAttribute>();
            foreach (var (key, desc) in attributes.Select(a => (key: a.RenamerId, desc: a.Description)))
            {
                if (key == null)
                {
                    continue;
                }

                if (Renamers.ContainsKey(key))
                {
                    Logger.Warn(
                        $"[RENAMER] Warning Duplicate renamer key \"{key}\" of types {implementation}@{implementation.Assembly.Location} and {Renamers[key]}@{Renamers[key].type.Assembly.Location}");
                    continue;
                }

                Logger.Info($"Added Renamer: {key} - {desc}");
                Renamers.Add(key, (implementation, desc));
            }
        }
    }

    public static IList<IRenamer> GetPluginRenamersSorted(string renamerName)
    {
        var settings = Utils.SettingsProvider.GetSettings();
        return _getEnabledRenamers(renamerName).OrderBy(a => renamerName == a.Key ? 0 : int.MaxValue)
            .ThenBy(a =>
                settings.Plugins.RenamerPriorities.ContainsKey(a.Key)
                    ? settings.Plugins.RenamerPriorities[a.Key]
                    : int.MaxValue)
            .ThenBy(a => a.Key, StringComparer.InvariantCulture)
            .Select(a => (IRenamer)ActivatorUtilities.CreateInstance(Utils.ServiceContainer, a.Value.type))
            .ToList();
    }

    private static IEnumerable<KeyValuePair<string, (Type type, string description)>> _getEnabledRenamers(
        string renamerName)
    {
        var settings = Utils.SettingsProvider.GetSettings();
        foreach (var kvp in Renamers)
        {
            if (!string.IsNullOrEmpty(renamerName) && kvp.Key != renamerName)
            {
                continue;
            }

            if (settings.Plugins.EnabledRenamers.TryGetValue(kvp.Key, out var isEnabled) && !isEnabled)
            {
                continue;
            }

            yield return kvp;
        }
    }
}
