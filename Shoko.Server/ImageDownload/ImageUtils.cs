using System.IO;
using Shoko.Server.Utilities;

namespace Shoko.Server.ImageDownload;

public class ImageUtils
{
    public static string ResolvePath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return null;

        var filePath = Path.Join(Path.TrimEndingDirectorySeparator(GetBaseImagesPath()), relativePath);
        var dirPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return filePath;
    }

    public static string GetBaseImagesPath()
    {
        var settings = Utils.SettingsProvider?.GetSettings();
        var baseDirPath = !string.IsNullOrEmpty(settings?.ImagesPath) ?
            Path.Combine(Utils.ApplicationPath, settings.ImagesPath) : Utils.DefaultImagePath;
        if (!Directory.Exists(baseDirPath))
            Directory.CreateDirectory(baseDirPath);

        return baseDirPath;
    }

    public static string GetBaseAniDBImagesPath()
    {
        var dirPath = Path.Combine(GetBaseImagesPath(), "AniDB");
        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    public static string GetBaseAniDBCharacterImagesPath()
    {
        var dirPath = Path.Combine(GetBaseImagesPath(), "AniDB_Char");
        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    public static string GetBaseAniDBCreatorImagesPath()
    {
        var dirPath = Path.Combine(GetBaseImagesPath(), "AniDB_Creator");

        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    public static string GetBaseTvDBImagesPath()
    {
        var dirPath = Path.Combine(GetBaseImagesPath(), "TvDB");
        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    public static string GetBaseMovieDBImagesPath()
    {
        var dirPath = Path.Combine(GetBaseImagesPath(), "MovieDB");
        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    public static string GetBaseTraktImagesPath()
    {
        var dirPath = Path.Combine(GetBaseImagesPath(), "Trakt");
        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    public static string GetImagesTempFolder()
    {
        var dirPath = Path.Combine(GetBaseImagesPath(), "_Temp_");

        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    public static string GetAniDBCharacterImagePath(int charID)
    {
        var sid = charID.ToString();
        var subFolder = sid.Length == 1 ? sid : sid[..2];
        var dirPath = Path.Combine(GetBaseAniDBCharacterImagesPath(), subFolder);

        if (!Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
        }

        return dirPath;
    }

    public static string GetAniDBCreatorImagePath(int creatorID)
    {
        var sid = creatorID.ToString();
        var subFolder = sid.Length == 1 ? sid : sid[..2];
        var dirPath = Path.Combine(GetBaseAniDBCreatorImagesPath(), subFolder);
        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    public static string GetAniDBImagePath(int animeID)
    {
        var sid = animeID.ToString();
        var subFolder = sid.Length == 1 ? sid : sid[..2];
        var dirPath = Path.Combine(GetBaseAniDBImagesPath(), subFolder);
        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    public static string GetTvDBImagePath()
    {
        var dirPath = GetBaseTvDBImagesPath();
        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    public static string GetMovieDBImagePath()
    {
        var dirPath = GetBaseMovieDBImagesPath();
        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }

    public static string GetTraktImagePath()
    {
        var dirPath = GetBaseTraktImagesPath();
        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        return dirPath;
    }
}
