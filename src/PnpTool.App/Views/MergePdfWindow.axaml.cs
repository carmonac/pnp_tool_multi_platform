using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PnpTool.App.ViewModels;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.Views;

/// <summary>The tool that joins PDFs; it closes when they are joined. Original: <c>Window_Unir_PDF</c>.</summary>
public partial class MergePdfWindow : Window
{
    public MergePdfWindow()
        : this(new MergePdfViewModel())
    {
    }

    public MergePdfWindow(MergePdfViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = StringResources.Get("ShellMenuBarItem_Herramientas_Unir");
        ToolTip.SetTip(AddButton, StringResources.Get("Form_UnirPDF_Boton_Anadir"));
        ToolTip.SetTip(RemoveButton, StringResources.Get("Form_UnirPDF_Boton_quitar"));
        ToolTip.SetTip(UpButton, StringResources.Get("Form_UnirPDF_Boton_FlechaArriba"));
        ToolTip.SetTip(DownButton, StringResources.Get("Form_UnirPDF_Boton_FlechaAbajo"));
        InterleaveButton.Content = StringResources.Get("Form_UnirPDF_Boton_Intercalar");
        OpenButton.Content = StringResources.Get("Form_UnirPDF_Boton_AbrirPDF");
        ToolTip.SetTip(MergeButton, StringResources.Get("Form_UnirPDF_Boton_Fusionar"));
        model.Notice += message => _ = new MessageWindow(message).ShowDialog(this);
        FilesBox.AddHandler(DragDrop.DropEvent, (_, e) => model.Add(PdfToolWindows.Dropped(e)));
    }

    public MergePdfViewModel Model { get; }

    /// <summary>Asks where to save the PDF; the test of the window replaces it.</summary>
    public Func<Task<string?>>? AskFile { get; set; }

    /// <summary>Asks where to save, joins the files and closes. Original: <c>BotonFusionar_Click</c>.</summary>
    public async Task MergeAsync()
    {
        string? destination = AskFile != null ? await AskFile() : await PdfToolWindows.PickPdfToSaveAsync(this, string.Empty);
        if (await Model.MergeAsync(destination))
        {
            Close(true);
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

    private void OnUp(object? sender, RoutedEventArgs e) => Model.MoveUp();

    private void OnDown(object? sender, RoutedEventArgs e) => Model.MoveDown();

    private async void OnMerge(object? sender, RoutedEventArgs e) => await MergeAsync();
}
