using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using PnpTool.App.Services;
using PnpTool.Core.Infrastructure;
using PnpTool.Imaging;

namespace PnpTool.App.Views;

/// <summary>What the two windows that create decks share: pictures and file dialogs.</summary>
internal static class DeckWindows
{
    private static FilePickerFileType DeckFiles => new("PNP Tool deck") { Patterns = ["*" + AppSettings.CustomDeckExtension] };

    /// <summary>The picture of a file for the screen, or nothing when there is no such file or it is not an image.</summary>
    public static Bitmap? LoadPicture(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return FaceImages.ToBitmap(DisplayImages.Load(path, 700));
        }
        catch (Exception exception) when (exception is ImageMagick.MagickException or IOException or UnauthorizedAccessException)
        {
            Log.Write("Picture not shown: " + exception.Message);
            return null;
        }
    }

    public static async Task<string?> PickFolderAsync(Window owner)
    {
        IReadOnlyList<IStorageFolder> folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            SuggestedStartLocation = await owner.StorageProvider.TryGetFolderFromPathAsync(AppSettings.LastFolder),
        });
        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    public static async Task<string?> PickDeckToOpenAsync(Window owner)
    {
        IReadOnlyList<IStorageFile> files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            SuggestedStartLocation = await owner.StorageProvider.TryGetFolderFromPathAsync(AppSettings.LastFolder),
            FileTypeFilter = [DeckFiles],
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    public static async Task<string?> PickDeckToSaveAsync(Window owner, string? name)
    {
        IStorageFile? file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = StringResources.Get("salvar_como"),
            SuggestedStartLocation = await owner.StorageProvider.TryGetFolderFromPathAsync(AppSettings.LastFolder),
            SuggestedFileName = name ?? string.Empty,
            DefaultExtension = AppSettings.CustomDeckExtension.TrimStart('.'),
            FileTypeChoices = [DeckFiles],
        });
        return file?.TryGetLocalPath();
    }

    public static async Task<string?> PickImageAsync(Window owner, string? startFolder = null)
    {
        IReadOnlyList<IStorageFile> files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            SuggestedStartLocation = await owner.StorageProvider.TryGetFolderFromPathAsync(startFolder ?? AppSettings.LastFolder),
            FileTypeFilter = [FilePickerFileTypes.ImageAll],
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }
}
