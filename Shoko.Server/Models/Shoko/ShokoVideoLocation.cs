using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using NLog;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.Shoko;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Databases;
using Shoko.Server.FileHelper.Subtitles;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Models.Internal;

public class Shoko_Video_Location : IShokoVideoLocation
{
    #region Database Columns

    /// <inheritdoc/>
    public int Id { get; set; }

    /// <inheritdoc/>
    public int VideoId { get; set; }

    /// <inheritdoc/>
    public int ImportFolderId { get; set; }
    
    /// <inheritdoc/>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// Allow automatic relocation to be applied to this file location.
    /// </summary>
    public bool AllowAutoRelocation = true;

    /// <summary>
    /// Allow this file location to be automatically removed when spotted as a
    /// duplicate of another location. Setting this to false will excempt it
    /// from being automatically removed.
    /// </summary>
    public bool AllowAutoDelete = true;

    #endregion

    #region Helpers

    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    public string FileName =>
        System.IO.Path.GetFileName(RelativePath);

    public string AbsolutePath
    {
        get
        {
            if (string.IsNullOrEmpty(RelativePath))
                return string.Empty;

            var importFolder = ImportFolder;
            if (string.IsNullOrEmpty(importFolder?.Path))
                return string.Empty;

            return System.IO.Path.Combine(importFolder.Path, RelativePath);
        }
    }

    public bool IsAccessible
    {
        get
        {
            return GetFileInfo() != null;
        }
    }

    public ImportFolder GetImportFolder()
    {
        var importFolder = ImportFolder;
        if (importFolder == null)
            throw new NullReferenceException($"ImportFolder with Id {ImportFolderId} not found.");

        return importFolder;
    }

    public ImportFolder? ImportFolder =>
        RepoFactory.ImportFolder.GetByID(ImportFolderId);

    public Shoko_Video GetVideo()
    {
        
        var video = Video;
        if (video == null)
            throw new NullReferenceException($"Shoko_Video with Id {VideoId} not found.");

        return video;
    }

    public Shoko_Video? Video =>
        RepoFactory.Shoko_Video.GetByID(VideoId);

    /// <summary>
    /// Get the file-info object for the on-disk location if it exists and the
    /// file size matches what we know.
    /// </summary>
    /// <returns>The file info object if successfull, otherwise null.</returns>
    public FileInfo? GetFileInfo()
    {
        // Check if the absolute path is resolvable.
        var absolutePath = AbsolutePath;
        if (string.IsNullOrEmpty(absolutePath))
            return null;

        // It shouldn't be possible that the video is null here, but hey, it
        // doesn't hurt to check.
        var video = Video;
        if (video is null)
            return null;

        // Check if both the file location exists, and that the size is correct.
        var fileInfo = new FileInfo(absolutePath);
        if (!fileInfo.Exists || fileInfo.Length != video.Size)
            return null;

        return fileInfo;
    }

    /// <summary>
    /// Get a byte-stream for the on-disk content, if the file location still
    /// exists and the file size matches what we know.
    /// </summary>
    /// <returns>The file </returns>
    public FileStream? GetFileStream() =>
        GetFileInfo()?.OpenRead();

    #region Relocation (Move & Rename)

    #region Records & Enums

    private enum DELAY_IN_USE
    {
        FIRST = 750,
        SECOND = 3000,
        THIRD = 5000,
    }

    /// <summary>
    /// Represents the outcome of a file relocation.
    /// </summary>
    /// <remarks>
    /// Possible states of the outcome:
    ///   Success: Success set to true, with valid ImportFolder and RelativePath
    ///     values.
    ///
    ///   Failure: Success set to false and ErrorMessage containing the reason.
    ///     ShouldRetry may be set to true if the operation can be retried.
    /// </remarks>
    public record RelocationResult
    {
        /// <summary>
        /// The relocation was successful.
        /// If true then the <see cref="ImportFolder"/> and
        /// <see cref="RelativePath"/> should be set to valid values, otherwise
        /// if false then the <see cref="ErrorMessage"/> should be set.
        /// </summary>
        public bool Success = false;

        /// <summary>
        /// True if the operation should be retried. This is more of an internal
        /// detail.
        /// </summary>
        internal bool ShouldRetry = false;

        /// <summary>
        /// Error message if the operation was not successful.
        /// </summary>
        public string? ErrorMessage = null;

        /// <summary>
        /// Indicates the file was moved from it's current directory.
        /// </summary>
        public bool Moved = false;

        /// <summary>
        /// Indicates the file was renamed.
        /// </summary>
        public bool Renamed = false;

        /// <summary>
        /// The destination import folder if the relocation result were
        /// successful.
        /// </summary>
        public ImportFolder? ImportFolder = null;

        /// <summary>
        /// The relative path from the <see cref="ImportFolder"/> to where
        /// the file resides.
        /// </summary>
        public string? RelativePath = null;

        /// <summary>
        /// Helper to get the full server path if the relative path and import
        /// folder are valid.
        /// </summary>
        /// <returns>The combined path.</returns>
        internal string? AbsolutePath
            => ImportFolder != null && !string.IsNullOrEmpty(RelativePath) ? Path.Combine(ImportFolder.Path, RelativePath) : null;
    }

    /// <summary>
    /// Represents a request to automatically rename a file.
    /// </summary>
    public record AutoRenameRequest
    {
        /// <summary>
        /// Indicates whether the result should be a preview of the
        /// relocation.
        /// </summary>
        public bool Preview = false;

        /// <summary>
        /// The name of the renaming script to use. Leave blank to use the
        /// default script.
        /// </summary>
        public string? ScriptName = null;

        /// <summary>
        /// Skip the rename operation.
        /// </summary>
        public bool SkipRename = false;
    }

    /// <summary>
    /// Represents a request to automatically move a file.
    /// </summary>
    public record AutoMoveRequest : AutoRenameRequest
    {

        /// <summary>
        /// Indicates whether empty directories should be deleted after
        /// relocating the file.
        /// </summary>
        public bool DeleteEmptyDirectories = true;

        /// <summary>
        /// Skip the move operation.
        /// </summary>
        public bool SkipMove = false;
    }

    /// <summary>
    /// Represents a request to automatically relocate (move and rename) a file.
    /// </summary>
    public record AutoRelocateRequest : AutoMoveRequest {
        /// <summary>
        /// Forcefully relocate the file even if automatic relocation has been
        /// disabled.
        /// </summary>
        public bool Force = false;
     }

    /// <summary>
    /// Represents a request to directly relocate a file.
    /// </summary>
    public record DirectRelocateRequest
    {
        /// <summary>
        /// The import folder where the file should be relocated to.
        /// </summary>
        public ImportFolder? ImportFolder = null;

        /// <summary>
        /// The relative path from the <see cref="ImportFolder"/> where the file
        /// should be relocated to.
        /// </summary>
        public string? RelativePath = null;

        /// <summary>
        /// Indicates whether empty directories should be deleted after
        /// relocating the file.
        /// </summary>
        public bool DeleteEmptyDirectories = true;
    }

    #endregion Records & Enums

    #region Methods

    /// <summary>
    /// Relocates a file directly to the specified location based on the given
    /// request.
    /// </summary>
    /// <param name="request">The <see cref="DirectRelocateRequest"/> containing
    /// the details for the relocation operation.</param>
    /// <returns>A <see cref="RelocationResult"/> representing the outcome of
    /// the relocation operation.</returns>
    public RelocationResult DirectlyRelocateFile(DirectRelocateRequest request)
    {
        if (request?.ImportFolder == null || string.IsNullOrWhiteSpace(request.RelativePath))
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = "Invalid request object, import folder, or relative path.",
            };

        // Sanitize relative path and reject paths leading to outside the import folder.
        var fullPath = Path.GetFullPath(Path.Combine(request.ImportFolder.Path, request.RelativePath));
        if (!fullPath.StartsWith(request.ImportFolder.Path, StringComparison.OrdinalIgnoreCase))
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = "The provided relative path leads outside the import folder.",
            };

        var oldRelativePath = RelativePath;
        var oldFullPath = AbsolutePath;
        if (string.IsNullOrWhiteSpace(oldRelativePath) || string.IsNullOrWhiteSpace(oldFullPath))
        {
            logger.Warn($"Could not find or access the file to move: \"{Id}\"");
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = $"Could not find or access the file to move: {Id}",
            };
        }

        // this can happen due to file locks, so retry in awhile.
        if (!File.Exists(oldFullPath))
        {
            logger.Warn($"Could not find or access the file to move: \"{oldFullPath}\"");
            return new()
            {
                Success = false,
                ShouldRetry = true,
                ErrorMessage = $"Could not find or access the file to move: \"{oldFullPath}\"",
            };
        }

        var dropFolder = GetImportFolder();
        var newRelativePath = Path.GetRelativePath(request.ImportFolder.Path, fullPath);
        var newFolderPath = Path.GetDirectoryName(newRelativePath)!;
        var newFullPath = Path.Combine(request.ImportFolder.Path, newRelativePath);
        var newFileName = Path.GetFileName(newRelativePath);
        var renamed = !string.Equals(Path.GetFileName(oldRelativePath), newFileName, StringComparison.InvariantCultureIgnoreCase);
        var moved = !string.Equals(Path.GetDirectoryName(oldFullPath), Path.GetDirectoryName(newFullPath), StringComparison.InvariantCultureIgnoreCase);

        // Don't touch files not in a drop source... unless we're requested to.
        if (moved && !dropFolder.IsDropSource)
        {
            logger.Trace($"Not moving file as it is NOT in an import folder marked as a drop source: \"{oldFullPath}\"");
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = $"Not moving file as it is NOT in an import folder marked as a drop source: \"{oldFullPath}\"",
            };
        }

        // Last ditch effort to ensure we aren't moving a file unto itself
        if (string.Equals(newFullPath, oldFullPath, StringComparison.InvariantCultureIgnoreCase))
        {
            logger.Trace($"Resolved to move \"{newFullPath}\" unto itself. Not moving.");
            return new()
            {
                Success = true,
                ImportFolder = request.ImportFolder,
                RelativePath = newRelativePath,
            };
        }

        var destFullTree = Path.Combine(request.ImportFolder.Path, newFolderPath);
        if (!Directory.Exists(destFullTree))
        {
            try
            {
                Utils.ShokoServer.AddFileWatcherExclusion(destFullTree);
                Directory.CreateDirectory(destFullTree);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.Message);
                return new()
                {
                    Success = false,
                    ShouldRetry = false,
                    ErrorMessage = ex.Message,
                };
            }
            finally
            {
                Utils.ShokoServer.RemoveFileWatcherExclusion(destFullTree);
            }
        }

        var sourceFile = new FileInfo(oldRelativePath);
        if (File.Exists(newFullPath))
        {
            // A file with the same name exists at the destination.
            logger.Trace("A file already exists at the new location, checking it for duplicate…");
            var video = Video;
            var destinationVideoLocation = RepoFactory.Shoko_Video_Location.GetByFilePathAndImportFolderID(newRelativePath, request.ImportFolder.Id);
            var destVideo = destinationVideoLocation?.Video;
            if (destVideo == null || destinationVideoLocation == null)
            {
                logger.Warn("The existing file at the new location does not have a Video. Not moving.");
                return new()
                {
                    Success = false,
                    ShouldRetry = false,
                    ErrorMessage = "The existing file at the new location does not have a Video. Not moving.",
                };
            }

            if (destVideo.ED2K == video?.ED2K)
            {
                logger.Debug($"Not moving file as it already exists at the new location, deleting source file instead: \"{oldFullPath}\" --- \"{newFullPath}\"");

                // if the file already exists, we can just delete the source file instead
                // this is safer than deleting and moving
                try
                {
                    sourceFile.Delete();
                }
                catch (Exception e)
                {
                    logger.Warn($"Unable to DELETE file: \"{AbsolutePath}\" error {e}");
                    RemoveRecord(false);

                    if (request.DeleteEmptyDirectories)
                        RecursiveDeleteEmptyDirectories(dropFolder?.Path, true);
                    return new()
                    {
                        Success = false,
                        ShouldRetry = false,
                        ErrorMessage = $"Unable to DELETE file: \"{AbsolutePath}\" error {e}",
                    };
                }
            }

            // Not a dupe, don't delete it
            logger.Trace("A file already exists at the new location, checking it for version and group");
            var destinationExistingAniDBFile = destVideo.GetAniDB();
            if (destinationExistingAniDBFile == null)
            {
                logger.Warn("The existing file at the new location does not have AniDB info. Not moving.");
                return new()
                {
                    Success = false,
                    ShouldRetry = false,
                    ErrorMessage = "The existing file at the new location does not have AniDB info. Not moving.",
                };
            }

            var aniDBFile = video?.GetAniDB();
            if (aniDBFile == null)
            {
                logger.Warn("The file does not have AniDB info. Not moving.");
                return new()
                {
                    Success = false,
                    ShouldRetry = false,
                    ErrorMessage = "The file does not have AniDB info. Not moving.",
                };
            }

            if (destinationExistingAniDBFile.Anime_GroupName == aniDBFile.Anime_GroupName &&
                destinationExistingAniDBFile.FileVersion < aniDBFile.FileVersion)
            {
                // This is a V2 replacing a V1 with the same name.
                // Normally we'd let the Multiple Files Utility handle it, but let's just delete the V1
                logger.Info("The existing file is a V1 from the same group. Replacing it.");

                // Delete the destination
                destinationVideoLocation.RemoveRecordAndDeletePhysicalFile();

                // Move
                Utils.ShokoServer.AddFileWatcherExclusion(newFullPath);
                logger.Info($"Moving file from \"{oldFullPath}\" to \"{newFullPath}\"");
                try
                {
                    sourceFile.MoveTo(newFullPath);
                }
                catch (Exception e)
                {
                    logger.Error($"Unable to MOVE file: \"{oldFullPath}\" to \"{newFullPath}\" error {e}");
                    Utils.ShokoServer.RemoveFileWatcherExclusion(newFullPath);
                    return new()
                    {
                        Success = false,
                        ShouldRetry = true,
                        ErrorMessage = $"Unable to MOVE file: \"{oldFullPath}\" to \"{newFullPath}\" error {e}",
                    };
                }

                ImportFolderId = request.ImportFolder.Id;
                RelativePath = newRelativePath;
                RepoFactory.Shoko_Video_Location.Save(this);

                if (request.DeleteEmptyDirectories)
                    RecursiveDeleteEmptyDirectories(dropFolder?.Path, true);
            }
        }
        else
        {
            Utils.ShokoServer.AddFileWatcherExclusion(newFullPath);
            logger.Info($"Moving file from \"{oldFullPath}\" to \"{newFullPath}\"");
            try
            {
                sourceFile.MoveTo(newFullPath);
            }
            catch (Exception e)
            {
                Utils.ShokoServer.RemoveFileWatcherExclusion(newFullPath);
                logger.Error($"Unable to MOVE file: \"{oldFullPath}\" to \"{newFullPath}\" error {e}");
                return new()
                {
                    Success = false,
                    ShouldRetry = true,
                    ErrorMessage = $"Unable to MOVE file: \"{oldFullPath}\" to \"{newFullPath}\" error {e}",
                };
            }

            ImportFolderId = request.ImportFolder.Id;
            RelativePath = newRelativePath;
            RepoFactory.Shoko_Video_Location.Save(this);

            if (request.DeleteEmptyDirectories)
                RecursiveDeleteEmptyDirectories(dropFolder?.Path, true);
        }

        if (renamed)
        {
            // Add a new lookup entry.
            var video = Video;
            var filenameHash = RepoFactory.CR_FileName_ED2K.GetByHash(video.ED2K);
            if (!filenameHash.Any(a => a.FileName.Equals(newFileName)))
            {
                var fnhash = new FileNameHash
                {
                    DateTimeUpdated = DateTime.Now,
                    FileName = newFileName,
                    FileSize = video.Size,
                    Hash = video.ED2K
                };
                RepoFactory.CR_FileName_ED2K.Save(fnhash);
            }
        }

        // Move the external subtitles.
        MoveExternalSubtitles(newFullPath, oldFullPath);

        // Fire off the moved/renamed event depending on what was done.
        if (renamed && !moved)
            ShokoEventHandler.Instance.OnFileRenamed(request.ImportFolder, Path.GetFileName(oldRelativePath), newFileName, this);
        else
            ShokoEventHandler.Instance.OnFileMoved(dropFolder, request.ImportFolder, oldRelativePath, newRelativePath, this);

        return new()
        {
            Success = true,
            ShouldRetry = false,
            ImportFolder = request.ImportFolder,
            RelativePath = newRelativePath,
            Moved = moved,
            Renamed = renamed,
        };
    }

    /// <summary>
    /// Automatically relocates a file using the specified relocation request or
    /// default settings.
    /// </summary>
    /// <param name="request">The <see cref="AutoRelocateRequest"/> containing
    /// the details for the relocation operation, or null for default settings.</param>
    /// <returns>A <see cref="RelocationResult"/> representing the outcome of
    /// the relocation operation.</returns>
    public RelocationResult AutoRelocateFile(AutoRelocateRequest? request = null)
    {
        // Allows calling the method without any parameters.
        request ??= new();

        if (!request.Force && !AllowAutoRelocation)
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = "Unable to relocate a file with auto relocation disabled unless forced."
            };

        if (!string.IsNullOrEmpty(request.ScriptName) && string.Equals(request.ScriptName, Shoko.Models.Constants.Renamer.TempFileName))
        {
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = "Do not attempt to use a temp file to rename or move.",
            };
        }

        // Make sure the import folder is reachable.
        var dropFolder = ImportFolder;
        if (dropFolder == null)
        {
            logger.Warn($"Unable to find import folder with id {ImportFolderId}");
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = $"Unable to find import folder with id {ImportFolderId}",
            };
        }

        // Make sure the path is resolvable.
        var oldFullPath = Path.Combine(dropFolder.Path, RelativePath);
        if (string.IsNullOrWhiteSpace(RelativePath) || string.IsNullOrWhiteSpace(oldFullPath))
        {
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = $"Could not find or access the file to move: {Id}",
            };
        }

        var settings = Utils.SettingsProvider.GetSettings();
        RelocationResult renameResult;
        RelocationResult moveResult;
        if (settings.Import.RenameThenMove)
        {
            // Try a maximum of 4 times to rename, and after that we bail.
            renameResult = RenameFile(request, dropFolder);
            if (!renameResult.Success && renameResult.ShouldRetry)
            {
                Thread.Sleep((int)DELAY_IN_USE.FIRST);
                renameResult = RenameFile(request, dropFolder);
                if (!renameResult.Success && renameResult.ShouldRetry)
                {
                    Thread.Sleep((int)DELAY_IN_USE.SECOND);
                    renameResult = RenameFile(request, dropFolder);
                    if (!renameResult.Success && renameResult.ShouldRetry)
                    {
                        Thread.Sleep((int)DELAY_IN_USE.THIRD);
                        renameResult = RenameFile(request, dropFolder);
                    }
                }
            }
            if (!renameResult.Success)
                return renameResult;

            // Same as above, just for moving instead.
            moveResult = MoveFile(request, dropFolder);
            if (!moveResult.Success && moveResult.ShouldRetry)
            {
                Thread.Sleep((int)DELAY_IN_USE.FIRST);
                moveResult = MoveFile(request, dropFolder);
                if (!moveResult.Success && moveResult.ShouldRetry)
                {
                    Thread.Sleep((int)DELAY_IN_USE.SECOND);
                    moveResult = MoveFile(request, dropFolder);
                    if (!moveResult.Success && moveResult.ShouldRetry)
                    {
                        Thread.Sleep((int)DELAY_IN_USE.THIRD);
                        moveResult = MoveFile(request, dropFolder);
                    }
                }
            }
            if (!moveResult.Success)
                return moveResult;
        }
        else
        {
            moveResult = MoveFile(request, dropFolder);
            if (!moveResult.Success && moveResult.ShouldRetry)
            {
                Thread.Sleep((int)DELAY_IN_USE.FIRST);
                moveResult = MoveFile(request, dropFolder);
                if (!moveResult.Success && moveResult.ShouldRetry)
                {
                    Thread.Sleep((int)DELAY_IN_USE.SECOND);
                    moveResult = MoveFile(request, dropFolder);
                    if (!moveResult.Success && moveResult.ShouldRetry)
                    {
                        Thread.Sleep((int)DELAY_IN_USE.THIRD);
                        moveResult = MoveFile(request, dropFolder);
                    }
                }
            }
            if (!moveResult.Success)
                return moveResult;

            renameResult = RenameFile(request, dropFolder);
            if (!renameResult.Success && renameResult.ShouldRetry)
            {
                Thread.Sleep((int)DELAY_IN_USE.FIRST);
                renameResult = RenameFile(request, dropFolder);
                if (!renameResult.Success && renameResult.ShouldRetry)
                {
                    Thread.Sleep((int)DELAY_IN_USE.SECOND);
                    renameResult = RenameFile(request, dropFolder);
                    if (!renameResult.Success && renameResult.ShouldRetry)
                    {
                        Thread.Sleep((int)DELAY_IN_USE.THIRD);
                        renameResult = RenameFile(request, dropFolder);
                    }
                }
            }
            if (!renameResult.Success)
                return renameResult;
        }

        // Set the linux permissions now if we're not previewing the result.
        if (!request.Preview)
        {
            Utils.ShokoServer.AddFileWatcherExclusion(AbsolutePath);
            try
            {
                LinuxFS.SetLinuxPermissions(AbsolutePath, settings.Linux.UID,
                    settings.Linux.GID, settings.Linux.Permission);
            }
            catch (InvalidOperationException e)
            {
                logger.Error(e, $"Unable to set permissions ({settings.Linux.UID}:{settings.Linux.GID} {settings.Linux.Permission}) on file {FileName}: Access Denied");
            }
            catch (Exception e)
            {
                logger.Error(e, "Error setting Linux Permissions: {0}", e);
            }
            Utils.ShokoServer.RemoveFileWatcherExclusion(AbsolutePath);
        }

        var correctFileName = Path.GetFileName(renameResult.RelativePath);
        var correctFolder = Path.GetDirectoryName(moveResult.RelativePath)!;
        var correctRelativePath = !string.IsNullOrEmpty(correctFileName) ? Path.Combine(correctFolder, correctFileName) : correctFileName;
        var correctFullPath = Path.Combine(moveResult.ImportFolder!.Path, correctRelativePath!);
        logger.Trace($"{(request.Preview ? "Resolved to move" : "Moved")} from \"{oldFullPath}\" to \"{correctFullPath}\".");
        return new()
        {
            Success = renameResult.Success && moveResult.Success,
            ShouldRetry = renameResult.ShouldRetry || moveResult.ShouldRetry,
            ImportFolder = moveResult.ImportFolder,
            RelativePath = correctRelativePath,
            Moved = moveResult.Moved,
            Renamed = renameResult.Renamed,
        };
    }

    /// <summary>
    /// Renames a file using the specified rename request.
    /// </summary>
    /// <param name="request">The <see cref="AutoRenameRequest"/> containing the
    /// details for the rename operation.</param>
    /// <param name="currentFolder">The current import folder.</param>
    /// <returns>A <see cref="RelocationResult"/> representing the outcome of
    /// the rename operation.</returns>
    private RelocationResult RenameFile(AutoRenameRequest request, ImportFolder currentFolder)
    {
        // Just return the existing values if we're going to skip the operation.
        if (request.SkipRename)
            return new()
            {
                Success = true,
                ShouldRetry = false,
                ImportFolder = currentFolder,
                RelativePath = RelativePath,
            };

        string newFileName;
        try
        {
            newFileName = RenameFileHelper.GetFilename(this, request.ScriptName);
        }
        // The renamer may throw an error
        catch (Exception ex)
        {
            var errorMessage = ex.Message;
            if (ex.Message.StartsWith("*Error:"))
                errorMessage = errorMessage.Substring(7).Trim();

            logger.Error($"Error: The renamer returned an error on file: \"{AbsolutePath}\"");
            logger.Error(ex, errorMessage);
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = errorMessage,
            };
        }

        if (string.IsNullOrWhiteSpace(newFileName))
        {
            logger.Error($"Error: The renamer returned a null or empty name for: \"{AbsolutePath}\"");
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = "The file renamer returned a null or empty value",
            };
        }

        // Or it may return an error message.
        if (newFileName.StartsWith("*Error:"))
        {
            var errorMessage = newFileName.Substring(7).Trim();
            logger.Error($"Error: The renamer returned an error on file: \"{AbsolutePath}\"\n{errorMessage}");
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = errorMessage,
            };
        }

        // Return early if we're only previewing.
        var newFullPath = Path.Combine(Path.GetDirectoryName(AbsolutePath)!, newFileName);
        var newRelativePath = Path.GetRelativePath(currentFolder.Path, newFullPath);
        if (request.Preview)
            return new()
            {
                Success = true,
                ImportFolder = currentFolder,
                RelativePath = newRelativePath,
                Renamed = !string.Equals(FileName, newFileName, StringComparison.InvariantCultureIgnoreCase),
            };

        // Actually move it.
        return DirectlyRelocateFile(new()
        {
            DeleteEmptyDirectories = false,
            ImportFolder = currentFolder,
            RelativePath = newRelativePath,
        });
    }

    /// <summary>
    /// Moves a file using the specified move request.
    /// </summary>
    /// <param name="request">The <see cref="AutoMoveRequest"/> containing the
    /// details for the move operation.</param>
    /// <param name="currentFolder">The current import folder.</param>
    /// <returns>A <see cref="RelocationResult"/> representing the outcome of
    /// the move operation.</returns>
    private RelocationResult MoveFile(AutoMoveRequest request, ImportFolder currentFolder)
    {
        // Just return the existing values if we're going to skip the operation.
        if (request.SkipMove)
            return new()
            {
                Success = true,
                ShouldRetry = false,
                ImportFolder = currentFolder,
                RelativePath = RelativePath,
            };

        IImportFolder destImpl;
        string newFolderPath;
        try
        {
            // Find the new destination.
            (destImpl, newFolderPath) = RenameFileHelper.GetDestination(currentFolder, this, request.ScriptName);
        }
        // The renamer may throw an error
        catch (Exception ex)
        {
            var errorMessage = ex.Message;
            if (ex.Message.StartsWith("*Error:"))
                errorMessage = errorMessage.Substring(7).Trim();

            logger.Warn($"Could not find a valid destination: \"{AbsolutePath}\"");
            logger.Error(ex, errorMessage);
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = errorMessage,
            };
        }

        // Ensure the new folder path is not null.
        newFolderPath ??= "";

        // Check if we have an import folder selected.
        if (!(destImpl is ImportFolder importFolder))
        {
            logger.Warn($"Could not find a valid destination: \"{AbsolutePath}\"");
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = !string.IsNullOrWhiteSpace(newFolderPath) ? (
                    newFolderPath.StartsWith("*Error:", StringComparison.InvariantCultureIgnoreCase) ? (
                        newFolderPath.Substring(7).TrimStart()
                    ) : (
                        newFolderPath
                    )
                ) : (
                    $"Could not find a valid destination: \"{AbsolutePath}"
                ),
            };
        }

        // Check the path for errors, even if an import folder is selected.
        if (newFolderPath.StartsWith("*Error:", StringComparison.InvariantCultureIgnoreCase))
        {
            logger.Warn($"Could not find a valid destination: \"{AbsolutePath}\"");
            return new()
            {
                Success = false,
                ShouldRetry = false,
                ErrorMessage = newFolderPath.Substring(7).TrimStart(),
            };
        }

        // Return early if we're only previewing.
        var oldFolderPath = Path.GetDirectoryName(AbsolutePath);
        var newRelativePath = Path.Combine(newFolderPath, FileName);
        if (request.Preview)
            return new()
            {
                Success = true,
                ImportFolder = importFolder,
                RelativePath = newRelativePath,
                Moved = !string.Equals(oldFolderPath, newFolderPath, StringComparison.InvariantCultureIgnoreCase),
            };

        // Actually move it.
        return DirectlyRelocateFile(new()
        {
            DeleteEmptyDirectories = request.DeleteEmptyDirectories,
            ImportFolder = importFolder,
            RelativePath = newRelativePath,
        });
    }

    #endregion Methods

    #region Move On Import

    public void RenameAndMoveAsRequired()
    {
        var settings = Utils.SettingsProvider.GetSettings();
        if (!settings.Import.RenameOnImport)
            logger.Trace($"Skipping rename of \"{AbsolutePath}\" as rename on import is disabled");
        if (!!settings.Import.MoveOnImport)
            logger.Trace($"Skipping move of \"{this.AbsolutePath}\" as move on import is disabled");

        AutoRelocateFile(new AutoRelocateRequest()
        {
            SkipRename = !settings.Import.RenameOnImport,
            SkipMove = !settings.Import.MoveOnImport,
        });
    }

    #endregion Move On Import

    #region Helpers

    private static void MoveExternalSubtitles(string newFullPath, string srcFullPath)
    {
        try
        {
            var srcParent = Path.GetDirectoryName(srcFullPath);
            var newParent = Path.GetDirectoryName(newFullPath);
            if (string.IsNullOrEmpty(newParent) || string.IsNullOrEmpty(srcParent))
                return;

            var textStreams = SubtitleHelper.GetSubtitleStreams(srcFullPath);
            // move any subtitle files
            foreach (var subtitleFile in textStreams)
            {
                if (string.IsNullOrEmpty(subtitleFile.Filename))
                    continue;

                var subPath = Path.Combine(srcParent, subtitleFile.Filename);
                if (!File.Exists(subPath))
                {
                    logger.Error($"Unable to rename external subtitle file \"{subtitleFile.Filename}\". Cannot access the file");
                    continue;
                }

                var subFile = new FileInfo(subPath);
                var newSubPath = Path.Combine(newParent, subFile.Name);
                if (File.Exists(newSubPath))
                {
                    try
                    {
                        File.Delete(newSubPath);
                    }
                    catch (Exception e)
                    {
                        logger.Warn($"Unable to DELETE file: \"{subtitleFile}\" error {e}");
                    }
                }

                try
                {
                    subFile.MoveTo(newSubPath);
                }
                catch (Exception e)
                {
                    logger.Error($"Unable to MOVE file: \"{subtitleFile}\" to \"{newSubPath}\" error {e}");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.Message);
        }
    }

    private static void DeleteExternalSubtitles(string srcFullPath)
    {
        try
        {
            var textStreams = SubtitleHelper.GetSubtitleStreams(srcFullPath);
            // move any subtitle files
            foreach (var subtitleFile in textStreams)
            {
                if (string.IsNullOrEmpty(subtitleFile.Filename)) continue;

                var srcParent = Path.GetDirectoryName(srcFullPath);
                if (string.IsNullOrEmpty(srcParent)) continue;

                var subPath = Path.Combine(srcParent, subtitleFile.Filename);
                if (!File.Exists(subPath)) continue;

                try
                {
                    File.Delete(subPath);
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Unable to delete file: \"{subtitleFile}\"");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }
    }

    private void RecursiveDeleteEmptyDirectories(string? dir, bool importfolder)
    {
        try
        {
            if (string.IsNullOrEmpty(dir))
            {
                return;
            }

            if (!Directory.Exists(dir))
            {
                return;
            }

            if (IsDirectoryEmpty(dir))
            {
                if (importfolder)
                {
                    return;
                }

                try
                {
                    Directory.Delete(dir);
                }
                catch (Exception ex)
                {
                    if (ex is DirectoryNotFoundException || ex is FileNotFoundException)
                    {
                        return;
                    }

                    logger.Warn("Unable to DELETE directory: {0} Error: {1}", dir,
                        ex);
                }

                return;
            }

            // If it has folder, recurse
            foreach (var d in Directory.EnumerateDirectories(dir))
            {
                if (Utils.SettingsProvider.GetSettings().Import.Exclude.Any(s => Regex.IsMatch(Path.GetDirectoryName(d) ?? string.Empty, s))) continue;
                RecursiveDeleteEmptyDirectories(d, false);
            }
        }
        catch (Exception e)
        {
            if (e is FileNotFoundException || e is DirectoryNotFoundException)
            {
                return;
            }

            logger.Error($"There was an error removing the empty directory: {dir}\r\n{e}");
        }
    }

    public bool IsDirectoryEmpty(string path)
    {
        try
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }
        catch
        {
            return false;
        }
    }

    #endregion Helpers

    #endregion Relocation (Move & Rename)

    #region Remove Record

    public void RemoveRecordAndDeletePhysicalFile(bool deleteFolder = true)
    {
        logger.Info("Deleting video local place record and file: {0}",
            AbsolutePath ?? Id.ToString());

        if (!File.Exists(AbsolutePath))
        {
            logger.Info($"Unable to find file. Removing Record: {AbsolutePath ?? RelativePath}");
            RemoveRecord();
            return;
        }

        try
        {
            File.Delete(AbsolutePath);
            DeleteExternalSubtitles(AbsolutePath);
        }
        // Just continue if the file doesn't exist.
        catch (FileNotFoundException) { }
        catch (Exception ex)
        {
            logger.Error($"Unable to delete file '{AbsolutePath}': {ex}");
            throw;
        }

        if (deleteFolder)
            RecursiveDeleteEmptyDirectories(ImportFolder?.Path, true);
        RemoveRecord();
    }

    public void RemoveAndDeleteFileWithOpenTransaction(ISession session, HashSet<ShokoSeries> seriesToUpdate)
    {
        logger.Info("Deleting video local place record and file: {0}",
            AbsolutePath ?? Id.ToString());


        if (!File.Exists(AbsolutePath))
        {
            logger.Info($"Unable to find file. Removing Record: {AbsolutePath}");
            RemoveRecordWithOpenTransaction(session, seriesToUpdate);
            return;
        }

        try
        {
            File.Delete(AbsolutePath);
            DeleteExternalSubtitles(AbsolutePath);
        }
        // Just continue if the file doesn't exist.
        catch (FileNotFoundException) { }
        catch (Exception ex)
        {
            logger.Error($"Unable to delete file '{AbsolutePath}': {ex}");
            return;
        }

        RecursiveDeleteEmptyDirectories(ImportFolder?.Path, true);
        RemoveRecordWithOpenTransaction(session, seriesToUpdate);
    }

    public void RemoveRecord(bool updateMyListStatus = true)
    {
        var importFolder = ImportFolder;
        logger.Info("Removing Shoko_Video_Location record for: {0}", AbsolutePath ?? Id.ToString());
        var seriesToUpdate = new List<ShokoSeries>();
        var v = Video;
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            if (v?.Locations?.Count <= 1)
            {
                if (updateMyListStatus)
                {
                    if (v.AniDB == null)
                    {
                        var xrefs = v.GetCrossReferences(false);
                        foreach (var xref in xrefs)
                        {
                            var ep = xref.AnidbEpisode;
                            if (ep == null)
                            {
                                continue;
                            }

                            var cmdDel = commandFactory.Create<CommandRequest_DeleteFileFromMyList>(
                                c =>
                                {
                                    c.AnimeID = xref.AnidbAnimeId;
                                    c.EpisodeType = ep.Type;
                                    c.EpisodeNumber = ep.Number;
                                }
                            );
                            cmdDel.Save();
                        }
                    }
                    else
                    {
                        var cmdDel = commandFactory.Create<CommandRequest_DeleteFileFromMyList>(
                            c =>
                            {
                                c.Hash = v.ED2K;
                                c.FileSize = v.Size;
                            }
                        );
                        cmdDel.Save();
                    }
                }

                try
                {
                    ShokoEventHandler.Instance.OnFileDeleted(importFolder, this, v);
                }
                catch
                {
                    // ignore
                }

                lock (BaseRepository.GlobalDBLock)
                {
                    using var transaction = session.BeginTransaction();
                    RepoFactory.Shoko_Video_Location.DeleteWithOpenTransaction(session, this);

                    seriesToUpdate.AddRange(v.GetSeries());
                    RepoFactory.Shoko_Video.DeleteWithOpenTransaction(session, v);

                    transaction.Commit();
                }
            }
            else
            {
                try
                {
                    ShokoEventHandler.Instance.OnFileDeleted(importFolder, this, v);
                }
                catch
                {
                    // ignore
                }

                lock (BaseRepository.GlobalDBLock)
                {
                    using var transaction = session.BeginTransaction();
                    RepoFactory.Shoko_Video_Location.DeleteWithOpenTransaction(session, this);
                    transaction.Commit();
                }
            }
        }

        foreach (var ser in seriesToUpdate)
        {
            ser?.QueueUpdateStats();
        }
    }


    public void RemoveRecordWithOpenTransaction(ISession session, ICollection<ShokoSeries> seriesToUpdate,
        bool updateMyListStatus = true)
    {
        var importFolder = ImportFolder;
        logger.Info("Removing Shoko_Video_Location record for: {0}", AbsolutePath ?? Id.ToString());
        var v = Video;
        var commandFactory = Utils.ServiceContainer.GetRequiredService<ICommandRequestFactory>();

        if (v?.Locations?.Count <= 1)
        {
            if (updateMyListStatus)
            {
                if (v.AniDB == null)
                {
                    var xrefs = v.GetCrossReferences(false);
                    foreach (var xref in xrefs)
                    {
                        var ep = xref.AnidbEpisode;
                        if (ep == null)
                        {
                            continue;
                        }

                        var cmdDel = commandFactory.Create<CommandRequest_DeleteFileFromMyList>(c =>
                        {
                            c.AnimeID = xref.AnidbAnimeId;
                            c.EpisodeType = ep.Type;
                            c.EpisodeNumber = ep.Number;
                        });
                        cmdDel.Save();
                    }
                }
                else
                {
                    var cmdDel = commandFactory.Create<CommandRequest_DeleteFileFromMyList>(
                        c =>
                        {
                            c.Hash = v.ED2K;
                            c.FileSize = v.Size;
                        }
                    );
                    cmdDel.Save();
                }
            }

            foreach (var series in v.GetSeries())
                seriesToUpdate.Add(series);

            try
            {
                ShokoEventHandler.Instance.OnFileDeleted(importFolder, this, v);
            }
            catch
            {
                // ignore
            }

            lock (BaseRepository.GlobalDBLock)
            {
                using var transaction = session.BeginTransaction();
                RepoFactory.Shoko_Video_Location.DeleteWithOpenTransaction(session, this);
                RepoFactory.Shoko_Video.DeleteWithOpenTransaction(session, v);

                transaction.Commit();
            }
        }
        else
        {
            try
            {
                ShokoEventHandler.Instance.OnFileDeleted(importFolder, this, v);
            }
            catch
            {
                // ignore
            }

            lock (BaseRepository.GlobalDBLock)
            {
                using var transaction = session.BeginTransaction();
                RepoFactory.Shoko_Video_Location.DeleteWithOpenTransaction(session, this);
                transaction.Commit();
            }
        }
    }

    #endregion

    #endregion

    #region IShokoVideoLocation

    IImportFolder IShokoVideoLocation.ImportFolder =>
        GetImportFolder();

    IShokoVideo IShokoVideoLocation.Video =>
        Video;

    DataSource IMetadata.DataSource =>
        DataSource.Shoko;

    #endregion
}
