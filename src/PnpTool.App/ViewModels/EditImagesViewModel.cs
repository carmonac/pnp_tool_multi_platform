using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagick;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Imaging;

namespace PnpTool.App.ViewModels;

/// <summary>
/// The tool that changes many images at once: size, effect and format.
/// Original: <c>WindowEditarimagenes</c>.
/// </summary>
public sealed partial class EditImagesViewModel : ViewModelBase
{
    private readonly ProjectSettings _settings;

    public EditImagesViewModel()
        : this(new ProjectSettings())
    {
    }

    /// <param name="settings">The project, for the effects that need the size of a card.</param>
    public EditImagesViewModel(ProjectSettings settings)
    {
        _settings = settings;
        Effects = ImageEffectOption.All(withMirror: false);
        _selectedEffect = Effects[0];
    }

    public ObservableCollection<string> Images { get; } = [];

    [ObservableProperty]
    private string? _selectedImage;

    /// <summary>The new width, as a percentage; an empty box is 100.</summary>
    [ObservableProperty]
    private string _scaleXText = "100";

    [ObservableProperty]
    private string _scaleYText = "100";

    public IReadOnlyList<ImageEffectOption> Effects { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectHasSize))]
    private ImageEffectOption _selectedEffect;

    [ObservableProperty]
    private string _effectSizeText = "20";

    public bool EffectHasSize => ImageEffects.HasSize(SelectedEffect.Value);

    [ObservableProperty]
    private ArgbColor _background = ArgbColor.Parse(AppSettings.ColorWhite);

    public IReadOnlyList<string> Extensions { get; } = ImageBatchEditor.Extensions;

    [ObservableProperty]
    private string _extension = ImageBatchEditor.Extensions[0];

    [ObservableProperty]
    private FilterType _filter = FilterType.Lanczos;

    [ObservableProperty]
    private bool _replace;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private double _progressMaximum = 1.0;

    /// <summary>Raised to tell the user something.</summary>
    public event Action<string>? Notice;

    /// <summary>Adds images, and the images inside folders. Original: <c>AnadirImagen</c>.</summary>
    public void Add(IEnumerable<string> paths)
    {
        foreach (string path in Panel.ImagePanelViewModel.ImagesAmong(paths))
        {
            if (Images.Count == 0)
            {
                AppSettings.LastFolder = Path.GetDirectoryName(path) ?? AppSettings.LastFolder;
            }

            Images.Add(path);
        }
    }

    /// <summary>Original: <c>BorrarFicherosSeleccionados</c>.</summary>
    public void RemoveSelected()
    {
        if (SelectedImage != null)
        {
            Images.Remove(SelectedImage);
        }
    }

    /// <summary>The settings as the editor takes them.</summary>
    public ImageBatchEditor Editor => new()
    {
        ScaleX = Percentage(ScaleXText),
        ScaleY = Percentage(ScaleYText),
        Effect = SelectedEffect.Value,
        EffectSize = int.TryParse(EffectSizeText, out int size) ? size : 0,
        Background = Background,
        Extension = Extension,
        Filter = Filter,
        Replace = Replace,
    };

    private static double Percentage(string? text) =>
        double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value) && value > 0.0 ? value : 100.0;

    /// <summary>
    /// Changes the images, writing into the folder those that are not replaced.
    /// Original: <c>Boton_Procesar_Click</c>.
    /// </summary>
    public async Task<List<string>> ProcessAsync(string folder)
    {
        if (Images.Count == 0)
        {
            Notice?.Invoke(StringResources.Get("faltan_datos"));
            return [];
        }

        ImageBatchEditor editor = Editor;
        string[] images = [.. Images];
        ProgressMaximum = images.Length;
        Progress = 0.0;
        try
        {
            (List<string> written, List<string> problems) = await Task.Run(() => editor.Edit(images, folder, _settings, done => Progress = done));
            if (problems.Count != 0)
            {
                Notice?.Invoke(string.Join(Environment.NewLine, problems));
            }

            return written;
        }
        finally
        {
            Progress = 0.0;
        }
    }
}
