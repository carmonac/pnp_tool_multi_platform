using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagick;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Imaging;

namespace PnpTool.App.ViewModels;

/// <summary>
/// The tool that cuts images into a table of pieces: a sheet of cards into its cards, or a
/// double page into two. Original: <c>Window_CortarImagenes</c> with <c>ControlCanvas</c>.
/// </summary>
public sealed partial class CutImagesViewModel : ViewModelBase
{
    public CutImagesViewModel()
    {
    }

    /// <param name="images">Images to start with, such as the double pages of a book.</param>
    public CutImagesViewModel(IEnumerable<string> images)
    {
        Add(images);
    }

    public ObservableCollection<string> Images { get; } = [];

    [ObservableProperty]
    private string? _selectedImage;

    [ObservableProperty]
    private int _columns = 1;

    [ObservableProperty]
    private int _rows = 2;

    [ObservableProperty]
    private int _borderLeft;

    [ObservableProperty]
    private int _borderRight;

    [ObservableProperty]
    private int _borderTop;

    [ObservableProperty]
    private int _borderBottom;

    [ObservableProperty]
    private int _spacingX;

    [ObservableProperty]
    private int _spacingY;

    [ObservableProperty]
    private bool _deleteSmallPieces;

    public IReadOnlyList<string> Extensions { get; } = ImageCutter.Extensions;

    [ObservableProperty]
    private string _extension = ImageCutter.Extensions[0];

    /// <summary>The thickness of the lines drawn over the image, from 1 to 10.</summary>
    [ObservableProperty]
    private int _lineThickness = 1;

    /// <summary>The colour of the lines drawn over the image.</summary>
    [ObservableProperty]
    private ArgbColor _lineColor = new(255, 255, 105, 180);

    [ObservableProperty]
    private double _progress;

    /// <summary>The size in pixels of the first image, which the borders are measured against.</summary>
    public (int Width, int Height) ImageSize { get; private set; }

    public string MaximumText => ImageSize.Width == 0 ? string.Empty : "Max X:" + ImageSize.Width + "px   Max Y:" + ImageSize.Height + "px";

    /// <summary>The label of the box that deletes nearly empty pieces.</summary>
    public static string SmallPiecesText => "<" + AppSettings.SmallFileSizeKb + "Kb";

    /// <summary>Raised to tell the user something.</summary>
    public event Action<string>? Notice;

    /// <summary>Raised when what is drawn over the image has changed.</summary>
    public event EventHandler? PreviewChanged;

    /// <summary>The settings as the cutter takes them.</summary>
    public ImageCutter Cutter => new()
    {
        Columns = Columns,
        Rows = Rows,
        BorderLeft = BorderLeft,
        BorderRight = BorderRight,
        BorderTop = BorderTop,
        BorderBottom = BorderBottom,
        SpacingX = SpacingX,
        SpacingY = SpacingY,
        DeleteSmallPieces = DeleteSmallPieces,
        Extension = Extension,
    };

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is not (nameof(Progress) or nameof(DeleteSmallPieces) or nameof(Extension)))
        {
            PreviewChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Two borders cannot take more than the image: the one typed gives way. Original: CompruebaValor.
    partial void OnBorderLeftChanged(int value) => BorderLeft = Fit(value, BorderRight, ImageSize.Width);

    partial void OnBorderRightChanged(int value) => BorderRight = Fit(value, BorderLeft, ImageSize.Width);

    partial void OnBorderTopChanged(int value) => BorderTop = Fit(value, BorderBottom, ImageSize.Height);

    partial void OnBorderBottomChanged(int value) => BorderBottom = Fit(value, BorderTop, ImageSize.Height);

    private static int Fit(int value, int other, int size) => Math.Max(0, value + other <= size ? value : size - other);

    /// <summary>
    /// Adds images, and the images inside folders. A file that cannot be read is left out,
    /// and one of another size than the first is added with a warning.
    /// Original: <c>AnadirImagen</c>.
    /// </summary>
    public void Add(IEnumerable<string> paths)
    {
        foreach (string path in Panel.ImagePanelViewModel.ImagesAmong(paths))
        {
            if (ImageFormats.CheckReadable(path).HasError)
            {
                continue;
            }

            var info = new MagickImageInfo(path);
            (int width, int height) = ((int)info.Width, (int)info.Height);
            if (Images.Count == 0)
            {
                ImageSize = (width, height);
                OnPropertyChanged(nameof(ImageSize));
                OnPropertyChanged(nameof(MaximumText));
                AppSettings.LastFolder = Path.GetDirectoryName(path) ?? AppSettings.LastFolder;
            }
            else if ((width, height) != ImageSize)
            {
                Notice?.Invoke(StringResources.Get("Imagen_no_mismo_tamano") + Environment.NewLine
                    + "First:" + ImageSize.Width + "x" + ImageSize.Height + " - This:" + width + "x" + height);
            }

            Images.Add(path);
            SelectedImage ??= path;
        }
    }

    /// <summary>Original: <c>QuitarFicheros</c>.</summary>
    public void RemoveSelected()
    {
        if (SelectedImage == null)
        {
            return;
        }

        int index = Images.IndexOf(SelectedImage);
        Images.RemoveAt(index);
        SelectedImage = Images.Count == 0 ? null : Images[Math.Max(0, index - 1)];
    }

    /// <summary>Original: <c>Boton_Vaciar_Click</c>.</summary>
    public void Clear()
    {
        Images.Clear();
        SelectedImage = null;
    }

    /// <summary>Original: <c>Form_CortarImagenes_Boton_Reset_Click</c>.</summary>
    public void ResetBorders() => (BorderLeft, BorderRight, BorderTop, BorderBottom) = (0, 0, 0, 0);

    /// <summary>Cuts the images into the folder and tells when it is done. Original: <c>Boton_Cortar_Click</c>.</summary>
    public async Task<List<string>> CutAsync(string folder)
    {
        if (Images.Count == 0)
        {
            Notice?.Invoke(StringResources.Get("no_hay_imagenes"));
            return [];
        }

        ImageCutter cutter = Cutter;
        string[] images = [.. Images];
        try
        {
            List<string> written = await Task.Run(() => cutter.Cut(images, folder, share => Progress = share));
            Notice?.Invoke(StringResources.Get("proceso_completado"));
            return written;
        }
        catch (Exception exception) when (exception is MagickException or IOException or UnauthorizedAccessException)
        {
            Notice?.Invoke(exception.Message);
            return [];
        }
        finally
        {
            Progress = 0.0;
        }
    }
}
