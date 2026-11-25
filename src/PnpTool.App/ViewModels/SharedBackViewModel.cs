using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.App.Services;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.App.ViewModels;

/// <summary>
/// The back image shared by every card, shown at the right of the main window when the
/// project uses one. Original: <c>Control_ImagenTrasera</c>.
/// </summary>
public sealed partial class SharedBackViewModel : ViewModelBase
{
    /// <summary>The back image. Original: <c>CaraTraseraIgual</c>.</summary>
    public CardFace Face { get; } = new() { FaceType = FaceType.Back };

    [ObservableProperty]
    private Bitmap? _image;

    /// <summary>Whether the project uses a shared back, so the control is shown. Original: <c>MuestraControl</c>.</summary>
    [ObservableProperty]
    private bool _isVisible;

    /// <summary>Whether an image has been chosen.</summary>
    public bool HasImage => Face.ImageKind == ImageKind.Normal && !string.IsNullOrEmpty(Face.OriginalPath);

    /// <summary>Raised when the image, or the way it is turned, changes. Original: <c>NotificaCambioTrasera</c>.</summary>
    public event EventHandler? Changed;

    /// <summary>The picture being made.</summary>
    public Task ImageLoaded { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// Takes an image file as the back. A file that cannot be read as an image is refused.
    /// Original: <c>ActualizaCartaTraseraIgual(string)</c>.
    /// </summary>
    public bool SetImage(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return false;
        }

        CardFace read = ImageInspector.Current.GetImageProperties(path);
        if (read.IsCorrupt)
        {
            return false;
        }

        Face.CopyFrom(read);
        Face.FaceType = FaceType.Back;
        Face.ImageKind = ImageKind.Normal;
        Refresh();
        return true;
    }

    /// <summary>Shows the back of a project that was opened. Original: <c>ActualizaCartaTraseraIgual(CaraDeCarta)</c>.</summary>
    public void Load(CardFace face)
    {
        Face.CopyFrom(face);
        Face.FaceType = FaceType.Back;
        Refresh();
    }

    /// <summary>Turns or mirrors the image. Original: <c>RotaryTransformarImagenDesdeMenu</c>.</summary>
    public void Rotate(RotationProperties change)
    {
        if (HasImage)
        {
            Face.ApplyRotation(change);
            Refresh();
        }
    }

    /// <summary>Leaves the control without image. Original: <c>Clear</c>.</summary>
    public void Clear()
    {
        Face.CopyFrom(new CardFace { FaceType = FaceType.Back });
        Refresh();
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(HasImage));
        ImageLoaded = HasImage ? LoadAsync() : Task.FromResult(Image = null);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task LoadAsync() => Image = await FaceImages.LoadAsync(Face, 512);
}
