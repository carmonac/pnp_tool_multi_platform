using Avalonia.Controls;
using Avalonia.Interactivity;
using PnpTool.App.ViewModels;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.Views;

/// <summary>
/// The two changes to an existing PDF, a tab each: the size of its sheets, and the place and
/// turn of their content. Original: <c>Window_ModificarTamanoHojaPDF</c> and
/// <c>Window_ModificarAlineacionPDF</c>.
/// </summary>
public partial class ModifyPdfWindow : Window
{
    public ModifyPdfWindow()
        : this(new ModifyPdfViewModel())
    {
    }

    public ModifyPdfWindow(ModifyPdfViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = StringResources.Get("ShellMenuBarItem_Herramientas_ModificarPDF");
        SizeTab.Header = StringResources.Get("Tamano");
        AlignTab.Header = StringResources.Get("Form_Setup_Caras_CaraTrasera_GroupBox_Desplazamiento");
        model.Notice += message => _ = new MessageWindow(message).ShowDialog(this);
    }

    public ModifyPdfViewModel Model { get; }

    /// <summary>Asks before writing over a file; the test of the window replaces it.</summary>
    public Func<string, Task<bool?>>? Ask { get; set; }

    /// <summary>Asks where to save the aligned PDF; the test of the window replaces it.</summary>
    public Func<Task<string?>>? AskFile { get; set; }

    /// <summary>Writes the copy with another sheet size and opens it. Original: <c>Boton_Comenzar_Click</c>.</summary>
    public async Task ChangeSizeAsync()
    {
        if (File.Exists(Model.File) && File.Exists(Model.ResizedFile))
        {
            string question = Model.ResizedFile + " " + StringResources.Get("Fichero_Existe");
            bool? go = Ask != null ? await Ask(question) : await new ConfirmWindow(question, withCancel: false).ShowDialog<bool?>(this);
            if (go != true)
            {
                return;
            }
        }

        string? written = Model.ChangeSize();
        if (written != null)
        {
            Platform.Services.OpenFile(written);
            Close(written);
        }
    }

    /// <summary>Asks where to save, writes the aligned copy and opens it. Original: <c>Boton_Procesar</c>.</summary>
    public async Task AlignAsync()
    {
        if (!File.Exists(Model.File))
        {
            Model.Align(null);
            return;
        }

        string? destination = AskFile != null ? await AskFile() : await PdfToolWindows.PickPdfToSaveAsync(this, "modified.pdf");
        if (!string.IsNullOrEmpty(destination) && Model.Align(destination))
        {
            Platform.Services.OpenFile(destination);
        }
    }

    private async void OnPickFile(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<string> files = await PdfToolWindows.PickPdfsAsync(this, multiple: false);
        if (files.Count != 0)
        {
            Model.File = files[0];
        }
    }

    private async void OnChangeSize(object? sender, RoutedEventArgs e) => await ChangeSizeAsync();

    private async void OnAlign(object? sender, RoutedEventArgs e) => await AlignAsync();
}
