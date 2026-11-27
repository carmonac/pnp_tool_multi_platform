using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagick;
using PnpTool.Controls;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Projects;

namespace PnpTool.App.ViewModels;

/// <summary>
/// The other settings of a project: colour space and profile, how images are written in
/// project files, the order of the cards on the sheets, what files the PDF comes out as,
/// the file identifier, compression and the filter to resize images.
/// Original: <c>WindowSetupOtrosParametros</c> with its controls
/// <c>Control_SelectorEspacioColor</c>, <c>Control_CaracteristicasFicheroPDFSalida</c>,
/// <c>Control_IDFichero</c> and <c>ComboBox_Filtro</c>.
/// </summary>
public sealed partial class OtherSettingsViewModel : ViewModelBase
{
    private readonly OtherSettings _settings = new();
    private bool _loading = true;

    /// <param name="settings">The settings of the project; they are not changed.</param>
    /// <param name="group">The group of the project, which decides what is offered.</param>
    public OtherSettingsViewModel(OtherSettings settings, SettingsGroup group)
    {
        _settings.CopyFrom(settings);
        if (group == SettingsGroup.Dice7Pixel)
        {
            _settings.ColorSpace = ProjectColorSpace.RGB;
        }

        bool sheetsOnly = group is SettingsGroup.Tabletop or SettingsGroup.Dice;
        OutputOffered = !sheetsOnly;
        ColorSpaceEnabled = group != SettingsGroup.Dice7Pixel;
        PdfOutputEnabled = group != SettingsGroup.Soriano;
        FileIdEnabled = group != SettingsGroup.Soriano;

        ColorSpaceOptions = PdfColorSpaceOption.All();
        _selectedColorSpace = ColorSpaceOptions.First(option => option.Value == _settings.ColorSpace);

        PathOptions = ImagePathOption.All();
        _selectedPath = PathOptions.First(option => option.Value == _settings.ImagePathMode);

        LayoutOptions = StackedLayoutOption.For(_settings.StackedLayout.LayoutType);
        _selectedLayout = LayoutOptions.First(option => option.Value == _settings.StackedLayout.LayoutType);
        _blockSizeText = _settings.StackedLayout.BlockSize.ToString();

        PdfOutputOptions = PdfOutputFileOption.All();
        _selectedPdfOutput = PdfOutputOptions.First(option => option.Value == _settings.PdfOutput);
        _maximumSizeText = _settings.PdfMaximumSize.ToString();

        _generateFileId = _settings.GenerateFileId;
        _fileIdSeed = _settings.FileIdSeed;

        CompressionOptions = CompressionOption.All();
        _selectedCompression = CompressionOptions.First(option => option.Value == _settings.Compression);
        _compressionPercentText = ((int)_settings.CompressionPercent).ToString();

        _resizeFilter = _settings.ResizeFilter;
        _loading = false;
    }

    // ---- Colour. Original: Control_SelectorEspacioColor ----

    /// <summary>The "seven pixels" dice are always RGB.</summary>
    public bool ColorSpaceEnabled { get; }

    public IReadOnlyList<PdfColorSpaceOption> ColorSpaceOptions { get; }

    [ObservableProperty]
    private PdfColorSpaceOption _selectedColorSpace;

    /// <summary>The name of the colour profile, or that there is none. Original: <c>ActualizaSIcc</c>.</summary>
    public string ProfileName =>
        string.IsNullOrEmpty(_settings.ColorProfile?.Description) ? "Icc not set" : _settings.ColorProfile.Description;

    /// <summary>The file of the colour profile, where the file dialog starts.</summary>
    public string ProfilePath => _settings.ColorProfilePath;

    /// <summary>
    /// A colour space brings the profile the program has for it.
    /// Original: <c>Form_Setup_Otros_ComboBox_ColorSpace_SelectionChanged</c>.
    /// </summary>
    partial void OnSelectedColorSpaceChanged(PdfColorSpaceOption value)
    {
        if (_loading || value == null)
        {
            return;
        }

        _settings.ColorSpace = value.Value;
        UseProfile(value.Value == ProjectColorSpace.CMYK ? AppSettings.CmykIccPath : AppSettings.RgbIccPath);
    }

    /// <summary>
    /// Takes a colour profile from a file. A file that is not a profile is refused and says
    /// so. Original: <c>Boton_PerfilIcc_Click</c>.
    /// </summary>
    public string? UseProfileFile(string path)
    {
        try
        {
            var profile = new ColorProfile(path);
            _ = profile.Description;
            _settings.ColorProfile = profile;
            _settings.ColorProfilePath = path;
            OnPropertyChanged(nameof(ProfileName));
            OnPropertyChanged(nameof(ProfilePath));
            return null;
        }
        catch (Exception exception) when (exception is MagickException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException)
        {
            return Path.GetFileName(path) + ": " + exception.Message;
        }
    }

    private void UseProfile(string path)
    {
        _settings.ColorProfilePath = path;
        _settings.ColorProfile = IccProfiles.TryLoad(path);
        OnPropertyChanged(nameof(ProfileName));
        OnPropertyChanged(nameof(ProfilePath));
    }

    // ---- Paths of the images ----

    public IReadOnlyList<ImagePathOption> PathOptions { get; }

    [ObservableProperty]
    private ImagePathOption _selectedPath;

    // ---- Order of the cards and files of the PDF. Original: Control_CaracteristicasFicheroPDFSalida ----

    /// <summary>Sheets for Tabletop Simulator and dice have no PDF options, identifier or compression.</summary>
    public bool OutputOffered { get; }

    public bool PdfOutputEnabled { get; }

    public IReadOnlyList<StackedLayoutOption> LayoutOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BlockSizeOffered))]
    private StackedLayoutOption _selectedLayout;

    /// <summary>How many sheets are piled up to be cut together.</summary>
    [ObservableProperty]
    private string _blockSizeText;

    public bool BlockSizeOffered => SelectedLayout.Value != LayoutType.Standard;

    public IReadOnlyList<PdfOutputFileOption> PdfOutputOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaximumSizeOffered))]
    private PdfOutputFileOption _selectedPdfOutput;

    /// <summary>In megabytes.</summary>
    [ObservableProperty]
    private string _maximumSizeText;

    public bool MaximumSizeOffered => SelectedPdfOutput.Value == PdfOutputMode.MaximumSize;

    // ---- File identifier. Original: Control_IDFichero ----

    public bool FileIdEnabled { get; }

    [ObservableProperty]
    private bool _generateFileId;

    [ObservableProperty]
    private string _fileIdSeed;

    // ---- Compression ----

    public IReadOnlyList<CompressionOption> CompressionOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompressionPercentOffered))]
    private CompressionOption _selectedCompression;

    [ObservableProperty]
    private string _compressionPercentText;

    public bool CompressionPercentOffered => SelectedCompression.Value == CompressionType.Percentage;

    // ---- Filter ----

    [ObservableProperty]
    private FilterType _resizeFilter;

    // ---- Accepting ----

    /// <summary>
    /// What stops the settings from being accepted, or null: a percentage that is not
    /// between 1 and 99, or piles of no sheets. Original: the check of <c>Boton_Ok_Click</c>,
    /// which looked at the compression the project had before and not at the one chosen.
    /// </summary>
    public string? Problem()
    {
        if (OutputOffered && CompressionPercentOffered && IntegerTextBox.Parse(CompressionPercentText) is <= 0 or >= 100)
        {
            return StringResources.Get("Form_Setup_Otros_Label_Comprimir") + " (" + SelectedCompression.Text + "): > 0, < 100";
        }

        if (OutputOffered && BlockSizeOffered && IntegerTextBox.Parse(BlockSizeText) < 1)
        {
            return SelectedLayout.Text + " (" + StringResources.Get("Tamano") + "): > 0";
        }

        return null;
    }

    /// <summary>The settings as they stand in the window. Original: the end of <c>Boton_Ok_Click</c>.</summary>
    public OtherSettings Result()
    {
        var result = new OtherSettings();
        result.CopyFrom(_settings);
        result.ColorSpace = SelectedColorSpace.Value;
        result.ImagePathMode = SelectedPath.Value;
        result.StackedLayout.LayoutType = SelectedLayout.Value;
        result.StackedLayout.BlockSize = IntegerTextBox.Parse(BlockSizeText);
        result.PdfOutput = SelectedPdfOutput.Value;
        result.PdfMaximumSize = IntegerTextBox.Parse(MaximumSizeText);
        result.GenerateFileId = GenerateFileId;
        result.FileIdSeed = FileIdSeed ?? string.Empty;
        result.Compression = SelectedCompression.Value;
        result.CompressionPercent = IntegerTextBox.Parse(CompressionPercentText);
        result.ResizeFilter = ResizeFilter;
        return result;
    }
}
