using SharpCompress.Archives;

namespace PnpTool.Core.Files;

/// <summary>
/// Takes the images out of compressed files (zip, rar, 7z and the comic-book forms of them)
/// so they can be added as cards. Not in the original, which had no way to add them.
/// </summary>
/// <remarks>
/// The images go to a folder beside the compressed file and named after it, as the file
/// manager does when a compressed file is opened, because the cards of a project point at
/// files that must still be there the next day. A file already in that folder is left as it
/// is and used, so adding the same compressed file twice does not write anything again.
/// </remarks>
public static class ImageArchives
{
    private static readonly string[] Extensions = [".zip", ".rar", ".7z", ".cbz", ".cbr", ".cb7"];

    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".avif"];

    /// <summary>Whether a name inside a compressed file is that of an image, by its extension.</summary>
    public static bool HasImageName(string name) => ImageExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);

    /// <summary>The patterns of the files taken, for file dialogs.</summary>
    public static IReadOnlyList<string> Patterns { get; } = [.. Extensions.Select(extension => "*" + extension)];

    public static bool IsArchive(string? path) =>
        !string.IsNullOrEmpty(path) && Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>The folder the images of a compressed file go to.</summary>
    public static string FolderFor(string archive)
    {
        string folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(archive)) ?? string.Empty, Path.GetFileNameWithoutExtension(archive));

        // "cards.zip" beside a file called "cards" cannot use that name.
        return File.Exists(folder) ? folder + "_images" : folder;
    }

    /// <summary>
    /// Writes the images of the compressed file to <see cref="FolderFor"/>, keeping its
    /// folders, and gives their paths in the order of the compressed file. What is not an
    /// image is skipped. A compressed file that cannot be read (damaged, with password, or
    /// nowhere to write) gives the error instead, with what was taken out until then.
    /// </summary>
    /// <param name="archive">The compressed file.</param>
    /// <param name="isImage">Tells images from other files by their name; <see cref="HasImageName"/> when left out.</param>
    public static (string[] Images, AppError Error) Extract(string archive, Func<string, bool>? isImage = null)
    {
        isImage ??= HasImageName;
        var images = new List<string>();
        var error = new AppError();
        try
        {
            string folder = FolderFor(archive);
            string root = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
            using IArchive opened = ArchiveFactory.OpenArchive(archive);
            foreach (IArchiveEntry entry in opened.Entries)
            {
                string? name = entry.Key;
                if (entry.IsDirectory || string.IsNullOrEmpty(name) || !isImage(name) || Path.GetFileName(name).StartsWith('.'))
                {
                    continue;
                }

                // Names inside compressed files can use either separator, and can point
                // outside the folder ("../../x.png"): those are not written.
                string target = Path.GetFullPath(Path.Combine(folder, name.Replace('\\', '/').TrimStart('/')));
                if (!target.StartsWith(root, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!File.Exists(target))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    try
                    {
                        entry.WriteToFile(target);
                    }
                    catch
                    {
                        // Half a file is worse than none.
                        File.Delete(target);
                        throw;
                    }
                }

                images.Add(target);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            error.Kind = ErrorKind.Error;
            error.Message = Path.GetFileName(archive) + ": " + exception.Message;
        }

        return ([.. images], error);
    }
}
