using System.IO.Compression;
using PnpTool.Core.Cards;
using PnpTool.Core.Files;

namespace PnpTool.Core.Projects;

/// <summary>
/// Packs a project to take it elsewhere: a folder with the project file and, in "images", a
/// copy of every image it uses, and optionally a zip of that folder.
/// Original: <c>ModeloComprimir</c>.
/// </summary>
public static class ProjectPacker
{
    /// <summary>Where the pieces of a packed project are.</summary>
    public sealed record Packed(string Folder, string ProjectFile, string ImagesFolder, string? ZipFile);

    /// <summary>
    /// Packs the project into <c>&lt;folder&gt;/&lt;name&gt;</c>. The project given is changed: its
    /// faces point to the copies.
    /// </summary>
    /// <param name="createZip">Also write <c>&lt;name&gt;.zip</c> inside the folder of the pack.</param>
    /// <param name="deleteFiles">
    /// With a zip, delete the images and the project file once they are inside it, so that
    /// only the zip is left. Without a zip nothing is deleted.
    /// </param>
    /// <param name="progress">Told the share of the images copied, from 0 to 100.</param>
    /// <remarks>
    /// Differences from the original: its window passed "zip" and "delete" in each other's
    /// place, so asking only for a zip deleted the files and made no zip; and it deleted when
    /// asked even with no zip to keep them. Two images with the same file name in different
    /// folders no longer overwrite each other: the second copy gets a number.
    /// </remarks>
    public static Packed Pack(Project project, string folder, string name, bool createZip, bool deleteFiles, Action<int>? progress = null)
    {
        string root = Path.Combine(folder, name);
        string images = Path.Combine(root, "images");
        Directory.CreateDirectory(images);
        string projectFile = Path.Combine(root, name + ".pnp");
        var copies = new Dictionary<string, string>(StringComparer.Ordinal);
        Card[] cards = project.Deck.Cards;
        for (int index = 0; index < cards.Length; index++)
        {
            Move(cards[index].Front, images, copies);
            Move(cards[index].Back, images, copies);
            progress?.Invoke(index * 100 / cards.Length);
        }

        if (project.Settings.PrintLayout.BackImage == BackImageMode.SameBack)
        {
            Move(project.Settings.SharedBack, images, copies);
        }

        project.Settings.Other.ImagePathMode = ImagePathMode.Relative;
        ProjectFileStore.SaveProject(projectFile, project);
        string? zip = null;
        if (createZip)
        {
            string temporary = Path.Combine(Path.GetTempPath(), "pnptool-pack-" + Guid.NewGuid().ToString("N") + ".zip");
            ZipFile.CreateFromDirectory(root, temporary);
            zip = Path.Combine(root, name + ".zip");
            File.Move(temporary, zip, overwrite: true);
            if (deleteFiles)
            {
                Directory.Delete(images, recursive: true);
                File.Delete(projectFile);
            }
        }

        progress?.Invoke(100);
        return new Packed(root, projectFile, images, zip);
    }

    /// <summary>Copies the image of a face and points the face to the copy. Original: <c>MueveImagen</c>.</summary>
    private static void Move(CardFace face, string images, Dictionary<string, string> copies)
    {
        if (face.ImageKind != ImageKind.Normal || string.IsNullOrEmpty(face.OriginalPath) || !File.Exists(face.OriginalPath))
        {
            return;
        }

        string source = Path.GetFullPath(face.OriginalPath);
        if (!copies.TryGetValue(source, out string? target))
        {
            target = Path.Combine(images, Path.GetFileName(source));
            for (int number = 2; copies.ContainsValue(target); number++)
            {
                target = Path.Combine(images, Path.GetFileNameWithoutExtension(source) + "_" + number + Path.GetExtension(source));
            }

            File.Copy(source, target, overwrite: true);
            copies[source] = target;
        }

        face.OriginalPath = target;
        face.Path = string.Empty;
    }
}
