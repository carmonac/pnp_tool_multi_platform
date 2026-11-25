using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.Services;

/// <summary>
/// The system's dialog to choose image files. Original: <c>Dialogos.DialogoSeleccionarFicherosImagenes</c>.
/// </summary>
public static class ImageFiles
{
    /// <summary>Asks for one image file or several. Gives no files when the user cancels.</summary>
    public static async Task<IReadOnlyList<string>> PickAsync(TopLevel owner, bool multiple)
    {
        IReadOnlyList<IStorageFile> files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = multiple,
            FileTypeFilter =
            [
                new FilePickerFileType(StringResources.Get("Form_Principal_Boton_AnadirImagen"))
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.tif", "*.tiff", "*.webp", "*.avif"],
                    AppleUniformTypeIdentifiers = ["public.image"],
                    MimeTypes = ["image/*"],
                },
                FilePickerFileTypes.All,
            ],
        });
        return [.. files.Select(file => file.TryGetLocalPath()).OfType<string>()];
    }
}
