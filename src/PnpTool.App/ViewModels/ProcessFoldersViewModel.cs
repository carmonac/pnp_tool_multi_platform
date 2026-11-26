using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.ViewModels;

/// <summary>
/// The tool that makes PDFs straight from folders of images, with the settings of the
/// project: one PDF of them all, or a PDF a folder. Original: <c>Window_ProcesarCarpeta</c>.
/// </summary>
public sealed partial class ProcessFoldersViewModel : ViewModelBase
{
    /// <summary>More folders than this are not taken, as in the original.</summary>
    public const int MostFolders = 99;

    public ObservableCollection<string> Folders { get; } = [];

    [ObservableProperty]
    private int _selectedIndex = -1;

    /// <summary>Whether the folders inside a folder added are added too.</summary>
    [ObservableProperty]
    private bool _includeSubfolders;

    /// <summary>Whether every folder gets a PDF of its own, named after it. Original: <c>RelacionUnoUno</c>.</summary>
    [ObservableProperty]
    private bool _onePdfPerFolder;

    /// <summary>Raised to tell the user something.</summary>
    public event Action<string>? Notice;

    /// <summary>Adds folders; what is not a folder is left out. Original: <c>Boton_Carpeta_Click</c>.</summary>
    public void Add(IEnumerable<string> paths)
    {
        foreach (string path in paths.Where(Directory.Exists))
        {
            if (!AddOne(path))
            {
                return;
            }

            if (IncludeSubfolders)
            {
                foreach (string inner in Directory.GetDirectories(path).Order(StringComparer.Ordinal))
                {
                    if (!AddOne(inner))
                    {
                        return;
                    }
                }
            }
        }
    }

    private bool AddOne(string folder)
    {
        if (Folders.Count >= MostFolders)
        {
            Notice?.Invoke("Too many folders");
            return false;
        }

        Folders.Add(folder);
        return true;
    }

    /// <summary>Original: <c>QuitarFicheros</c>.</summary>
    public void RemoveSelected()
    {
        int index = SelectedIndex;
        if (index < 0 || index >= Folders.Count)
        {
            return;
        }

        Folders.RemoveAt(index);
        SelectedIndex = Folders.Count == 0 ? -1 : Math.Max(0, index - 1);
    }

    /// <summary>Original: <c>Boton_FlechaArriba_Click</c>.</summary>
    public void MoveUp() => Move(-1);

    /// <summary>Original: <c>Boton_FlechaAbajo_Click</c>.</summary>
    public void MoveDown() => Move(1);

    private void Move(int step)
    {
        int index = SelectedIndex;
        int target = index + step;
        if (index < 0 || target < 0 || target >= Folders.Count)
        {
            return;
        }

        Folders.Move(index, target);
        SelectedIndex = target;
    }

    /// <summary>Whether there is something to process; tells the user when there is not.</summary>
    public bool Ready()
    {
        if (Folders.Count == 0)
        {
            Notice?.Invoke(StringResources.Get("faltan_datos"));
            return false;
        }

        return true;
    }
}
