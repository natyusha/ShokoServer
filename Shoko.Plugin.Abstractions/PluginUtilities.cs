using System;

#nullable enable
namespace Shoko.Plugin.Abstractions;

public static class PluginUtilities
{
    public static string RemoveInvalidPathCharacters(this string path)
    {
        string ret = path.Replace(@"*", string.Empty);
        ret = ret.Replace(@"|", string.Empty);
        ret = ret.Replace(@"\", string.Empty);
        ret = ret.Replace(@"/", string.Empty);
        ret = ret.Replace(@":", string.Empty);
        ret = ret.Replace("\"", string.Empty); // double quote
        ret = ret.Replace(@">", string.Empty);
        ret = ret.Replace(@"<", string.Empty);
        ret = ret.Replace(@"?", string.Empty);
        while (ret.EndsWith("."))
            ret = ret.Substring(0, ret.Length - 1);
        return ret.Trim();
    }

    public static string ReplaceInvalidPathCharacters(this string path)
    {
        string ret = path.Replace(@"*", "\u2605"); // ★ (BLACK STAR)
        ret = ret.Replace(@"|", "\u00a6"); // ¦ (BROKEN BAR)
        ret = ret.Replace(@"\", "\u29F9"); // ⧹ (BIG REVERSE SOLIDUS)
        ret = ret.Replace(@"/", "\u29F8"); // ⧸ (BIG SOLIDUS)
        ret = ret.Replace(@":", "\u0589"); // ։ (ARMENIAN FULL STOP)
        ret = ret.Replace("\"", "\u2033"); // ″ (DOUBLE PRIME)
        ret = ret.Replace(@">", "\u203a"); // › (SINGLE RIGHT-POINTING ANGLE QUOTATION MARK)
        ret = ret.Replace(@"<", "\u2039"); // ‹ (SINGLE LEFT-POINTING ANGLE QUOTATION MARK)
        ret = ret.Replace(@"?", "\uff1f"); // ？ (FULL WIDTH QUESTION MARK)
        ret = ret.Replace(@"...", "\u2026"); // … (HORIZONTAL ELLIPSIS)
        if (ret.StartsWith(".", StringComparison.Ordinal)) ret = "․" + ret.Substring(1, ret.Length - 1);
        if (ret.EndsWith(".", StringComparison.Ordinal)) // U+002E
            ret = ret.Substring(0, ret.Length - 1) + "․"; // U+2024
        return ret.Trim();
    }

    public static string PadZeroes(this int num, int total)
    {
        int zeroPadding = total.ToString().Length;
        return num.ToString().PadLeft(zeroPadding, '0');
    }
}
