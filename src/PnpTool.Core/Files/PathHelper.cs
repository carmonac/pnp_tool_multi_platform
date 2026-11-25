using System.Text;

namespace PnpTool.Core.Files;

/// <summary>
/// Path handling for files shared with the Windows version, which stores relative
/// paths with backslashes.
/// </summary>
public static class PathHelper
{
    private const char WindowsSeparator = '\\';

    /// <summary>
    /// Turns a path read from a file into one usable on this system: backslashes become
    /// the local separator. Windows drive paths ("C:\…") are returned unchanged.
    /// </summary>
    public static string FromFile(string? path)
    {
        if (string.IsNullOrEmpty(path) || Path.DirectorySeparatorChar == WindowsSeparator)
        {
            return path ?? string.Empty;
        }

        return IsWindowsDrivePath(path) ? path : path.Replace(WindowsSeparator, Path.DirectorySeparatorChar);
    }

    /// <summary>Turns a relative path into the form written to files: always backslashes.</summary>
    public static string ToFile(string? path)
    {
        if (string.IsNullOrEmpty(path) || Path.DirectorySeparatorChar == WindowsSeparator)
        {
            return path ?? string.Empty;
        }

        return Path.IsPathRooted(path) ? path : path.Replace(Path.DirectorySeparatorChar, WindowsSeparator);
    }

    /// <summary>True for "C:" style prefixes. Original: <c>EsUnidadHD</c>, extended to whole paths.</summary>
    public static bool IsWindowsDrivePath(string path) =>
        path.Length >= 2 && path[1] == ':' && char.IsAsciiLetter(path[0]);

    /// <summary>
    /// Resolves <paramref name="path"/> (as stored in a project) against the folder of the
    /// project file. Absolute paths are returned unchanged. Original:
    /// <c>FuncionesGenerales.DevuelveRelativePath</c>; the algorithm is kept, with rooted
    /// Unix paths treated like Windows drive paths.
    /// </summary>
    public static string ResolveRelative(string? baseFolder, string path)
    {
        if (baseFolder == null)
        {
            return string.Empty;
        }

        path = FromFile(path);
        if (IsWindowsDrivePath(path) || (Path.DirectorySeparatorChar != WindowsSeparator && Path.IsPathRooted(path)))
        {
            return path;
        }

        char separator = Path.DirectorySeparatorChar;
        string[] target = path.Split(separator);
        string[] from = baseFolder.Split(separator);
        int counter = -1;
        do
        {
            counter++;
        }
        while (counter < target.Length && target[counter] == "..");
        counter++;

        var result = new StringBuilder();
        int index = 0;
        do
        {
            if (index > 0)
            {
                result.Append(separator);
            }

            result.Append(from[index]);
            index++;
        }
        while (index <= from.Length - counter);

        if (counter <= 1 && path != ".")
        {
            result.Append(separator).Append(path);
        }
        else
        {
            for (index = counter - 1; index < target.Length; index++)
            {
                if (target[index] != ".")
                {
                    result.Append(separator).Append(target[index]);
                }
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Path of <paramref name="path"/> relative to the file <paramref name="relativeToFile"/>,
    /// in local form and always containing a separator ("./a.png" rather than "a.png").
    /// Original: <c>FuncionesGenerales.GetRelativePath</c>.
    /// </summary>
    public static string MakeRelative(string relativeToFile, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var from = new Uri(relativeToFile);
        var to = new Uri(path);
        string relative = Uri.UnescapeDataString(from.MakeRelativeUri(to).ToString())
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        if (!relative.Contains(Path.DirectorySeparatorChar))
        {
            relative = $".{Path.DirectorySeparatorChar}{relative}";
        }

        return relative;
    }

    /// <summary>
    /// The path as it is spelled on disk. Windows, and usually macOS, ignore letter case in
    /// file names; Linux does not, so a project made on Windows can name <c>Cards\Ace.PNG</c>
    /// for a file stored as <c>cards/ace.png</c>. When <paramref name="path"/> does not exist,
    /// each of its parts is looked up ignoring case; if every part is found the real path is
    /// returned, otherwise <paramref name="path"/> unchanged. The original has nothing like
    /// this because it only ran on Windows.
    /// </summary>
    public static string FindExisting(string path)
    {
        if (string.IsNullOrEmpty(path) || File.Exists(path) || Directory.Exists(path))
        {
            return path;
        }

        string? root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            return path;
        }

        string current = root;
        foreach (string part in path[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            string exact = Path.Combine(current, part);
            if (File.Exists(exact) || Directory.Exists(exact))
            {
                current = exact;
                continue;
            }

            string? match = null;
            try
            {
                match = Directory.EnumerateFileSystemEntries(current)
                    .FirstOrDefault(entry => string.Equals(Path.GetFileName(entry), part, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }

            if (match == null)
            {
                return path;
            }

            current = match;
        }

        return current;
    }
}
