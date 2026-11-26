using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PnpTool.App.ViewModels;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.App.Views;

/// <summary>
/// The editor of advanced templates: the list of templates, the paper, the cards of the one
/// chosen and a sketch of the sheet. Original: <c>Window_PlantillasAvanzadas</c>.
/// </summary>
public partial class TemplateEditorWindow : Window
{
    public TemplateEditorWindow()
        : this(new TemplateEditorViewModel())
    {
    }

    public TemplateEditorWindow(TemplateEditorViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        string location = StringResources.Get("Ubicacion");
        string size = StringResources.Get("Tamano");
        Title = StringResources.Get("Window_PlantillasAvanzadas");
        NameLabel.Text = StringResources.Get("Form_Setup_Label_Subgrupo");
        BleedLabel.Text = StringResources.Get("Form_Setup_LabelSangrado");
        LocationXLabel.Text = location + " X:";
        LocationYLabel.Text = location + " Y:";
        SizeXLabel.Text = size + " X:";
        SizeYLabel.Text = size + " Y:";
        RotationLabel.Text = StringResources.Get("Rotacion");
        ToolTip.SetTip(SaveButton, StringResources.Get("ShellMenuItem_File_Guardar"));
        ToolTip.SetTip(DeleteButton, StringResources.Get("Form_Principal_Boton_Eliminar"));
        ToolTip.SetTip(CloneButton, StringResources.Get("Clonar"));
        ToolTip.SetTip(ImportButton, StringResources.Get("Importar"));
        ToolTip.SetTip(ExportButton, StringResources.Get("Exportar"));
        ToolTip.SetTip(AddButton, StringResources.Get("Añadir"));
        ToolTip.SetTip(RemoveButton, StringResources.Get("Form_UnirPDF_Boton_quitar"));
        ToolTip.SetTip(PortraitButton, StringResources.Get("Form_Setup_Label_PapelVertical"));
        ToolTip.SetTip(LandscapeButton, StringResources.Get("Form_Setup_Label_PapelHorizontal"));
        model.Notice += message => _ = new MessageWindow(message).ShowDialog(this);
        model.PreviewChanged += (_, _) => Draw();
        Draw();
    }

    public TemplateEditorViewModel Model { get; }

    /// <summary>Asks before deleting a template; the test of the window replaces it.</summary>
    public Func<string, Task<bool?>>? Ask { get; set; }

    /// <summary>Deletes the template chosen once the user agrees. Original: <c>Boton_Borrar_Click</c>.</summary>
    public async Task DeleteAsync()
    {
        if (Model.IsNew)
        {
            return;
        }

        string question = StringResources.Get("Confirmar_Borrado");
        bool? answer = Ask != null ? await Ask(question) : await new ConfirmWindow(question, withCancel: false).ShowDialog<bool?>(this);
        if (answer == true)
        {
            Model.Delete();
        }
    }

    private void Draw()
    {
        UnitPoint paper = Model.Paper.OrientedSize;
        Preview.Show(
            paper.X.Millimeter,
            paper.Y.Millimeter,
            [.. Model.Slots.Select(slot => new TemplatePreviewCard(
                new Rect(slot.Card.Location.X.Millimeter, slot.Card.Location.Y.Millimeter, slot.Card.Size.X.Millimeter, slot.Card.Size.Y.Millimeter),
                slot.Color,
                slot == Model.SelectedSlot,
                Model.Fits(slot)))]);
    }

    /// <summary>The delete key removes the card chosen in the list. Original: <c>HandleEsc</c>.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Delete && SlotsBox.IsKeyboardFocusWithin)
        {
            Model.RemoveSelected();
            e.Handled = true;
        }
    }

    private void OnPortrait(object? sender, RoutedEventArgs e) => Model.Paper.Landscape = false;

    private void OnLandscape(object? sender, RoutedEventArgs e) => Model.Paper.Landscape = true;

    private void OnAdd(object? sender, RoutedEventArgs e) => Model.Add();

    private void OnRemove(object? sender, RoutedEventArgs e) => Model.RemoveSelected();

    private void OnUp(object? sender, RoutedEventArgs e) => Model.MoveUp();

    private void OnDown(object? sender, RoutedEventArgs e) => Model.MoveDown();

    private void OnSave(object? sender, RoutedEventArgs e) => Model.Save();

    private void OnClone(object? sender, RoutedEventArgs e) => Model.Clone();

    private async void OnDelete(object? sender, RoutedEventArgs e) => await DeleteAsync();

    private static FilePickerFileType TemplateFiles => new("PNP Tool advanced template") { Patterns = ["*" + AppSettings.AdvancedTemplateExtension] };

    private async void OnImport(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(AppSettings.LastFolder),
            FileTypeFilter = [TemplateFiles],
        });
        string? path = files.Count == 0 ? null : files[0].TryGetLocalPath();
        if (path != null)
        {
            Model.Import(path);
        }
    }

    private async void OnExport(object? sender, RoutedEventArgs e)
    {
        string? name = Model.ExportName;
        if (name == null)
        {
            return;
        }

        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(AppSettings.LastFolder),
            SuggestedFileName = name,
            DefaultExtension = AppSettings.AdvancedTemplateExtension.TrimStart('.'),
            FileTypeChoices = [TemplateFiles],
        });
        string? path = file?.TryGetLocalPath();
        if (path != null)
        {
            Model.Export(path);
        }
    }
}
