using PnpTool.Core.Infrastructure;

namespace PnpTool.Core.Files;

/// <summary>
/// The last ten projects opened, newest first, kept in a text file with one path per line.
/// Original: <c>UltimosFicherosClase</c>, without the part that built ribbon menu items.
/// </summary>
public sealed class RecentFiles
{
    /// <summary>Number of entries kept, and of lines in the file.</summary>
    public const int Capacity = 10;

    private readonly string _path;
    private readonly List<string> _files = [];

    /// <param name="path">The list file; by default <see cref="AppPaths.RecentFilesFile"/>.</param>
    public RecentFiles(string? path = null) => _path = path ?? AppPaths.RecentFilesFile;

    /// <summary>The projects, newest first. Original: <c>ListadoUltimosFicheros</c> and <c>Cuantos</c>.</summary>
    public IReadOnlyList<string> Files => _files;

    /// <summary>
    /// Reads the list, leaving out projects that no longer exist. Reading stops at the first
    /// empty line. Original: <c>LeeFichero</c>.
    /// </summary>
    public void Load()
    {
        _files.Clear();
        if (!File.Exists(_path))
        {
            return;
        }

        foreach (string line in File.ReadLines(_path).Take(Capacity))
        {
            if (line.Length == 0)
            {
                break;
            }

            if (File.Exists(line))
            {
                _files.Add(line);
            }
        }
    }

    /// <summary>
    /// Records that a project was opened or saved: it moves to the top of the list, or leaves
    /// the list when the file is gone. Only .pnp files are listed; for anything else nothing
    /// happens and the result is false. Original: <c>AnadeUltimo</c>.
    /// </summary>
    public bool Add(string file)
    {
        if (Path.GetExtension(file) != AppSettings.ProjectExtension)
        {
            return false;
        }

        // The original failed here when a missing file was not in the list; it is simply not added.
        _files.Remove(file);
        if (File.Exists(file))
        {
            _files.Insert(0, file);
            if (_files.Count > Capacity)
            {
                _files.RemoveRange(Capacity, _files.Count - Capacity);
            }
        }

        Save();
        return true;
    }

    /// <summary>Removes the entry at <paramref name="index"/>. Original: <c>BorraElemento</c>.</summary>
    public void RemoveAt(int index)
    {
        if (index >= 0 && index < _files.Count)
        {
            _files.RemoveAt(index);
            Save();
        }
    }

    /// <summary>The path at <paramref name="index"/>, or empty when there is none. Original: <c>DevuelveRuta</c>.</summary>
    public string PathAt(int index) => index >= 0 && index < _files.Count ? _files[index] : string.Empty;

    /// <summary>
    /// The text shown in the menu: paths over 35 characters keep their first 10 and last 25
    /// characters around six dots. Original: part of <c>ColeccionRibbonGalleryItem</c>.
    /// </summary>
    public static string DisplayText(string path) =>
        path.Length > 35 ? path[..10] + "......" + path[^25..] : path;

    /// <summary>Always writes <see cref="Capacity"/> lines, empty ones for unused places. Original: <c>GrabarFichero</c>.</summary>
    private void Save()
    {
        using var writer = new StreamWriter(_path);
        for (int i = 0; i < Capacity; i++)
        {
            writer.WriteLine(i < _files.Count ? _files[i] : string.Empty);
        }
    }
}
