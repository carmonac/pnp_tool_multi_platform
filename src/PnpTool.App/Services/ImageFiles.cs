using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.Services;

/// <summary>
/// The system's dialog to choose image files. Original: <c>Dialogos.DialogoSeleccionarFicherosImagenes</c>.
/// </summary>
public static class ImageFiles
{
    private static readonly string[] ImagePatterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.tif", "*.tiff", "*.webp", "*.avif"];

    /// <summary>
    /// Asks for one image file or several, and for compressed files of images too when
    /// <paramref name="archives"/> says so. Gives no files when the user cancels.
    /// </summary>
    public static async Task<IReadOnlyList<string>> PickAsync(TopLevel owner, bool multiple, bool archives = false)
    {
        IReadOnlyList<IStorageFile> files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = multiple,
            FileTypeFilter =
            [
                new FilePickerFileType(StringResources.Get("Form_Principal_Boton_AnadirImagen"))
                {
                    Patterns = [.. ImagePatterns, .. archives ? ImageArchives.Patterns : []],
                    AppleUniformTypeIdentifiers = archives ? ["public.image", "public.archive"] : ["public.image"],
                    MimeTypes = archives
                        ? ["image/*", "application/zip", "application/vnd.rar", "application/x-rar", "application/x-7z-compressed", "application/vnd.comicbook+zip", "application/vnd.comicbook-rar"]
                        : ["image/*"],
                },
                FilePickerFileTypes.All,
            ],
        });
        return [.. files.Select(file => file.TryGetLocalPath()).OfType<string>()];
    }
}
