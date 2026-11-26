using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PnpTool.App.ViewModels;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.Views;

/// <summary>
/// The list of folders to make PDFs from. It closes with true to go on, and with nothing
/// when it is just closed. Original: <c>Window_ProcesarCarpeta</c>.
/// </summary>
public partial class ProcessFoldersWindow : Window
{
    public ProcessFoldersWindow()
        : this(new ProcessFoldersViewModel())
    {
    }

    public ProcessFoldersWindow(ProcessFoldersViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = StringResources.Get("ShellMenuBarItem_Herramientas_ProcesarCarpeta");
        SubfoldersBox.Content = StringResources.Get("Form_ProcesarCarpetas_CheckBox_Subcarpetas");
        ToolTip.SetTip(AddButton, StringResources.Get("Form_UnirPDF_Boton_Anadir"));
        ToolTip.SetTip(RemoveButton, StringResources.Get("Form_UnirPDF_Boton_quitar"));
        ToolTip.SetTip(ProcessButton, StringResources.Get("ShellMenuBarItem_Herramientas_ProcesarCarpeta"));
        model.Notice += message => _ = new MessageWindow(message).ShowDialog(this);
        FoldersBox.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    public ProcessFoldersViewModel Model { get; }

    /// <summary>Closes to go on when there are folders, as the process button does.</summary>
    public void Accept()
    {
        if (Model.Ready())
        {
            Close(true);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }

        if (e.Key == Key.Delete && FoldersBox.IsKeyboardFocusWithin)
        {
            Model.RemoveSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close(null);
        }
    }

    private void OnDrop(object? sender, DragEventArgs e) =>
        Model.Add((e.DataTransfer.TryGetFiles() ?? []).Select(file => file.TryGetLocalPath()).OfType<string>());

    private async void OnAdd(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = true,
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(AppSettings.LastFolder),
        });
        Model.Add(folders.Select(folder => folder.TryGetLocalPath()).OfType<string>());
    }

    private void OnRemove(object? sender, RoutedEventArgs e) => Model.RemoveSelected();

    private void OnUp(object? sender, RoutedEventArgs e) => Model.MoveUp();

    private void OnDown(object? sender, RoutedEventArgs e) => Model.MoveDown();

    private void OnProcess(object? sender, RoutedEventArgs e) => Accept();
}
