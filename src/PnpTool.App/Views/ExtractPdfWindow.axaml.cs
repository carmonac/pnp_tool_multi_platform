using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PnpTool.App.ViewModels;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.Views;

/// <summary>
/// The tool that takes images out of PDFs. It closes with the images to bring to the panel,
/// with an empty list when they are not wanted there, or with nothing when it is just
/// closed. Original: <c>WindowExtraer</c>.
/// </summary>
public partial class ExtractPdfWindow : Window
{
    public ExtractPdfWindow()
        : this(new ExtractPdfViewModel())
    {
    }

    public ExtractPdfWindow(ExtractPdfViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = StringResources.Get("Form_Extraer");
        ImportBox.Content = StringResources.Get("Form_Extraer_CheckBox_importar");
        WholePagesBox.Content = StringResources.Get("Form_Extraer_CheckBox_PaginaCompleta");
        ClearBox.Content = StringResources.Get("Form_Extraer_CheckBox_BorrarCarpetaDestino");
        InvertBox.Content = StringResources.Get("Form_Extraer_CheckBox_InvertirColores");
        SmallBox.Content = ExtractPdfViewModel.SmallText;
        ToolTip.SetTip(AddButton, StringResources.Get("Form_UnirPDF_Boton_Anadir"));
        ToolTip.SetTip(RemoveButton, StringResources.Get("Form_UnirPDF_Boton_quitar"));
        ToolTip.SetTip(ExtractButton, StringResources.Get("Form_Extraer"));
        model.Notice += OnNotice;
        Closed += (_, _) => model.Notice -= OnNotice;
        FilesBox.AddHandler(DragDrop.DropEvent, (_, e) => model.Add(PdfToolWindows.Dropped(e)));
    }

    public ExtractPdfViewModel Model { get; }

    /// <summary>Asks for the folder for the images; the test of the window replaces it.</summary>
    public Func<Task<string?>>? AskFolder { get; set; }

    /// <summary>Asks whether to use a folder inside one that has files; the test of the window replaces it.</summary>
    public Func<string, Task<bool?>>? Ask { get; set; }

    private readonly List<string> _said = [];

    private void OnNotice(string message) => _said.Add(message);

    /// <summary>Asks for a folder, extracts and closes with the images. Original: <c>Boton_Extraer_Click</c> and <c>Trabajador_Fin</c>.</summary>
    public async Task ExtractAsync()
    {
        if (Model.Files.Count == 0)
        {
            await new MessageWindow(StringResources.Get("faltan_datos")).ShowDialog(this);
            return;
        }

        string? folder = AskFolder != null ? await AskFolder() : await DeckWindows.PickFolderAsync(this);
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        if (ExtractPdfViewModel.HasFiles(folder))
        {
            string question = StringResources.Get("Directorio_Tiene_Archivos");
            bool? inside = Ask != null ? await Ask(question) : await new ConfirmWindow(question, withCancel: false).ShowDialog<bool?>(this);
            if (inside == true)
            {
                folder = Path.Combine(folder, ExtractPdfViewModel.Subfolder);
            }
        }

        _said.Clear();
        List<string>? written = await Model.ExtractAsync(folder);
        foreach (string message in _said.ToList())
        {
            if (Ask == null)
            {
                await new MessageWindow(message).ShowDialog(this);
            }
        }

        if (written != null)
        {
            Close(Model.Import ? written : new List<string>());
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Delete && FilesBox.IsKeyboardFocusWithin)
        {
            Model.RemoveSelected();
            e.Handled = true;
        }
    }

    private async void OnAdd(object? sender, RoutedEventArgs e) => Model.Add(await PdfToolWindows.PickPdfsAsync(this, multiple: true));

    private void OnRemove(object? sender, RoutedEventArgs e) => Model.RemoveSelected();

    private async void OnExtract(object? sender, RoutedEventArgs e) => await ExtractAsync();
}
