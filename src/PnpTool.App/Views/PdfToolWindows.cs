using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.Views;

/// <summary>File dialogs shared by the windows that work on PDFs.</summary>
internal static class PdfToolWindows
{
    private static FilePickerFileType PdfFiles => new("PDF") { Patterns = ["*.pdf"] };

    public static async Task<IReadOnlyList<string>> PickPdfsAsync(Window owner, bool multiple)
    {
        IReadOnlyList<IStorageFile> files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = multiple,
            SuggestedStartLocation = await owner.StorageProvider.TryGetFolderFromPathAsync(AppSettings.LastFolder),
            FileTypeFilter = [PdfFiles],
        });
        return [.. files.Select(file => file.TryGetLocalPath()).OfType<string>()];
    }

    public static async Task<string?> PickPdfToSaveAsync(Window owner, string name)
    {
        IStorageFile? file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedStartLocation = await owner.StorageProvider.TryGetFolderFromPathAsync(AppSettings.LastFolder),
            SuggestedFileName = name,
            DefaultExtension = "pdf",
            FileTypeChoices = [PdfFiles],
        });
        return file?.TryGetLocalPath();
    }

    public static IEnumerable<string> Dropped(Avalonia.Input.DragEventArgs e) =>
        (e.DataTransfer.TryGetFiles() ?? []).Select(file => file.TryGetLocalPath()).OfType<string>();
}
