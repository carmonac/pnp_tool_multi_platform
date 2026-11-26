using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Projects;

namespace PnpTool.App.ViewModels;

/// <summary>
/// Packs the project with its images into a folder, and optionally a zip, to take it to
/// another computer. Original: <c>WindowComprimir</c> with <c>ModeloComprimir</c>.
/// </summary>
public sealed partial class PackProjectViewModel : ViewModelBase
{
    private readonly Project _project;

    public PackProjectViewModel()
        : this(new Project(), string.Empty)
    {
    }

    /// <param name="project">A copy of the project; it is changed by the packing.</param>
    /// <param name="openFile">The file the project is saved in, whose name is offered for the pack.</param>
    public PackProjectViewModel(Project project, string openFile)
    {
        _project = project;
        string name = Path.GetFileNameWithoutExtension(openFile);
        _name = string.IsNullOrEmpty(name) ? "Project" : name;
    }

    /// <summary>Where the folder of the pack is made.</summary>
    [ObservableProperty]
    private string _folder = string.Empty;

    /// <summary>The name of the folder, of the project file and of the zip.</summary>
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private bool _createZip;

    /// <summary>With a zip, whether only the zip is left.</summary>
    [ObservableProperty]
    private bool _deleteFiles;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Raised to tell the user something.</summary>
    public event Action<string>? Notice;

    /// <summary>Packs the project. Gives what was made, or null when something is missing or fails. Original: <c>Boton_Start_Click</c>.</summary>
    public async Task<ProjectPacker.Packed?> PackAsync()
    {
        string name = Path.GetFileNameWithoutExtension(Name.Trim());
        if (string.IsNullOrWhiteSpace(Folder) || name.Length == 0)
        {
            Notice?.Invoke(StringResources.Get("faltan_datos"));
            return null;
        }

        IsBusy = true;
        try
        {
            string folder = Folder;
            bool zip = CreateZip;
            bool delete = DeleteFiles;
            return await Task.Run(() => ProjectPacker.Pack(_project, folder, name, zip, zip && delete, share => Progress = share));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Notice?.Invoke(exception.Message);
            return null;
        }
        finally
        {
            IsBusy = false;
            Progress = 0.0;
        }
    }
}
