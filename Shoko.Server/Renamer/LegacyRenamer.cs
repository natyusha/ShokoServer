using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.Models;
using Shoko.Plugin.Abstractions.Models.AniDB;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Repositories;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Renamer;

// http://wiki.anidb.net/w/WebAOM#Move.2Frename_system
public struct FileRenameTag
{
    public static readonly string AnimeNameRomaji = "%ann";
    public static readonly string AnimeNameKanji = "%kan";
    public static readonly string AnimeNameEnglish = "%eng";
    public static readonly string EpisodeNameRomaji = "%epn";
    public static readonly string EpisodeNameEnglish = "%epr";
    public static readonly string EpisodeNumber = "%enr";
    public static readonly string GroupShortName = "%grp";
    public static readonly string GroupLongName = "%grl";
    public static readonly string ED2KLower = "%ed2";
    public static readonly string ED2KUpper = "%ED2";
    public static readonly string CRCLower = "%crc";
    public static readonly string CRCUpper = "%CRC";
    public static readonly string FileVersion = "%ver";
    public static readonly string Source = "%src";
    public static readonly string Resolution = "%res";
    public static readonly string VideoHeight = "%vdh";
    public static readonly string Year = "%yea";
    public static readonly string Episodes = "%eps"; // Total number of episodes
    public static readonly string Type = "%typ"; // Type [unknown, TV, OVA, Movie, TV Special, Other, web]
    public static readonly string FileID = "%fid";
    public static readonly string AnimeID = "%aid";
    public static readonly string EpisodeID = "%eid";
    public static readonly string GroupID = "%gid";
    public static readonly string DubLanguage = "%dub";
    public static readonly string SubLanguage = "%sub";
    public static readonly string VideoCodec = "%vid"; //tracks separated with '
    public static readonly string AudioCodec = "%aud"; //tracks separated with '
    public static readonly string VideoBitDepth = "%bit"; // 8bit, 10bit

    public static readonly string OriginalFileName = "%sna";
    // The original file name as specified by the sub group

    public static readonly string Censored = "%cen";
    public static readonly string Deprecated = "%dep";


    /*
    %md5 / %MD5	 md5 sum (lower/upper)
    %sha / %SHA	 sha1 sum (lower/upper)
    %inv	 Invalid crc string
        * */
}

public struct FileRenameReserved
{
    public static readonly string Do = "DO";
    public static readonly string Fail = "FAIL";
    public static readonly string Add = "ADD";
    public static readonly string Replace = "REPLACE";
    public static readonly string None = "none"; // used for videos with no audio or no subitle languages
    public static readonly string Unknown = "unknown"; // used for videos with no audio or no subitle languages
}

[Renamer(RENAMER_ID, Description = "Legacy")]
public class LegacyRenamer : IRenamer
{
    private const string RENAMER_ID = "Legacy";

    private readonly ILogger<LegacyRenamer> logger;

    public LegacyRenamer(ILogger<LegacyRenamer> logger)
    {
        this.logger = logger;
    }

    public string GetFilename(RenameEventArgs args)
    {
        if (args.Script == null)
        {
            throw new Exception("*Error: No script available for renamer");
        }

        if (args.Script.Type != RENAMER_ID && args.Script.Type != GroupAwareRenamer.RENAMER_ID)
        {
            return null;
        }

        return GetNewFileName(args, args.Script.Script);
    }

    public (IImportFolder destination, string subfolder) GetDestination(MoveEventArgs args)
    {
        if (args.Script == null)
        {
            throw new Exception("*Error: No script available for renamer");
        }

        return GetDestinationFolder(args);
    }

    private static readonly char[] validTests = "AGFEHXRTYDSCIZJWUMN".ToCharArray();

    /* TESTS
    A   int     Anime id
    G   int     Group id
    F   int     File version (ie 1, 2, 3 etc) Can use ! , > , >= , < , <=
    E   text    Episode number
    H   text    Episode Type (E=episode, S=special, T=trailer, C=credit, P=parody, O=other)
    X   text    Total number of episodes
    R   text    Rip source [Blu-ray, unknown, camcorder, TV, DTV, VHS, VCD, SVCD, LD, DVD, HKDVD, www]
    T   text    Type [unknown, TV, OVA, Movie, Other, web]
    Y   int     Year
    D   text    Dub language (one of the audio tracks) [japanese, english, ...]
    S   text    Sub language (one of the subtitle tracks) [japanese, english, ...]
    C   text    Video Codec (one of the video tracks) [H264/AVC, DivX5/6, unknown, VP Other, WMV9 (also WMV3), XviD, ...]
    J   text    Audio Codec (one of the audio tracks) [AC3, FLAC, MP3 CBR, MP3 VBR, Other, unknown, Vorbis (Ogg Vorbis)  ...]
    I   text    Tag has a value. Do not use %, i.e. I(eng) [eng, kan, rom, ...]
    Z   int     Video Bith Depth [8,10]
    W   int     Video Resolution Width [720, 1280, 1920, ...]
    U   int     Video Resolution Height [576, 720, 1080, ...]
    M   null    empty - test whether the file is manually linked
     */

    /* TESTS - Alphabetical
    A   int     Anime id
    C   text    Video Codec (one of the video tracks) [H264/AVC, DivX5/6, unknown, VP Other, WMV9 (also WMV3), XviD, ...]
    D   text    Dub language (one of the audio tracks) [japanese, english, ...]
    E   text    Episode number
    F   int     File version (ie 1, 2, 3 etc) Can use ! , > , >= , < , <=
    G   int     Group id
    H   text    Episode Type (E=episode, S=special, T=trailer, C=credit, P=parody, O=other)
    I   text    Tag has a value. Do not use %, i.e. I(eng) [eng, kan, rom, ...]
    J   text    Audio Codec (one of the audio tracks) [AC3, FLAC, MP3 CBR, MP3 VBR, Other, unknown, Vorbis (Ogg Vorbis)  ...]
    M   null    empty - test whether the file is manually linked
    N   null    empty - test whether the file has any episodes linked to it
    R   text    Rip source [Blu-ray, unknown, camcorder, TV, DTV, VHS, VCD, SVCD, LD, DVD, HKDVD, www]
    S   text    Sub language (one of the subtitle tracks) [japanese, english, ...]
    T   text    Type [unknown, TV, OVA, Movie, Other, web]
    U   int     Video Resolution Height [576, 720, 1080, ...]
    W   int     Video Resolution Width [720, 1280, 1920, ...]
    X   text    Total number of episodes
    Y   int     Year
    Z   int     Video Bith Depth [8,10]
     */

    /// <summary>
    /// Test if the file belongs to the specified anime
    /// </summary>
    /// <param name="test"></param>
    /// <param name="episodes"></param>
    /// <returns></returns>
    private bool EvaluateTestA(string test, List<IEpisodeMetadata> episodes)
    {
        try
        {
            var notCondition = false;
            if (test.Substring(0, 1).Equals("!"))
            {
                notCondition = true;
                test = test.Substring(1, test.Length - 1);
            }

            if (!int.TryParse(test, out var animeID))
            {
                return false;
            }

            if (notCondition)
            {
                return test != episodes[0].ShowId;
            }

            return test == episodes[0].ShowId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test if the file belongs to the specified group
    /// </summary>
    /// <param name="test"></param>
    /// <param name="aniFile"></param>
    /// <returns></returns>
    private bool EvaluateTestG(string test, IAniDBFile aniFile)
    {
        try
        {
            var notCondition = false;
            if (test.Substring(0, 1).Equals("!"))
            {
                notCondition = true;
                test = test.Substring(1, test.Length - 1);
            }

            var groupID = 0;

            //Leave groupID at 0 if "unknown".
            if (!test.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(test, out groupID))
                {
                    return false;
                }
            }

            if (notCondition)
            {
                return groupID != aniFile.ReleaseGroup.Id;
            }

            return groupID == aniFile.ReleaseGroup.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test if the file is manually linked
    /// No test parameter is required
    /// </summary>
    /// <param name="test"></param>
    /// <param name="aniFile"></param>
    /// <param name="episodes"></param>
    /// <returns></returns>
    private bool EvaluateTestM(string test, IAniDBFile aniFile, List<IEpisodeMetadata> episodes)
    {
        try
        {
            var notCondition = !string.IsNullOrEmpty(test) && test.Substring(0, 1).Equals("!");

            // for a file to be manually linked it must NOT have an anifile, but does need episodes attached
            var manuallyLinked = false;
            if (aniFile == null)
            {
                manuallyLinked = true;
                if (episodes == null || episodes.Count == 0)
                {
                    manuallyLinked = false;
                }
            }

            if (notCondition)
            {
                return !manuallyLinked;
            }

            return manuallyLinked;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test if the file has any episodes linked
    /// No test parameter is required
    /// </summary>
    /// <param name="test"></param>
    /// <param name="aniFile"></param>
    /// <param name="episodes"></param>
    /// <returns></returns>
    private bool EvaluateTestN(string test, IAniDBFile aniFile, List<IEpisodeMetadata> episodes)
    {
        try
        {
            var notCondition = !string.IsNullOrEmpty(test) && test.Substring(0, 1).Equals("!");

            var epsLinked = aniFile == null && episodes != null && episodes.Count > 0;

            if (notCondition)
            {
                return !epsLinked;
            }

            return epsLinked;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test if this file has the specified Dub (audio) language
    /// </summary>
    /// <param name="test"></param>
    /// <param name="aniFile"></param>
    /// <returns></returns>
    private bool EvaluateTestD(string test, IAniDBFile aniFile)
    {
        try
        {
            var notCondition = false;
            if (test.Substring(0, 1).Equals("!"))
            {
                notCondition = true;
                test = test.Substring(1, test.Length - 1);
            }

            if (aniFile == null)
            {
                return false;
            }

            return notCondition
                ? aniFile.Languages.All(lan =>
                    !lan.LanguageName.Trim().Equals(test.Trim(), StringComparison.InvariantCultureIgnoreCase))
                : aniFile.Languages.Any(lan =>
                    lan.LanguageName.Trim().Equals(test.Trim(), StringComparison.InvariantCultureIgnoreCase));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test is this files has the specified Sub (subtitle) language
    /// </summary>
    /// <param name="test"></param>
    /// <param name="aniFile"></param>
    /// <returns></returns>
    private bool EvaluateTestS(string test, IAniDBFile aniFile)
    {
        try
        {
            var notCondition = false;
            if (test.Substring(0, 1).Equals("!"))
            {
                notCondition = true;
                test = test.Substring(1, test.Length - 1);
            }

            if (aniFile == null)
            {
                return false;
            }

            if (
                test.Trim()
                    .Equals(FileRenameReserved.None, StringComparison.InvariantCultureIgnoreCase) &&
                aniFile.Media.SubtitleLanguages.Count == 0)
            {
                return !notCondition;
            }

            return notCondition
                ? aniFile.Subtitles.All(lan =>
                    !lan.LanguageName.Trim().Equals(test.Trim(), StringComparison.InvariantCultureIgnoreCase))
                : aniFile.Subtitles.Any(lan =>
                    lan.LanguageName.Trim().Equals(test.Trim(), StringComparison.InvariantCultureIgnoreCase));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test is this files is a specific version
    /// </summary>
    /// <param name="test"></param>
    /// <param name="aniFile"></param>
    /// <returns></returns>
    private bool EvaluateTestF(string test, IAniDBFile aniFile)
    {
        try
        {
            ProcessNumericalOperators(ref test, out var notCondition, out var greaterThan, out var greaterThanEqual,
                out var lessThan, out var lessThanEqual);

            if (aniFile == null)
            {
                return false;
            }

            if (!int.TryParse(test, out var version))
            {
                return false;
            }

            var hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

            if (!hasFileVersionOperator)
            {
                if (!notCondition)
                {
                    return aniFile.FileVersion == version;
                }

                return aniFile.FileVersion != version;
            }

            if (greaterThan)
            {
                return aniFile.FileVersion > version;
            }

            if (greaterThanEqual)
            {
                return aniFile.FileVersion >= version;
            }

            if (lessThan)
            {
                return aniFile.FileVersion < version;
            }

            return aniFile.FileVersion <= version;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test is this file is a specific bit depth
    /// </summary>
    /// <param name="test"></param>
    /// <param name="vid"></param>
    /// <returns></returns>
    private bool EvaluateTestZ(string test, IShokoVideo vid)
    {
        try
        {
            ProcessNumericalOperators(ref test, out var notCondition, out var greaterThan, out var greaterThanEqual,
                out var lessThan, out var lessThanEqual);

            if (!int.TryParse(test, out var testBitDepth))
            {
                return false;
            }

            var videoStream = vid.Media?.Video.FirstOrDefault();
            if (videoStream == null)
            {
                return false;
            }

            var hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

            if (!hasFileVersionOperator)
            {
                if (!notCondition)
                {
                    return testBitDepth == videoStream.BitDepth;
                }

                return testBitDepth != videoStream.BitDepth;
            }

            if (greaterThan)
            {
                return videoStream.BitDepth > testBitDepth;
            }

            if (greaterThanEqual)
            {
                return videoStream.BitDepth >= testBitDepth;
            }

            if (lessThan)
            {
                return videoStream.BitDepth < testBitDepth;
            }

            return videoStream.BitDepth <= testBitDepth;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    private bool EvaluateTestW(string test, IShokoVideo vid)
    {
        try
        {
            ProcessNumericalOperators(ref test, out var notCondition, out var greaterThan, out var greaterThanEqual,
                out var lessThan, out var lessThanEqual);

            if (vid == null)
            {
                return false;
            }

            if (!int.TryParse(test, out var testWidth))
            {
                return false;
            }

            var width = vid.Media?.Video.FirstOrDefault()?.Width ?? 0;

            var hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

            if (!hasFileVersionOperator)
            {
                if (!notCondition)
                {
                    return testWidth == width;
                }

                return testWidth != width;
            }

            if (greaterThan)
            {
                return width > testWidth;
            }

            if (greaterThanEqual)
            {
                return width >= testWidth;
            }

            if (lessThan)
            {
                return width < testWidth;
            }

            return width <= testWidth;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    private bool EvaluateTestU(string test, IShokoVideo vid)
    {
        try
        {
            ProcessNumericalOperators(ref test, out var notCondition, out var greaterThan, out var greaterThanEqual,
                out var lessThan, out var lessThanEqual);

            if (vid == null)
            {
                return false;
            }

            if (!int.TryParse(test, out var testHeight))
            {
                return false;
            }

            var height = vid.Media?.Video.FirstOrDefault()?.Height ?? 0;

            var hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

            if (!hasFileVersionOperator)
            {
                if (!notCondition)
                {
                    return testHeight == height;
                }

                return testHeight != height;
            }

            if (greaterThan)
            {
                return height > testHeight;
            }

            if (greaterThanEqual)
            {
                return height >= testHeight;
            }

            if (lessThan)
            {
                return height < testHeight;
            }

            return height <= testHeight;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
            return false;
        }
    }


    private bool EvaluateTestR(string test, IAniDBFile aniFile)
    {
        try
        {
            var notCondition = false;
            if (test.Substring(0, 1).Equals("!"))
            {
                notCondition = true;
                test = test.Substring(1, test.Length - 1);
            }

            if (aniFile == null)
            {
                return false;
            }

            var hasSource = aniFile.Source != FileSource.Unknown;
            if (
                test.Trim()
                    .Equals(FileRenameReserved.Unknown, StringComparison.InvariantCultureIgnoreCase) &&
                !hasSource)
            {
                return !notCondition;
            }


            if (test.Trim().Equals(aniFile.Source.ToString(), StringComparison.InvariantCultureIgnoreCase))
            {
                return !notCondition;
            }

            return notCondition;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    private bool EvaluateTestT(string test, IShowMetadata anime)
    {
        try
        {
            var notCondition = false;
            if (test.Substring(0, 1).Equals("!"))
            {
                notCondition = true;
                test = test.Substring(1, test.Length - 1);
            }

            var hasType = !string.IsNullOrEmpty(anime.AnimeType.ToRawString());
            if (
                test.Trim()
                    .Equals(FileRenameReserved.Unknown, StringComparison.InvariantCultureIgnoreCase) &&
                !hasType)
            {
                return !notCondition;
            }

            if (test.Trim().Equals(anime.AnimeType.ToRawString(), StringComparison.InvariantCultureIgnoreCase))
            {
                return !notCondition;
            }

            return notCondition;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    private bool EvaluateTestY(string test, IShowMetadata anime)
    {
        try
        {
            ProcessNumericalOperators(ref test, out var notCondition, out var greaterThan, out var greaterThanEqual,
                out var lessThan, out var lessThanEqual);

            if (!int.TryParse(test, out var testYear))
            {
                return false;
            }

            var year = anime.AirDate.HasValue ? anime.AirDate.Value.Year : 0;
            var hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

            if (!hasFileVersionOperator)
            {
                if (!notCondition)
                {
                    return year == testYear;
                }

                return year != testYear;
            }

            if (greaterThan)
            {
                return year > testYear;
            }

            if (greaterThanEqual)
            {
                return year >= testYear;
            }

            if (lessThan)
            {
                return year < testYear;
            }

            return year <= testYear;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    private bool EvaluateTestE(string test, List<IEpisodeMetadata> episodes)
    {
        try
        {
            ProcessNumericalOperators(ref test, out var notCondition, out var greaterThan, out var greaterThanEqual,
                out var lessThan, out var lessThanEqual);

            if (!int.TryParse(test, out var testEpNumber))
            {
                return false;
            }

            var hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

            if (!hasFileVersionOperator)
            {
                if (!notCondition)
                {
                    return episodes[0].Number == testEpNumber;
                }

                return episodes[0].Number != testEpNumber;
            }

            if (greaterThan)
            {
                return episodes[0].Number > testEpNumber;
            }

            if (greaterThanEqual)
            {
                return episodes[0].Number >= testEpNumber;
            }

            if (lessThan)
            {
                return episodes[0].Number < testEpNumber;
            }

            return episodes[0].Number <= testEpNumber;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    private bool EvaluateTestH(string test, List<IEpisodeMetadata> episodes)
    {
        try
        {
            var notCondition = false;
            if (test.Substring(0, 1).Equals("!"))
            {
                notCondition = true;
                test = test.Substring(1, test.Length - 1);
            }

            var epType = string.Empty;
            switch (episodes[0].Type)
            {
                case EpisodeType.Normal:
                    epType = "E";
                    break;
                case EpisodeType.ThemeSong:
                    epType = "C";
                    break;
                case EpisodeType.Other:
                    epType = "O";
                    break;
                case EpisodeType.Parody:
                    epType = "P";
                    break;
                case EpisodeType.Special:
                    epType = "S";
                    break;
                case EpisodeType.Trailer:
                    epType = "T";
                    break;
                default:
                    epType = "U";
                    break;
            }


            if (test.Trim().Equals(epType, StringComparison.InvariantCultureIgnoreCase))
            {
                return !notCondition;
            }

            return notCondition;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Takes the the test parameters and checks for numerical operators
    /// Removes the operators from the test string passed
    /// </summary>
    /// <param name="test"></param>
    /// <param name="notCondition"></param>
    /// <param name="greaterThan"></param>
    /// <param name="greaterThanEqual"></param>
    /// <param name="lessThan"></param>
    /// <param name="lessThanEqual"></param>
    private void ProcessNumericalOperators(ref string test, out bool notCondition, out bool greaterThan,
        out bool greaterThanEqual, out bool lessThan, out bool lessThanEqual)
    {
        notCondition = false;
        if (test.Substring(0, 1).Equals("!"))
        {
            notCondition = true;
            test = test.Substring(1, test.Length - 1);
        }

        greaterThan = false;
        greaterThanEqual = false;
        if (test.Substring(0, 1).Equals(">"))
        {
            greaterThan = true;
            test = test.Substring(1, test.Length - 1);
            if (test.Substring(0, 1).Equals("="))
            {
                greaterThan = false;
                greaterThanEqual = true;
                test = test.Substring(1, test.Length - 1);
            }
        }

        lessThan = false;
        lessThanEqual = false;
        if (!test.Substring(0, 1).Equals("<"))
        {
            return;
        }

        lessThan = true;
        test = test.Substring(1, test.Length - 1);
        if (!test.Substring(0, 1).Equals("="))
        {
            return;
        }

        lessThan = false;
        lessThanEqual = true;
        test = test.Substring(1, test.Length - 1);
    }

    private bool EvaluateTestX(string test, IShowMetadata anime)
    {
        try
        {
            ProcessNumericalOperators(ref test, out var notCondition, out var greaterThan, out var greaterThanEqual,
                out var lessThan, out var lessThanEqual);

            if (!int.TryParse(test, out var epCount))
            {
                return false;
            }

            var normalCount = anime.EpisodeCounts.Normal;
            var hasFileVersionOperator = greaterThan | greaterThanEqual | lessThan | lessThanEqual;

            if (!hasFileVersionOperator)
            {
                if (!notCondition)
                {
                    return normalCount == epCount;
                }

                return normalCount != epCount;
            }

            if (greaterThan)
            {
                return normalCount > epCount;
            }

            if (greaterThanEqual)
            {
                return normalCount >= epCount;
            }

            if (lessThan)
            {
                return normalCount < epCount;
            }

            return normalCount <= epCount;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Test whether the specified tag has a value
    /// </summary>
    /// <param name="test"></param>
    /// <param name="vid"></param>
    /// <param name="aniFile"></param>
    /// <param name="episodes"></param>
    /// <param name="anime"></param>
    /// <returns></returns>
    private bool EvaluateTestI(string test, IShokoVideo vid, IAniDBFile aniFile,
        List<IEpisodeMetadata> episodes,
        IShowMetadata anime)
    {
        try
        {
            var notCondition = false;
            if (test.Substring(0, 1).Equals("!"))
            {
                notCondition = true;
                test = test.Substring(1, test.Length - 1);
            }


            if (anime == null)
            {
                return false;
            }

            #region Test if Anime ID exists

            // Test if Anime ID exists

            var tagAnimeID = FileRenameTag.AnimeID.Substring(1,
                FileRenameTag.AnimeID.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagAnimeID, StringComparison.InvariantCultureIgnoreCase))
            {
                // manually linked files won't have an anime id
                if (aniFile != null)
                {
                    if (notCondition)
                    {
                        return false;
                    }

                    return true;
                }

                if (notCondition)
                {
                    return true;
                }

                return false;
            }

            #endregion

            #region Test if Group ID exists

            // Test if Group ID exists

            var tagGroupID = FileRenameTag.GroupID.Substring(1,
                FileRenameTag.GroupID.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagGroupID, StringComparison.InvariantCultureIgnoreCase))
            {
                // manually linked files won't have an group id
                if (aniFile != null)
                {
                    if (notCondition)
                    {
                        return false;
                    }

                    return true;
                }

                if (notCondition)
                {
                    return false;
                }

                return true;
            }

            #endregion

            #region Test if Original File Name exists

            // Test if Original File Nameexists

            var tagOriginalFileName = FileRenameTag.OriginalFileName.Substring(1,
                FileRenameTag.OriginalFileName.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagOriginalFileName, StringComparison.InvariantCultureIgnoreCase))
            {
                // manually linked files won't have an Original File Name
                if (aniFile != null)
                {
                    if (string.IsNullOrEmpty(aniFile.OriginalFilename))
                    {
                        return notCondition;
                    }

                    return !notCondition;
                }

                return notCondition;
            }

            #endregion

            #region Test if Episode Number exists

            // Test if Episode Number exists
            var tagEpisodeNumber = FileRenameTag.EpisodeNumber.Substring(1,
                FileRenameTag.EpisodeNumber.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagEpisodeNumber, StringComparison.InvariantCultureIgnoreCase))
            {
                // manually linked files won't have an Episode Number
                if (aniFile != null)
                {
                    return !notCondition;
                }

                return notCondition;
            }

            #endregion

            #region Test file version

            // Test if Group Short Name exists - yes it always does
            var tagFileVersion = FileRenameTag.FileVersion.Substring(1,
                FileRenameTag.FileVersion.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagFileVersion, StringComparison.InvariantCultureIgnoreCase))
            {
                // manually linked files won't have an anime id
                if (aniFile != null)
                {
                    return !notCondition;
                }

                return notCondition;
            }

            #endregion

            #region Test if ED2K Upper exists

            // Test if Group Short Name exists - yes it always does
            var tagED2KUpper = FileRenameTag.ED2KUpper.Substring(1,
                FileRenameTag.ED2KUpper.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagED2KUpper, StringComparison.InvariantCultureIgnoreCase))
            {
                return !notCondition;
            }

            #endregion

            #region Test if ED2K Lower exists

            // Test if Group Short Name exists - yes it always does
            var tagED2KLower = FileRenameTag.ED2KLower.Substring(1,
                FileRenameTag.ED2KLower.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagED2KLower, StringComparison.InvariantCultureIgnoreCase))
            {
                return !notCondition;
            }

            #endregion

            #region Test if English title exists

            var tagAnimeNameEnglish = FileRenameTag.AnimeNameEnglish.Substring(1,
                FileRenameTag.AnimeNameEnglish.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagAnimeNameEnglish, StringComparison.InvariantCultureIgnoreCase))
            {
                if (anime.GetTitles().Any(ti =>
                        ti.Language == TextLanguage.English &&
                        (ti.TitleType == TitleType.Main || ti.TitleType == TitleType.Official)))
                {
                    return !notCondition;
                }

                return notCondition;
            }

            #endregion

            #region Test if Kanji title exists

            var tagAnimeNameKanji = FileRenameTag.AnimeNameKanji.Substring(1,
                FileRenameTag.AnimeNameKanji.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagAnimeNameKanji, StringComparison.InvariantCultureIgnoreCase))
            {
                if (anime.GetTitles().Any(ti =>
                        ti.Language == TextLanguage.Japanese &&
                        (ti.TitleType == TitleType.Main || ti.TitleType == TitleType.Official)))
                {
                    return !notCondition;
                }

                return notCondition;
            }

            #endregion

            #region Test if Romaji title exists

            var tagAnimeNameRomaji = FileRenameTag.AnimeNameRomaji.Substring(1,
                FileRenameTag.AnimeNameRomaji.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagAnimeNameRomaji, StringComparison.InvariantCultureIgnoreCase))
            {
                if (anime.GetTitles().Any(ti =>
                        ti.Language == TextLanguage.Romaji &&
                        (ti.TitleType == TitleType.Main || ti.TitleType == TitleType.Official)))
                {
                    return !notCondition;
                }

                return notCondition;
            }

            #endregion

            #region Test if episode name (english) exists

            var tagEpisodeNameEnglish = FileRenameTag.EpisodeNameEnglish.Substring(1,
                FileRenameTag.EpisodeNameEnglish.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagEpisodeNameEnglish, StringComparison.InvariantCultureIgnoreCase))
            {
                var title = RepoFactory.AniDB_Episode_Title
                    .GetByEpisodeIDAndLanguage(episodes[0].EpisodeID, TextLanguage.English)
                    .FirstOrDefault()?.Value;
                if (string.IsNullOrEmpty(title))
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if episode name (romaji) exists

            var tagEpisodeNameRomaji = FileRenameTag.EpisodeNameRomaji.Substring(1,
                FileRenameTag.EpisodeNameRomaji.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagEpisodeNameRomaji, StringComparison.InvariantCultureIgnoreCase))
            {
                var title = RepoFactory.AniDB_Episode_Title
                    .GetByEpisodeIDAndLanguage(episodes[0].EpisodeID, TextLanguage.Romaji)
                    .FirstOrDefault()?.Value;
                if (string.IsNullOrEmpty(title))
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if group name short exists

            // Test if Group Short Name exists - yes it always does
            var tagGroupShortName = FileRenameTag.GroupShortName.Substring(1,
                FileRenameTag.GroupShortName.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagGroupShortName, StringComparison.InvariantCultureIgnoreCase))
            {
                if (string.IsNullOrEmpty(aniFile?.Anime_GroupNameShort))
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if group name long exists

            // Test if Group Short Name exists - yes it always does
            var tagGroupLongName = FileRenameTag.GroupLongName.Substring(1,
                FileRenameTag.GroupLongName.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagGroupLongName, StringComparison.InvariantCultureIgnoreCase))
            {
                if (string.IsNullOrEmpty(aniFile?.Anime_GroupName))
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if CRC Lower exists

            // Test if Group Short Name exists - yes it always does
            var tagCRCLower = FileRenameTag.CRCLower.Substring(1,
                FileRenameTag.CRCLower.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagCRCLower, StringComparison.InvariantCultureIgnoreCase))
            {
                var crc = vid.CRC32;

                if (string.IsNullOrEmpty(crc))
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if CRC Upper exists

            // Test if Group Short Name exists - yes it always does
            var tagCRCUpper = FileRenameTag.CRCUpper.Substring(1,
                FileRenameTag.CRCUpper.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagCRCUpper, StringComparison.InvariantCultureIgnoreCase))
            {
                var crc = vid.CRC32;

                if (string.IsNullOrEmpty(crc))
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test file has an audio track

            var tagDubLanguage = FileRenameTag.DubLanguage.Substring(1,
                FileRenameTag.DubLanguage.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagDubLanguage, StringComparison.InvariantCultureIgnoreCase))
            {
                if (aniFile == null || aniFile.Languages.Count == 0)
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test file has a subtitle track

            var tagSubLanguage = FileRenameTag.SubLanguage.Substring(1,
                FileRenameTag.SubLanguage.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagSubLanguage, StringComparison.InvariantCultureIgnoreCase))
            {
                if (aniFile == null || aniFile.Subtitles.Count == 0)
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if Video resolution exists

            var tagVidRes = FileRenameTag.Resolution.Substring(1,
                FileRenameTag.Resolution.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagVidRes, StringComparison.InvariantCultureIgnoreCase))
            {
                var vidRes = string.Empty;

                if (string.IsNullOrEmpty(vidRes) && vid != null)
                {
                    vidRes = vid.VideoResolution;
                }

                if (string.IsNullOrEmpty(vidRes))
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test file has a video codec defined

            var tagVideoCodec = FileRenameTag.VideoCodec.Substring(1,
                FileRenameTag.VideoCodec.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagVideoCodec, StringComparison.InvariantCultureIgnoreCase))
            {
                return notCondition;
            }

            #endregion

            #region Test file has an audio codec defined

            var tagAudioCodec = FileRenameTag.AudioCodec.Substring(1,
                FileRenameTag.AudioCodec.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagAudioCodec, StringComparison.InvariantCultureIgnoreCase))
            {
                return notCondition;
            }

            #endregion

            #region Test file has Video Bit Depth defined

            var tagVideoBitDepth = FileRenameTag.VideoBitDepth.Substring(1,
                FileRenameTag.VideoBitDepth.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagVideoBitDepth, StringComparison.InvariantCultureIgnoreCase))
            {
                var bitDepthExists = vid?.Media?.VideoStream != null && vid.Media?.VideoStream?.BitDepth != 0;
                if (!bitDepthExists)
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if censored

            var tagCensored = FileRenameTag.Censored.Substring(1,
                FileRenameTag.Censored.Length - 1); // remove % at the front
            if (test.Trim().Equals(tagCensored, StringComparison.InvariantCultureIgnoreCase))
            {
                var isCensored = false;
                if (aniFile != null)
                {
                    isCensored = aniFile.IsCensored ?? false;
                }

                if (!isCensored)
                {
                    return notCondition;
                }

                return !notCondition;
            }

            #endregion

            #region Test if Deprecated

            var tagDeprecated = FileRenameTag.Deprecated.Substring(1,
                FileRenameTag.Deprecated.Length - 1); // remove % at the front
            if (!test.Trim().Equals(tagDeprecated, StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            var isDeprecated = false;
            if (aniFile != null)
            {
                isDeprecated = aniFile.IsDeprecated;
            }

            if (!isDeprecated)
            {
                return notCondition;
            }

            return !notCondition;

            #endregion=
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return false;
        }
    }

    public static string GetNewFileName(RenameEventArgs args, string script)
    {
        // Cheat and just look it up by location to avoid rewriting this whole file.
        var sourceFolder = RepoFactory.ImportFolder.GetAll()
            .FirstOrDefault(a => args.FileInfo.FilePath.StartsWith(a.ImportFolderLocation));
        if (sourceFolder == null)
        {
            throw new Exception("*Unable to get import folder");
        }

        var location = args.VideoLocation;
        var video = args.Video;
        var lines = script.Split(Environment.NewLine.ToCharArray());

        var newFileName = string.Empty;

        var episodes = new List<AniDB_Episode>();
        AniDB_Anime anime;

        if (video == null)
        {
            throw new Exception("*Error: Unable to access file");
        }

        // get all the data so we don't need to get multiple times
        var aniFile = video.GetAniDBFile();
        if (aniFile == null)
        {
            var animeEps = video.GetAnimeEpisodes();
            if (animeEps.Count == 0)
            {
                throw new Exception("*Error: Unable to get episode for file");
            }

            episodes.AddRange(animeEps.Select(a => a.AniDB_Episode).OrderBy(a => a.EpisodeType)
                .ThenBy(a => a.EpisodeNumber));

            anime = RepoFactory.AniDB_Anime.GetByAnimeID(episodes[0].AnimeID);
            if (anime == null)
            {
                throw new Exception("*Error: Unable to get anime for file");
            }
        }
        else
        {
            episodes = aniFile.Episodes;
            if (episodes.Count == 0)
            {
                throw new Exception("*Error: Unable to get episode for file");
            }

            anime = RepoFactory.AniDB_Anime.GetByAnimeID(episodes[0].AnimeID);
            if (anime == null)
            {
                throw new Exception("*Error: Unable to get anime for file");
            }
        }

        foreach (var line in lines)
        {
            var thisLine = line.Trim();
            if (thisLine.Length == 0)
            {
                continue;
            }

            // remove all comments from this line
            var comPos = thisLine.IndexOf("//", StringComparison.Ordinal);
            if (comPos >= 0)
            {
                thisLine = thisLine.Substring(0, comPos);
            }


            // check if this line has no tests (applied to all files)
            if (thisLine.StartsWith(FileRenameReserved.Do, StringComparison.InvariantCultureIgnoreCase))
            {
                var action = GetAction(thisLine);
                PerformActionOnFileName(ref newFileName, action, video, aniFile, episodes, anime);
            }
            else if (EvaluateTest(thisLine, video, aniFile, episodes, anime))
            {
                // if the line has passed the tests, then perform the action

                var action = GetAction(thisLine);

                // if the action is fail, we don't want to rename
                if (action.ToUpper()
                    .Trim()
                    .Equals(FileRenameReserved.Fail, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new Exception("*Error: The script called FAIL");
                }

                PerformActionOnFileName(ref newFileName, action, video, aniFile, episodes, anime);
            }
        }

        if (string.IsNullOrEmpty(newFileName))
        {
            throw new Exception("*Error: the new filename is empty (script error)");
        }

        var pathToVid = location.FilePath;
        if (string.IsNullOrEmpty(pathToVid))
        {
            throw new Exception("*Error: Unable to get the file's old filename");
        }

        var ext =
            Path.GetExtension(pathToVid); //Prefer VideoLocal_Place as this is more accurate.
        if (string.IsNullOrEmpty(ext))
        {
            throw
                new Exception(
                    "*Error: Unable to get the file's extension"); // fail if we get a blank extension, something went wrong.
        }

        // finally add back the extension
        return Utils.ReplaceInvalidFolderNameCharacters($"{newFileName.Replace("`", "'")}{ext}");
    }

    private static void PerformActionOnFileName(ref string newFileName, string action, SVR_VideoLocal vid,
        SVR_AniDB_File aniFile, List<AniDB_Episode> episodes, AniDB_Anime anime)
    {
        // find the first test
        var posStart = action.IndexOf(" ", StringComparison.Ordinal);
        if (posStart < 0)
        {
            return;
        }

        var actionType = action.Substring(0, posStart);
        var parameter = action.Substring(posStart + 1, action.Length - posStart - 1);


        // action is to add the the new file name
        if (actionType.Trim()
            .Equals(FileRenameReserved.Add, StringComparison.InvariantCultureIgnoreCase))
        {
            PerformActionOnFileNameADD(ref newFileName, parameter, vid, aniFile, episodes, anime);
        }

        if (actionType.Trim()
            .Equals(FileRenameReserved.Replace, StringComparison.InvariantCultureIgnoreCase))
        {
            PerformActionOnFileNameREPLACE(ref newFileName, parameter);
        }
    }

    private static void PerformActionOnFileNameREPLACE(ref string newFileName, string action)
    {
        try
        {
            action = action.Trim();

            var posStart1 = action.IndexOf("'", 0, StringComparison.Ordinal);
            if (posStart1 < 0)
            {
                return;
            }

            var posEnd1 = action.IndexOf("'", posStart1 + 1, StringComparison.Ordinal);
            if (posEnd1 < 0)
            {
                return;
            }

            var toReplace = action.Substring(posStart1 + 1, posEnd1 - posStart1 - 1);

            var posStart2 = action.IndexOf("'", posEnd1 + 1, StringComparison.Ordinal);
            if (posStart2 < 0)
            {
                return;
            }

            var posEnd2 = action.IndexOf("'", posStart2 + 1, StringComparison.Ordinal);
            if (posEnd2 < 0)
            {
                return;
            }

            var replaceWith = action.Substring(posStart2 + 1, posEnd2 - posStart2 - 1);

            newFileName = newFileName.Replace(toReplace, replaceWith);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }
    }

    private static void PerformActionOnFileNameADD(ref string newFileName, string action, SVR_VideoLocal vid,
        SVR_AniDB_File aniFile, List<AniDB_Episode> episodes, AniDB_Anime anime)
    {
        newFileName += action;
        newFileName = newFileName.Replace("'", string.Empty);

        #region Anime ID

        if (action.Trim().ToLower().Contains(FileRenameTag.AnimeID.ToLower()))
        {
            newFileName = newFileName.Replace(FileRenameTag.AnimeID, anime.AnimeID.ToString());
        }

        #endregion

        #region English title

        if (action.Trim().ToLower().Contains(FileRenameTag.AnimeNameEnglish.ToLower()))
        {
            newFileName = anime.GetTitles()
                .Where(ti =>
                    ti.Language == TextLanguage.English &&
                    (ti.TitleType == TitleType.Main || ti.TitleType == TitleType.Official))
                .Aggregate(newFileName,
                    (current, ti) => current.Replace(FileRenameTag.AnimeNameEnglish, ti.Value));
        }

        #endregion

        #region Romaji title

        if (action.Trim().ToLower().Contains(FileRenameTag.AnimeNameRomaji.ToLower()))
        {
            newFileName = anime.GetTitles()
                .Where(ti =>
                    ti.Language == TextLanguage.Romaji &&
                    (ti.TitleType == TitleType.Main || ti.TitleType == TitleType.Official))
                .Aggregate(newFileName,
                    (current, ti) => current.Replace(FileRenameTag.AnimeNameRomaji, ti.Value));
        }

        #endregion

        #region Kanji title

        if (action.Trim().ToLower().Contains(FileRenameTag.AnimeNameKanji.ToLower()))
        {
            newFileName = anime.GetTitles()
                .Where(ti =>
                    ti.Language == TextLanguage.Japanese &&
                    (ti.TitleType == TitleType.Main || ti.TitleType == TitleType.Official))
                .Aggregate(newFileName,
                    (current, ti) => current.Replace(FileRenameTag.AnimeNameKanji, ti.Value));
        }

        #endregion

        #region Episode Number

        if (action.Trim().ToLower().Contains(FileRenameTag.EpisodeNumber.ToLower()))
        {
            var prefix = string.Empty;
            int epCount;

            if (episodes[0].GetEpisodeTypeEnum() == EpisodeType.Credits)
            {
                prefix = "C";
            }

            if (episodes[0].GetEpisodeTypeEnum() == EpisodeType.Other)
            {
                prefix = "O";
            }

            if (episodes[0].GetEpisodeTypeEnum() == EpisodeType.Parody)
            {
                prefix = "P";
            }

            if (episodes[0].GetEpisodeTypeEnum() == EpisodeType.Special)
            {
                prefix = "S";
            }

            if (episodes[0].GetEpisodeTypeEnum() == EpisodeType.Trailer)
            {
                prefix = "T";
            }

            switch (episodes[0].GetEpisodeTypeEnum())
            {
                case EpisodeType.Episode:
                    epCount = anime.EpisodeCountNormal;
                    break;
                case EpisodeType.Special:
                    epCount = anime.EpisodeCountSpecial;
                    break;
                case EpisodeType.Credits:
                case EpisodeType.Trailer:
                case EpisodeType.Parody:
                case EpisodeType.Other:
                    epCount = 1;
                    break;
                default:
                    epCount = 1;
                    break;
            }

            var zeroPadding = Math.Max(epCount.ToString().Length, 2);

            // normal episode
            var episodeNumber = prefix + episodes[0].EpisodeNumber.ToString().PadLeft(zeroPadding, '0');

            if (episodes.Count > 1)
            {
                episodeNumber += "-" +
                                 episodes[episodes.Count - 1].EpisodeNumber.ToString().PadLeft(zeroPadding, '0');
            }

            newFileName = newFileName.Replace(FileRenameTag.EpisodeNumber, episodeNumber);
        }

        #endregion

        #region Episode Number

        if (action.Trim().ToLower().Contains(FileRenameTag.Episodes.ToLower()))
        {
            int epCount;

            switch (episodes[0].GetEpisodeTypeEnum())
            {
                case EpisodeType.Episode:
                    epCount = anime.EpisodeCountNormal;
                    break;
                case EpisodeType.Special:
                    epCount = anime.EpisodeCountSpecial;
                    break;
                case EpisodeType.Credits:
                case EpisodeType.Trailer:
                case EpisodeType.Parody:
                case EpisodeType.Other:
                    epCount = 1;
                    break;
                default:
                    epCount = 1;
                    break;
            }

            var zeroPadding = epCount.ToString().Length;

            var episodeNumber = epCount.ToString().PadLeft(zeroPadding, '0');

            newFileName = newFileName.Replace(FileRenameTag.Episodes, episodeNumber);
        }

        #endregion

        #region Episode name (english)

        if (action.Trim().ToLower().Contains(FileRenameTag.EpisodeNameEnglish.ToLower()))
        {
            var epname = RepoFactory.AniDB_Episode_Title
                .GetByEpisodeIDAndLanguage(episodes[0].EpisodeID, TextLanguage.English)
                .FirstOrDefault()?.Value;
            var settings = Utils.SettingsProvider.GetSettings();
            if (epname?.Length > settings.LegacyRenamerMaxEpisodeLength)
            {
                epname = epname.Substring(0, settings.LegacyRenamerMaxEpisodeLength - 1) + "…";
            }

            newFileName = newFileName.Replace(FileRenameTag.EpisodeNameEnglish, epname);
        }

        #endregion

        #region Episode name (romaji)

        if (action.Trim().ToLower().Contains(FileRenameTag.EpisodeNameRomaji.ToLower()))
        {
            var epname = RepoFactory.AniDB_Episode_Title
                .GetByEpisodeIDAndLanguage(episodes[0].EpisodeID, TextLanguage.Romaji)
                .FirstOrDefault()?.Value;
            var settings = Utils.SettingsProvider.GetSettings();
            if (epname?.Length > settings.LegacyRenamerMaxEpisodeLength)
            {
                epname = epname.Substring(0, settings.LegacyRenamerMaxEpisodeLength - 1) + "…";
            }

            newFileName = newFileName.Replace(FileRenameTag.EpisodeNameRomaji, epname);
        }

        #endregion

        #region sub group name (short)

        if (action.Trim().ToLower().Contains(FileRenameTag.GroupShortName.ToLower()))
        {
            var subgroup = aniFile?.Anime_GroupNameShort ?? "Unknown";
            if (subgroup.Equals("raw", StringComparison.InvariantCultureIgnoreCase))
            {
                subgroup = "Unknown";
            }

            newFileName = newFileName.Replace(FileRenameTag.GroupShortName, subgroup);
        }

        #endregion

        #region sub group name (long)

        if (action.Trim().ToLower().Contains(FileRenameTag.GroupLongName.ToLower()))
        {
            newFileName = newFileName.Replace(FileRenameTag.GroupLongName,
                aniFile?.Anime_GroupName ?? "Unknown");
        }

        #endregion

        #region ED2k hash (upper)

        if (action.Trim().Contains(FileRenameTag.ED2KUpper))
        {
            newFileName = newFileName.Replace(FileRenameTag.ED2KUpper, vid.Hash.ToUpper());
        }

        #endregion

        #region ED2k hash (lower)

        if (action.Trim().Contains(FileRenameTag.ED2KLower))
        {
            newFileName = newFileName.Replace(FileRenameTag.ED2KLower, vid.Hash.ToLower());
        }

        #endregion

        #region CRC (upper)

        if (action.Trim().Contains(FileRenameTag.CRCUpper))
        {
            var crc = vid.CRC32;

            if (!string.IsNullOrEmpty(crc))
            {
                crc = crc.ToUpper();
                newFileName = newFileName.Replace(FileRenameTag.CRCUpper, crc);
            }
        }

        #endregion

        #region CRC (lower)

        if (action.Trim().Contains(FileRenameTag.CRCLower))
        {
            var crc = vid.CRC32;

            if (!string.IsNullOrEmpty(crc))
            {
                crc = crc.ToLower();
                newFileName = newFileName.Replace(FileRenameTag.CRCLower, crc);
            }
        }

        #endregion

        #region File Version

        if (action.Trim().Contains(FileRenameTag.FileVersion))
        {
            newFileName = newFileName.Replace(FileRenameTag.FileVersion,
                aniFile?.FileVersion.ToString() ?? "1");
        }

        #endregion

        #region Audio languages (dub)

        if (action.Trim().Contains(FileRenameTag.DubLanguage))
        {
            newFileName =
                newFileName.Replace(FileRenameTag.DubLanguage, aniFile?.LanguagesRAW ?? string.Empty);
        }

        #endregion

        #region Subtitle languages (sub)

        if (action.Trim().Contains(FileRenameTag.SubLanguage))
        {
            newFileName =
                newFileName.Replace(FileRenameTag.SubLanguage, aniFile?.SubtitlesRAW ?? string.Empty);
        }

        #endregion

        #region Video Codec

        if (action.Trim().Contains(FileRenameTag.VideoCodec))
        {
            newFileName = newFileName.Replace(FileRenameTag.VideoCodec, vid?.Media?.VideoStream?.CodecID);
        }

        #endregion

        #region Audio Codec

        if (action.Trim().Contains(FileRenameTag.AudioCodec))
        {
            newFileName = newFileName.Replace(FileRenameTag.AudioCodec,
                vid?.Media?.AudioStreams.FirstOrDefault()?.CodecID);
        }

        #endregion

        #region Video Bit Depth

        if (action.Trim().Contains(FileRenameTag.VideoBitDepth))
        {
            newFileName = newFileName.Replace(FileRenameTag.VideoBitDepth,
                (vid?.Media?.VideoStream?.BitDepth).ToString());
        }

        #endregion

        #region Video Source

        if (action.Trim().Contains(FileRenameTag.Source))
        {
            newFileName = newFileName.Replace(FileRenameTag.Source, aniFile?.File_Source ?? "Unknown");
        }

        #endregion

        #region Anime Type

        if (action.Trim().Contains(FileRenameTag.Type))
        {
            newFileName = newFileName.Replace(FileRenameTag.Type, anime.GetAnimeTypeRAW());
        }

        #endregion

        #region Video Resolution

        if (action.Trim().Contains(FileRenameTag.Resolution))
        {
            var res = string.Empty;
            // try the video info
            if (vid != null)
            {
                res = vid.VideoResolution;
            }

            newFileName = newFileName.Replace(FileRenameTag.Resolution, res.Trim());
        }

        #endregion

        #region Video Height

        if (action.Trim().Contains(FileRenameTag.VideoHeight))
        {
            var res = string.Empty;
            // try the video info
            if (vid != null)
            {
                res = vid.VideoResolution;
            }

            var reses = res.Split('x');
            if (reses.Length > 1)
            {
                res = reses[1];
            }

            newFileName = newFileName.Replace(FileRenameTag.VideoHeight, res);
        }

        #endregion

        #region Year

        if (action.Trim().Contains(FileRenameTag.Year))
        {
            newFileName = newFileName.Replace(FileRenameTag.Year, anime.BeginYear.ToString());
        }

        #endregion

        #region File ID

        if (action.Trim().Contains(FileRenameTag.FileID))
        {
            if (aniFile != null)
            {
                newFileName = newFileName.Replace(FileRenameTag.FileID, aniFile.FileID.ToString());
            }
        }

        #endregion

        #region Episode ID

        if (action.Trim().Contains(FileRenameTag.EpisodeID))
        {
            newFileName = newFileName.Replace(FileRenameTag.EpisodeID, episodes[0].EpisodeID.ToString());
        }

        #endregion

        #region Group ID

        if (action.Trim().Contains(FileRenameTag.GroupID))
        {
            newFileName =
                newFileName.Replace(FileRenameTag.GroupID, aniFile?.GroupID.ToString() ?? "Unknown");
        }

        #endregion

        #region Original File Name

        if (action.Trim().Contains(FileRenameTag.OriginalFileName))
        {
            // remove the extension first
            if (aniFile != null)
            {
                var ext = Path.GetExtension(aniFile.FileName);
                var partial = aniFile.FileName.Substring(0, aniFile.FileName.Length - ext.Length);

                newFileName = newFileName.Replace(FileRenameTag.OriginalFileName, partial);
            }
        }

        #endregion

        #region Censored

        if (action.Trim().Contains(FileRenameTag.Censored))
        {
            var censored = "cen";
            if (aniFile?.IsCensored ?? false)
            {
                censored = "unc";
            }

            newFileName = newFileName.Replace(FileRenameTag.Censored, censored);
        }

        #endregion

        #region Deprecated

        if (action.Trim().Contains(FileRenameTag.Deprecated))
        {
            var depr = "New";
            if (aniFile?.IsDeprecated ?? false)
            {
                depr = "DEPR";
            }

            newFileName = newFileName.Replace(FileRenameTag.Deprecated, depr);
        }

        #endregion
    }

    private static string GetAction(string line)
    {
        // find the first test
        var posStart = line.IndexOf("DO ", StringComparison.Ordinal);
        if (posStart < 0)
        {
            return string.Empty;
        }

        var action = line.Substring(posStart + 3, line.Length - posStart - 3);
        return action;
    }

    private static bool EvaluateTest(string line, SVR_VideoLocal vid, SVR_AniDB_File aniFile,
        List<AniDB_Episode> episodes,
        AniDB_Anime anime)
    {
        line = line.Trim();
        // determine if this line has a test
        foreach (var c in validTests)
        {
            var prefix = $"IF {c}(";
            if (!line.ToUpper().StartsWith(prefix))
            {
                continue;
            }

            // find the first test
            var posStart = line.IndexOf('(');
            var posEnd = line.IndexOf(')');
            var posStartOrig = posStart;

            if (posEnd < posStart)
            {
                return false;
            }

            var condition = line.Substring(posStart + 1, posEnd - posStart - 1);
            var passed = EvaluateTest(c, condition, vid, aniFile, episodes, anime);

            // check for OR's and AND's
            while (posStart > 0)
            {
                posStart = line.IndexOf(';', posStart);
                if (posStart <= 0)
                {
                    continue;
                }

                var thisLineRemainder = line.Substring(posStart + 1, line.Length - posStart - 1).Trim();
                // remove any spacing
                //char thisTest = line.Substring(posStart + 1, 1).ToCharArray()[0];
                var thisTest = thisLineRemainder.Substring(0, 1).ToCharArray()[0];

                var posStartNew = thisLineRemainder.IndexOf('(');
                var posEndNew = thisLineRemainder.IndexOf(')');
                condition = thisLineRemainder.Substring(posStartNew + 1, posEndNew - posStartNew - 1);

                var thisPassed = EvaluateTest(thisTest, condition, vid, aniFile, episodes, anime);

                if (!passed || !thisPassed)
                {
                    return false;
                }

                posStart = posStart + 1;
            }

            // if the first test passed, and we only have OR's left then it is an automatic success
            if (passed)
            {
                return true;
            }

            posStart = posStartOrig;
            while (posStart > 0)
            {
                posStart = line.IndexOf(',', posStart);
                if (posStart <= 0)
                {
                    continue;
                }

                var thisLineRemainder =
                    line.Substring(posStart + 1, line.Length - posStart - 1).Trim();
                // remove any spacing
                //char thisTest = line.Substring(posStart + 1, 1).ToCharArray()[0];
                var thisTest = thisLineRemainder.Substring(0, 1).ToCharArray()[0];

                var posStartNew = thisLineRemainder.IndexOf('(');
                var posEndNew = thisLineRemainder.IndexOf(')');
                condition = thisLineRemainder.Substring(posStartNew + 1, posEndNew - posStartNew - 1);

                var thisPassed = EvaluateTest(thisTest, condition, vid, aniFile, episodes, anime);

                if (thisPassed)
                {
                    return true;
                }

                posStart = posStart + 1;
            }
        }

        return false;
    }

    private static bool EvaluateTest(char testChar, string testCondition, IShokoVideo vid,
        IAniDBFile aniFile,
        List<IEpisodeMetadata> episodes, IShowMetadata anime)
    {
        testCondition = testCondition.Trim();

        switch (testChar)
        {
            case 'A':
                return EvaluateTestA(testCondition, episodes);
            case 'G':
                return EvaluateTestG(testCondition, aniFile);
            case 'D':
                return EvaluateTestD(testCondition, aniFile);
            case 'S':
                return EvaluateTestS(testCondition, aniFile);
            case 'F':
                return EvaluateTestF(testCondition, aniFile);
            case 'R':
                return EvaluateTestR(testCondition, aniFile);
            case 'Z':
                return EvaluateTestZ(testCondition, vid);
            case 'T':
                return EvaluateTestT(testCondition, anime);
            case 'Y':
                return EvaluateTestY(testCondition, anime);
            case 'E':
                return EvaluateTestE(testCondition, episodes);
            case 'H':
                return EvaluateTestH(testCondition, episodes);
            case 'X':
                return EvaluateTestX(testCondition, anime);
            case 'C':
                return false;
            case 'J':
                return false;
            case 'I':
                return EvaluateTestI(testCondition, vid, aniFile, episodes, anime);
            case 'W':
                return EvaluateTestW(testCondition, vid);
            case 'U':
                return EvaluateTestU(testCondition, vid);
            case 'M':
                return EvaluateTestM(testCondition, aniFile, episodes);
            case 'N':
                return EvaluateTestN(testCondition, aniFile, episodes);
            default:
                return false;
        }
    }

    public (IImportFolder dest, string folder) GetDestinationFolder(MoveEventArgs args)
    {
        IImportFolder destFolder = null;
        var settings = Utils.SettingsProvider.GetSettings();
        foreach (var fldr in RepoFactory.ImportFolder.GetAll())
        {
            if (!fldr.FolderIsDropDestination)
            {
                continue;
            }

            if (fldr.FolderIsDropSource)
            {
                continue;
            }

            if (!Directory.Exists(fldr.ImportFolderLocation))
            {
                continue;
            }

            // Continue if on a separate drive and there's no space
            if (!settings.Import.SkipDiskSpaceChecks &&
                !args.VideoLocation.AbsolutePath.StartsWith(Path.GetPathRoot(fldr.ImportFolderLocation)))
            {
                var available = 0L;
                try
                {
                    available = new DriveInfo(fldr.ImportFolderLocation).AvailableFreeSpace;
                }
                catch (Exception e)
                {
                    logger.Error(e);
                }

                if (available < args.Video.Size)
                {
                    continue;
                }
            }

            destFolder = fldr;
            break;
        }

        var xrefs = args.CrossReferences;
        if (xrefs.Count == 0)
        {
            return (null, "No xrefs");
        }

        var xref = xrefs.FirstOrDefault(a => a != null);
        if (xref == null)
        {
            return (null, "No xrefs");
        }

        // find the series associated with this episode
        var series = xref.Series;
        if (series == null)
        {
            return (null, "Series not Found");
        }

        // sort the episodes by air date, so that we will move the file to the location of the latest episode
        var allEps = series.AllEpisodes
            .OrderByDescending(a => a.AniDBEpisode.AirDate)
            .ToList();

        foreach (var ep in allEps)
        {
            // check if this episode belongs to more than one anime
            // if it does we will ignore it
            var fileEpXrefs = ep.AllCrossReferences;
            int? seriesID = null;
            var crossOver = false;
            foreach (var fileEpXref in fileEpXrefs)
            {
                if (!seriesID.HasValue)
                {
                    seriesID = fileEpXref.SeriesId;
                }
                else
                {
                    if (seriesID.Value != fileEpXref.SeriesId)
                    {
                        crossOver = true;
                    }
                }
            }

            if (crossOver)
            {
                continue;
            }

            foreach (var vid in ep.AllVideos
                         .Where(a => a.AllLocations.Any(b => b.ImportFolder.Type.HasFlag(ImportFolderType.Excluded))).ToList())
            {
                if (vid.Hashes.ED2K == args.Video.Hashes.ED2K)
                {
                    continue;
                }

                var place = vid.AllLocations.FirstOrDefault();
                var thisFileName = place?.RelativePath;
                if (thisFileName == null)
                {
                    continue;
                }

                var folderName = Path.GetDirectoryName(thisFileName);

                var dstImportFolder = place.ImportFolder;
                if (dstImportFolder == null)
                {
                    continue;
                }

                // check space
                if (!args.VideoLocation.AbsolutePath.StartsWith(Path.GetPathRoot(dstImportFolder.Path)) &&
                    !settings.Import.SkipDiskSpaceChecks)
                {
                    var available = 0L;
                    try
                    {
                        available = new DriveInfo(dstImportFolder.Path).AvailableFreeSpace;
                    }
                    catch (Exception e)
                    {
                        logger.Error(e);
                    }

                    if (available < vid.Size)
                    {
                        continue;
                    }
                }

                if (!Directory.Exists(Path.Combine(place.ImportFolder.Path, folderName)))
                {
                    continue;
                }

                // ensure we aren't moving to the current directory
                if (Path.Combine(place.ImportFolder.Path, folderName).Equals(
                        Path.GetDirectoryName(args.VideoLocation.AbsolutePath), StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                destFolder = place.ImportFolder;

                return (destFolder, folderName);
            }
        }

        if (destFolder == null)
        {
            return (null, "Unable to resolve a destination");
        }

        return (destFolder, Utils.ReplaceInvalidFolderNameCharacters(series.PreferredTitle.Value));
    }
}
