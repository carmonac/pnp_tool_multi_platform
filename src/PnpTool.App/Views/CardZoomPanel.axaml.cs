using Avalonia.Controls;
using Avalonia.Media.Imaging;
using PnpTool.App.Services;
using PnpTool.Controls;
using PnpTool.Core.Cards;
using PnpTool.Core.Primitives;

namespace PnpTool.App.Views;

/// <summary>
/// The larger view of a face shown while the pointer rests on it: the image with its path,
/// or the colour of a blank face. Original: <c>PanelCartaZoom_WPF</c>.
/// </summary>
public partial class CardZoomPanel : UserControl
{
    /// <summary>Longest side of the picture, in pixels.</summary>
    public const int PictureSize = 800;

    private CancellationTokenSource _loading = new();

    public CardZoomPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows a face. The window is 400 by 300 for a landscape image, 300 by 400 for a
    /// portrait one and 350 square otherwise. Original: <c>Muestra</c>.
    /// </summary>
    public async Task ShowAsync(CardFace face)
    {
        _loading.Cancel();
        _loading = new CancellationTokenSource();
        CancellationToken cancellation = _loading.Token;
        Picture.Source = null;
        if (face.ImageKind != ImageKind.Normal)
        {
            ArgbColor color = ArgbColor.Parse(face.Path);
            PathLabel.Text = "RGB#" + face.Path;
            ColorFill.Background = color.ToBrush();
            ColorFill.IsVisible = true;
            Width = Height = 350;
            return;
        }

        ColorFill.IsVisible = false;
        PathLabel.Text = ShortPath(face.OriginalPath);
        Width = Height = 350;
        Bitmap? picture = await FaceImages.LoadAsync(face, PictureSize, cancellation);
        if (cancellation.IsCancellationRequested || picture == null)
        {
            return;
        }

        (Width, Height) = picture.PixelSize.Width > picture.PixelSize.Height
            ? (400, 300)
            : picture.PixelSize.Width < picture.PixelSize.Height ? (300, 400) : (350, 350);
        Picture.Source = picture;
    }

    /// <summary>Stops showing the face. Original: the clean-up in <c>PanelesImagenes_MouseLeave</c>.</summary>
    public void Clear()
    {
        _loading.Cancel();
        Picture.Source = null;
    }

    /// <summary>A path over 70 characters keeps its first and last 25. Original: <c>AjustaTextoRuta</c>.</summary>
    public static string ShortPath(string path) =>
        path.Length > 70 ? path[..25] + "..." + path[^25..] : path;
}
