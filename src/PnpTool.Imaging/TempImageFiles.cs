using System.Security.Cryptography;
using System.Text;
using PnpTool.Core.Infrastructure;

namespace PnpTool.Imaging;

/// <summary>Names for the temporary copies the program makes of images it has to change.</summary>
public static class TempImageFiles
{
    /// <summary>
    /// Longest file name most file systems of macOS and Linux accept is 255 bytes; names are
    /// kept well under it.
    /// </summary>
    private const int LongestStem = 150;

    /// <summary>
    /// A file name in the temporary folder for a changed copy of <paramref name="path"/>, built
    /// from the whole path so that images with the same name in different folders do not
    /// collide: <c>/home/ana/cards/ace.png</c> gives <c>_home_ana_cards_ace_0.png</c>.
    /// The second value says whether the copy has to be made: it is false when a copy with
    /// that name was written less than 50 seconds ago, which is then reused.
    /// Original: <c>DevuelveNombreUnico</c>.
    /// </summary>
    /// <remarks>
    /// Windows limits the whole path; macOS and Linux limit each name to 255 bytes, which a
    /// deep folder would exceed here. A name that would be too long keeps its end and gets a
    /// hash of the full path in front. The original has no such case.
    /// </remarks>
    public static (string Path, bool MustCreate) UniqueName(string path, string format)
    {
        string[] parts = path.Split(Path.DirectorySeparatorChar);
        var stem = new StringBuilder((parts[0].Length > 0 ? parts[0][0].ToString() : string.Empty) + "_");
        for (int i = 1; i < parts.Length - 1; i++)
        {
            stem.Append(parts[i] + "_");
        }

        stem.Append(Path.GetFileNameWithoutExtension(path) + "_");
        string name = Shorten(stem.ToString());

        int index = 0;
        bool mustCreate = true;
        string candidate;
        bool found;
        do
        {
            candidate = Path.Combine(AppSettings.TempFolder, name + index + "." + format);
            if (File.Exists(candidate))
            {
                found = true;
                if (DateTime.UtcNow.Subtract(new FileInfo(candidate).LastWriteTimeUtc).TotalSeconds < 50.0)
                {
                    mustCreate = false;
                }
            }
            else
            {
                found = false;
            }

            index++;
        }
        while (found & mustCreate);

        return (candidate, mustCreate);
    }

    private static string Shorten(string stem)
    {
        if (Encoding.UTF8.GetByteCount(stem) <= LongestStem)
        {
            return stem;
        }

        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stem)))[..16];
        string tail = stem;
        while (Encoding.UTF8.GetByteCount(tail) > LongestStem - hash.Length - 1)
        {
            tail = tail[1..];
        }

        return hash + "_" + tail;
    }
}
