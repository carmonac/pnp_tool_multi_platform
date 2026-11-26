using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PnpTool.App.Services;
using PnpTool.App.ViewModels;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.Views;

/// <summary>
/// The tool that changes many images at once. Original: <c>WindowEditarimagenes</c>.
/// </summary>
public partial class EditImagesWindow : Window
{
    public EditImagesWindow()
        : this(new EditImagesViewModel())
    {
    }

    public EditImagesWindow(EditImagesViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = StringResources.Get("ShellMenuBarItem_Herramientas_ModificarImagenes");
        ToolTip.SetTip(AddButton, StringResources.Get("Form_CortarImagenes_Boton_AnadirImagen"));
        ToolTip.SetTip(FolderButton, StringResources.Get("Form_CortarImagenes_Boton_SeleccionarCarpeta"));
        ToolTip.SetTip(RemoveButton, StringResources.Get("Form_CortarImagenes_Boton_QuitarListbox"));
        ToolTip.SetTip(ProcessButton, StringResources.Get("ShellMenuBarItem_Herramientas_ModificarImagenes"));
        ToolTip.SetTip(ScaleXBox, StringResources.Get("Ancho") + " %");
        ToolTip.SetTip(ScaleYBox, StringResources.Get("Alto") + " %");
        ReplaceBox.Content = StringResources.Get("Form_ValidarImagen_CheckBox_Reemplazar");
        BackgroundButton.Load(model.Background);
        BackgroundButton.ColorChanged += _ => model.Background = BackgroundButton.Color;
        FilterBox.SelectedFilter = model.Filter;
        model.Notice += message => _ = new MessageWindow(message).ShowDialog(this);
        ImagesBox.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    public EditImagesViewModel Model { get; }

    /// <summary>Asks for the folder for the new images; the test of the window replaces it.</summary>
    public Func<Task<string?>>? AskFolder { get; set; }

    /// <summary>Asks for a folder, changes the images and shows the folder. Original: <c>Boton_Procesar_Click</c>.</summary>
    public async Task ProcessAsync()
    {
        if (Model.Images.Count == 0)
        {
            await new MessageWindow(StringResources.Get("faltan_datos")).ShowDialog(this);
            return;
        }

        string? folder = AskFolder != null ? await AskFolder() : await DeckWindows.PickFolderAsync(this);
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        Model.Filter = FilterBox.SelectedFilter;
        await Model.ProcessAsync(folder);
        Platform.Services.OpenFolder(folder);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Delete && ImagesBox.IsKeyboardFocusWithin)
        {
            Model.RemoveSelected();
            e.Handled = true;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e) =>
        Model.Add((e.DataTransfer.TryGetFiles() ?? []).Select(file => file.TryGetLocalPath()).OfType<string>());

    private async void OnAdd(object? sender, RoutedEventArgs e) => Model.Add(await ImageFiles.PickAsync(this, multiple: true));

    private async void OnAddFolder(object? sender, RoutedEventArgs e)
    {
        string? folder = await DeckWindows.PickFolderAsync(this);
        if (folder != null)
        {
            Model.Add([folder]);
        }
    }

    private void OnRemove(object? sender, RoutedEventArgs e) => Model.RemoveSelected();

    private async void OnProcess(object? sender, RoutedEventArgs e) => await ProcessAsync();
}
