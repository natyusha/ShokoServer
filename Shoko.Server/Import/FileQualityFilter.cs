using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Models.MediaInfo;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.AniDB;
using Shoko.Plugin.Abstractions.Models.Shoko;
using Shoko.Server.Extensions;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server;

public static class FileQualityFilter
{
    /*
    Types (This is to determine the order of these types to use)
        List

    Quality -- AniDB_File.File_Source
    - BD
    - DVD
    - HDTV
    - TV
    - www
    - unknown

    Resolution (use rounding to determine where strange sizes fit)
    - 1080p
    - 1080p (BD)
    - 720p (BD Downscale and TV)
    - 540p (Nice DVD)
    - 480p (DVD)
    - < 480p (I really don't care at this low)

    Sub Groups (Need searching, will use fuzzy)
        List (Ordered set technically)
    ex.
    - Doki
    - ...
    - HorribleSubs

    Not configurable
    Higher version from the same release group, source, and resolution
    Chaptered over not chaptered

    make an enum
    reference said enum through a CompareByType

    */

    public static FileQualityPreferences Settings => Utils.SettingsProvider.GetSettings().FileQualityPreferences;

    #region Checks

    public static bool ShouldRemoveVideo(IShokoVideo video) =>
        !ShouldKeepVideo(video);

    public static bool ShouldKeepVideo(IShokoVideo video)
    {
        // Don't delete files with missing info. If it's not getting updated, then do it manually
        var anidbFile = video.AnidbFile;
        var allowUnknown = Utils.SettingsProvider.GetSettings().FileQualityPreferences.AllowDeletingFilesWithMissingInfo;
        if (IsNullOrUnknown(anidbFile) && !allowUnknown) return true;

        var result = true;
        var media = video.Media;
        foreach (var type in Settings.RequiredTypes)
        {
            result &= type switch
            {
                FileQualityFilterType.AUDIOCODEC =>
                    CheckAudioCodec(media),
                FileQualityFilterType.AUDIOSTREAMCOUNT =>
                    CheckAudioStreamCount(media),
                FileQualityFilterType.CHAPTER =>
                    CheckChaptered(anidbFile, media),
                FileQualityFilterType.RESOLUTION =>
                    CheckResolution(media),
                FileQualityFilterType.SOURCE =>
                    CheckSource(anidbFile),
                FileQualityFilterType.SUBGROUP =>
                    CheckSubGroup(anidbFile),
                FileQualityFilterType.SUBSTREAMCOUNT =>
                    CheckSubStreamCount(video),
                FileQualityFilterType.VERSION =>
                    CheckDeprecated(anidbFile),
                FileQualityFilterType.VIDEOCODEC =>
                    CheckVideoCodec(media),
                _ => true,
            };

            if (!result)
                break;
        }

        return result;
    }

    private static bool CheckAudioCodec(IMediaInfo? media)
    {
        var codecs = media?.Audio
            .Select(stream => stream.Codec.Simplified)
            .Where(codec => codec != "unknown")
            .OrderBy(codec => codec)
            .ToList() ?? new();
        if (codecs.Count == 0)
            return false;

        return Settings.RequiredAudioCodecs.Operator switch
        {
            FileQualityFilterOperationType.IN =>
                codecs.FindInEnumerable(Settings.RequiredAudioCodecs.Value),
            FileQualityFilterOperationType.NOTIN =>
                !codecs.FindInEnumerable(Settings.RequiredAudioCodecs.Value),
            _ => true,
        };
    }

    private static bool CheckAudioStreamCount(IMediaInfo? media)
    {
        var streamCount = media?.Audio.Count ?? -1;
        if (streamCount == -1)
            return true;

        return Settings.RequiredAudioStreamCount.Operator switch
        {
            FileQualityFilterOperationType.EQUALS =>
                streamCount == Settings.RequiredAudioStreamCount.Value,
            FileQualityFilterOperationType.GREATER_EQ =>
                streamCount >= Settings.RequiredAudioStreamCount.Value,
            FileQualityFilterOperationType.LESS_EQ =>
                streamCount <= Settings.RequiredAudioStreamCount.Value,
            _ => true,
        };
    }

    private static bool CheckChaptered(IAniDBFile? anidbFile, IMediaInfo? media)
    {
        return anidbFile?.IsChaptered ?? media?.Chapters.Any() ?? false;
    }

    private static bool CheckDeprecated(IAniDBFile? aniFile)
    {
        return !(aniFile?.IsDeprecated ?? false);
    }

    private static bool CheckResolution(IMediaInfo? media)
    {
        if (media == null)
            return true;
        var videoStream = media.Video.FirstOrDefault();
        if (videoStream == null || videoStream.Width == 0 || videoStream.Height == 0)
            return true;

        var resolution = MediaInfoUtils.GetStandardResolution(new(videoStream.Width, videoStream.Height));
        var resolutionArea = videoStream.Width * videoStream.Height;
        return Settings.RequiredResolutions.Operator switch
        {
            FileQualityFilterOperationType.EQUALS =>
                resolution.Equals(Settings.RequiredResolutions.Value.FirstOrDefault()),
            FileQualityFilterOperationType.GREATER_EQ =>
                MediaInfoUtils.ResolutionArea169
                    .Concat(MediaInfoUtils.ResolutionArea43)
                    .Where(pair => resolutionArea >= pair.Key)
                    .Select(pair => pair.Value)
                    .FindInEnumerable(Settings.RequiredResolutions.Value),
            FileQualityFilterOperationType.LESS_EQ =>
                MediaInfoUtils.ResolutionArea169
                    .Concat(MediaInfoUtils.ResolutionArea43)
                    .Where(pair => resolutionArea <= pair.Key)
                    .Select(pair => pair.Value)
                    .FindInEnumerable(Settings.RequiredResolutions.Value),
            FileQualityFilterOperationType.IN =>
                Settings.RequiredResolutions.Value.Contains(resolution),
            FileQualityFilterOperationType.NOTIN =>
                !Settings.RequiredResolutions.Value.Contains(resolution),
            _ => false,
        };
    }

    private static bool CheckSource(IAniDBFile? aniFile)
    {
        if (IsNullOrUnknown(aniFile))
            return false;

        var source = aniFile!.Source.ToRawString().ToLowerInvariant();
        if (FileQualityPreferences.SimplifiedSources.ContainsKey(source))
            source = FileQualityPreferences.SimplifiedSources[source];

        return Settings.RequiredSources.Operator switch
        {
            FileQualityFilterOperationType.IN =>
                Settings.RequiredSources.Value.Contains(source),
            FileQualityFilterOperationType.NOTIN =>
                !Settings.RequiredSources.Value.Contains(source),
            _ => true,
        };
    }

    private static bool CheckSubGroup(IAniDBFile? aniFile)
    {
        if (IsNullOrUnknown(aniFile))
            return false;

        var releaseGroup = aniFile!.ReleaseGroup;
        return Settings.RequiredSubGroups.Operator switch
        {
            FileQualityFilterOperationType.IN =>
                Settings.RequiredSubGroups.Value.Contains(releaseGroup.Name?.ToLowerInvariant()) ||
                Settings.RequiredSubGroups.Value.Contains(releaseGroup.ShortName?.ToLowerInvariant()),
            FileQualityFilterOperationType.NOTIN =>
                !Settings.RequiredSubGroups.Value.Contains(releaseGroup.Name?.ToLowerInvariant()) &&
                !Settings.RequiredSubGroups.Value.Contains(releaseGroup.ShortName?.ToLowerInvariant()),
            _ => true,
        };
    }

    private static bool CheckSubStreamCount(IShokoVideo? file)
    {
        var streamCount = file?.Media?.Subtitles.Count ?? -1;
        if (streamCount == -1)
            return true;

        return Settings.RequiredSubStreamCount.Operator switch
        {
            FileQualityFilterOperationType.EQUALS =>
                streamCount == Settings.RequiredSubStreamCount.Value,
            FileQualityFilterOperationType.GREATER_EQ =>
                streamCount >= Settings.RequiredSubStreamCount.Value,
            FileQualityFilterOperationType.LESS_EQ =>
                streamCount <= Settings.RequiredSubStreamCount.Value,
            _ => true,
        };
    }

    private static bool CheckVideoCodec(IMediaInfo? media)
    {
        var codecs = media?.Subtitles
            .Select(stream => stream.Codec.Simplified)
            .Where(codec => codec != "unknown")
            .OrderBy(codec => codec)
            .ToList() ?? new();
        if (codecs.Count == 0)
            return false;

        return Settings.RequiredVideoCodecs.Operator switch
        {
            FileQualityFilterOperationType.IN =>
                Settings.RequiredVideoCodecs.Value.FindInEnumerable(codecs),
            FileQualityFilterOperationType.NOTIN =>
                !Settings.RequiredVideoCodecs.Value.FindInEnumerable(codecs),
            _ => true,
        };
    }

    #endregion

    #region Comparisons

    // -1 if oldFile is to be deleted, 0 if they are comparatively equal, 1 if the oldFile is better
    public static int CompareTo(IShokoVideo? newVideo, IShokoVideo? oldVideo)
    {
        if (newVideo == null && oldVideo == null)
            return 0;
        if (newVideo == null)
            return 1;
        if (oldVideo == null)
            return -1;

        var newMedia = newVideo.Media;
        var newAnidbFile = newVideo.AnidbFile;
        var oldMedia = oldVideo.Media;
        var oldAnidbFile = oldVideo.AnidbFile;
        foreach (var type in Settings.PreferredTypes)
        {
            var result = (type) switch
            {
                FileQualityFilterType.AUDIOCODEC =>
                    CompareAudioCodecTo(newVideo, oldVideo, newMedia, oldMedia),
                FileQualityFilterType.AUDIOSTREAMCOUNT =>
                    CompareAudioStreamCountTo(newMedia, oldMedia),
                FileQualityFilterType.CHAPTER =>
                    CompareChapterTo(newMedia, newAnidbFile, oldMedia, oldAnidbFile),
                FileQualityFilterType.RESOLUTION =>
                    CompareResolutionTo(newVideo, oldVideo),
                FileQualityFilterType.SOURCE =>
                    CompareSourceTo(newAnidbFile, oldAnidbFile),
                FileQualityFilterType.SUBGROUP =>
                    CompareSubGroupTo(newAnidbFile, oldAnidbFile),
                FileQualityFilterType.SUBSTREAMCOUNT =>
                    CompareSubStreamCountTo(newMedia, oldMedia),
                FileQualityFilterType.VERSION =>
                    CompareVersionTo(newAnidbFile, oldAnidbFile, newMedia, oldMedia),
                FileQualityFilterType.VIDEOCODEC =>
                    CompareVideoCodecTo(newMedia, oldMedia),
                _ => 0,
            };

            if (result != 0)
                return result;
        }

        return 0;
    }

    private static int CompareAudioCodecTo(IShokoVideo newFile, IShokoVideo oldFile, IMediaInfo? newMedia, IMediaInfo? oldMedia)
    {
        var newCodecs = newMedia?.Audio
            .Select(stream => stream.Codec.Simplified)
            .Where(codec => codec != "unknown")
            .OrderBy(codec => codec)
            .ToList() ?? new();
        var oldCodecs = oldMedia?.Audio
            .Select(stream => stream.Codec.Simplified)
            .Where(codec => codec != "unknown")
            .OrderBy(codec => codec)
            .ToList() ?? new();
        // compare side by side, average codec quality would be vague and annoying, defer to number of audio tracks
        if (newCodecs.Count != oldCodecs.Count)
            return 0;

        var max = Math.Min(newCodecs.Count, oldCodecs.Count);
        for (var i = 0; i < max; i++)
        {
            var newCodec = newCodecs[i];
            var oldCodec = oldCodecs[i];
            var newIndex = Settings.PreferredAudioCodecs.IndexOf(newCodec);
            var oldIndex = Settings.PreferredAudioCodecs.IndexOf(oldCodec);
            if (newIndex == -1 || oldIndex == -1)
                continue;

            var result = newIndex.CompareTo(oldIndex);
            if (result != 0)
                return result;
        }

        return 0;
    }

    private static int CompareAudioStreamCountTo(IMediaInfo? newMedia, IMediaInfo? oldMedia)
    {
        var newStreamCount = newMedia?.Audio.Count ?? 0;
        var oldStreamCount = oldMedia?.Audio.Count ?? 0;
        return oldStreamCount.CompareTo(newStreamCount);
    }

    private static int CompareChapterTo(IMediaInfo? newMedia, IAniDBFile? newFile, IMediaInfo? oldMedia, IAniDBFile? oldFile)
    {
        var newIsChaptered = newFile?.IsChaptered ?? newMedia?.Chapters.Any() ?? false;
        var oldIsChaptered = oldFile?.IsChaptered ?? oldMedia?.Chapters.Any() ?? false;
        return newIsChaptered.CompareTo(oldIsChaptered);
    }

    private static int CompareResolutionTo(IShokoVideo newFile, IShokoVideo oldFile)
    {
        var newRes = newFile.Resolution;
        var oldRes = oldFile.Resolution;
        if (newRes == "unknown" && oldRes == "unknown")
            return 0;
        if (newRes == "unknown")
            return 1;
        if (oldRes == "unknown")
            return -1;

        var newIndex = Settings.PreferredResolutions.IndexOf(newRes);
        var oldIndex = Settings.PreferredResolutions.IndexOf(oldRes);
        if (newIndex == -1 && oldIndex == -1)
            return 0;
        if (newIndex == -1)
            return 1;
        if (oldIndex == -1)
            return -1;

        return newIndex.CompareTo(oldIndex);
    }

    private static int CompareSourceTo(IAniDBFile? newFile, IAniDBFile? oldFile)
    {
        var newAnidbFileIsNullOrUnknown = IsNullOrUnknown(newFile);
        var oldAnidbFileIsNullOrUnknown = IsNullOrUnknown(oldFile);
        if (newAnidbFileIsNullOrUnknown && oldAnidbFileIsNullOrUnknown)
            return 0;
        if (newAnidbFileIsNullOrUnknown)
            return 1;
        if (oldAnidbFileIsNullOrUnknown)
            return -1;

        var newSource = newFile!.Source.ToRawString().ToLowerInvariant();
        if (FileQualityPreferences.SimplifiedSources.ContainsKey(newSource))
            newSource = FileQualityPreferences.SimplifiedSources[newSource];

        var oldSource = oldFile!.Source.ToRawString().ToLowerInvariant();
        if (FileQualityPreferences.SimplifiedSources.ContainsKey(oldSource))
            oldSource = FileQualityPreferences.SimplifiedSources[oldSource];

        var newIndex = Settings.PreferredSources.IndexOf(newSource);
        var oldIndex = Settings.PreferredSources.IndexOf(oldSource);
        if (newIndex == -1 && oldIndex == -1)
            return 0;
        if (newIndex == -1)
            return 1;
        if (oldIndex == -1)
            return -1;
        return newIndex.CompareTo(oldIndex);
    }

    private static int CompareSubGroupTo(IAniDBFile? newFile, IAniDBFile? oldFile)
    {
        var newAnidbFileIsNullOrUnknown = IsNullOrUnknown(newFile);
        var oldAnidbFileIsNullOrUnknown = IsNullOrUnknown(oldFile);
        if (newAnidbFileIsNullOrUnknown && oldAnidbFileIsNullOrUnknown)
            return 0;
        if (newAnidbFileIsNullOrUnknown)
            return 1;
        if (oldAnidbFileIsNullOrUnknown)
            return -1;

        var newIndex = -1;
        var newGroup = newFile!.ReleaseGroup;
        if (!string.IsNullOrEmpty(newGroup.Name))
            newIndex = Settings.PreferredSubGroups.IndexOf(newGroup.Name);
        if (newIndex == -1 && !string.IsNullOrEmpty(newGroup.ShortName))
            newIndex = Settings.PreferredSubGroups.IndexOf(newGroup.ShortName);

        var oldIndex = -1;
        var oldGroup = oldFile!.ReleaseGroup;
        if (!string.IsNullOrEmpty(oldGroup.Name))
            oldIndex = Settings.PreferredSubGroups.IndexOf(oldGroup.Name);
        if (oldIndex == -1 && !string.IsNullOrEmpty(oldGroup.ShortName))
            oldIndex = Settings.PreferredSubGroups.IndexOf(oldGroup.ShortName);

        if (newIndex == -1 && oldIndex == -1)
            return 0;
        if (newIndex == -1)
            return 1;
        if (oldIndex == -1)
            return -1;
        return newIndex.CompareTo(oldIndex);
    }

    private static int CompareSubStreamCountTo(IMediaInfo? newMedia, IMediaInfo? oldMedia)
    {
        var newStreamCount = newMedia?.Subtitles?.Count ?? 0;
        var oldStreamCount = oldMedia?.Subtitles?.Count ?? 0;
        return oldStreamCount.CompareTo(newStreamCount);
    }

    private static int CompareVersionTo(IAniDBFile? newFile, IAniDBFile? oldFile, IMediaInfo? newMedia, IMediaInfo? oldMedia)
    {
        var newAnidbFileIsNullOrUnknown = IsNullOrUnknown(newFile);
        var oldAnidbFileIsNullOrUnknown = IsNullOrUnknown(oldFile);
        if (newAnidbFileIsNullOrUnknown && oldAnidbFileIsNullOrUnknown)
            return 0;
        if (newAnidbFileIsNullOrUnknown)
            return 1;
        if (oldAnidbFileIsNullOrUnknown)
            return -1;

        if (newFile!.ReleaseGroupId != oldFile!.ReleaseGroupId)
            return 0;

        var newBitDepth = newMedia?.Video.FirstOrDefault()?.BitDepth ?? -1;
        var oldBitDepth = oldMedia?.Video.FirstOrDefault()?.BitDepth ?? -1;
        if (newBitDepth != oldBitDepth)
            return 0;

        var newSimpleCodec = newMedia?.Video.FirstOrDefault()?.Codec.Simplified;
        var oldSimpleCodec = oldMedia?.Video.FirstOrDefault()?.Codec.Simplified;
        if (!string.Equals(newSimpleCodec, oldSimpleCodec))
            return 0;

        return oldFile.FileVersion.CompareTo(newFile.FileVersion);
    }

    private static int CompareVideoCodecTo(IMediaInfo? newMedia, IMediaInfo? oldMedia)
    {
        var newCodecs = newMedia?.Video
            .Select(stream => stream.Codec.Simplified)
            .Where(codec => codec != "unknown")
            .OrderBy(codec => codec)
            .ToList() ?? new();
        var oldCodecs = oldMedia?.Video
            .Select(stream => stream.Codec.Simplified)
            .Where(codec => codec != "unknown")
            .OrderBy(codec => codec)
            .ToList() ?? new();
        // compare side by side, average codec quality would be vague and annoying, defer to number of audio tracks
        if (newCodecs.Count != oldCodecs.Count)
            return 0;

        var max = Math.Min(newCodecs.Count, oldCodecs.Count);
        for (var i = 0; i < max; i++)
        {
            var newCodec = newCodecs[i];
            var oldCodec = oldCodecs[i];
            var newIndex = Settings.PreferredVideoCodecs.IndexOf(newCodec);
            var oldIndex = Settings.PreferredVideoCodecs.IndexOf(oldCodec);
            if (newIndex == -1 || oldIndex == -1)
            {
                continue;
            }

            var result = newIndex.CompareTo(oldIndex);
            if (result != 0)
                return result;

            var newBitDepth = newMedia?.Video.FirstOrDefault()?.BitDepth ?? -1;
            var oldBitDepth = oldMedia?.Video.FirstOrDefault()?.BitDepth ?? -1;
            if (newBitDepth == -1 || oldBitDepth == -1)
                continue;

            if (newBitDepth == 8 && oldBitDepth == 10)
                return Settings.Prefer8BitVideo ? -1 : 1;

            if (newBitDepth == 10 && oldBitDepth == 8)
                return Settings.Prefer8BitVideo ? 1 : -1;
        }

        return 0;
    }

    #endregion

    #region Information from Models (Operations that aren't simple)

    private static bool IsNullOrUnknown(IAniDBFile? file)
    {
        // Check file
        if (file == null ||
            file.Source == FileSource.Unknown)
            return true;

        // Check release group.
        var releaseGroup = file.ReleaseGroup;
        if (string.IsNullOrWhiteSpace(releaseGroup.Name) ||
            releaseGroup.Name.Equals("unknown", StringComparison.InvariantCultureIgnoreCase) ||
            string.IsNullOrWhiteSpace(releaseGroup.ShortName) ||
            releaseGroup.ShortName.Equals("unknown", StringComparison.InvariantCultureIgnoreCase))
            return true;

        return false;
    }

    #endregion
}
