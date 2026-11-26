using Avalonia.Controls;
using Avalonia.Interactivity;
using PnpTool.App.ViewModels;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Projects;

namespace PnpTool.App.Views;

/// <summary>
/// Packs the project and its images to take them elsewhere; it shows the folder made and
/// closes when done. Original: <c>WindowComprimir</c>.
/// </summary>
public partial class PackProjectWindow : Window
{
    public PackProjectWindow()
        : this(new PackProjectViewModel())
    {
    }

    public PackProjectWindow(PackProjectViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = StringResources.Get("Form_Comprimir");
        ZipBox.Content = StringResources.Get("Window_Comprimir_CheckBox_CrearZip");
        DeleteBox.Content = StringResources.Get("Window_Comprimir_CheckBox_BorrarFicheros");
        model.Notice += message => _ = new MessageWindow(message).ShowDialog(this);
    }

    public PackProjectViewModel Model { get; }

    /// <summary>Packs and, when it goes well, shows the folder and closes. Original: <c>Boton_Start_Click</c> and <c>Trabajador_Fin</c>.</summary>
    public async Task StartAsync()
    {
        ProjectPacker.Packed? packed = await Model.PackAsync();
        if (packed != null)
        {
            Platform.Services.OpenFolder(packed.Folder);
            Close(packed);
        }
    }

    private async void OnPickFolder(object? sender, RoutedEventArgs e)
    {
        string? folder = await DeckWindows.PickFolderAsync(this);
        if (folder != null)
        {
            Model.Folder = folder;
        }
    }

    private async void OnStart(object? sender, RoutedEventArgs e) => await StartAsync();
}
