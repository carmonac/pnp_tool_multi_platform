using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Controls;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Primitives;

namespace PnpTool.App.ViewModels;

/// <summary>A way of adding images to cards of two faces, or to be asked each time, as a list shows it. Original: <c>ComboboxItemSubirImagenes</c>.</summary>
public sealed record AddImagesChoiceOption(string Text, AddImagesResult Value)
{
    public override string ToString() => Text;
}

/// <summary>A quality for the pictures of the cards on screen, as a list shows it. Original: <c>ComboboxItemTiposResolucionImagen</c>.</summary>
public sealed record PanelResolutionOption(string Text, ImageResolutionType Value)
{
    public override string ToString() => Text;
}

/// <summary>What accepting the program settings changed that the main window has to follow.</summary>
public sealed record ProgramSettingsChange(bool Language, bool Unit, bool PanelResolution, bool DefaultProfiles, bool PathOnHover);

/// <summary>
/// The settings of the program, which are the same for every project: language, units,
/// update notifications, resolution limits, default colour profiles, quality of the
/// pictures on screen, printer margin, how images are added to cards of two faces, and a
/// few switches and numbers. Original: <c>WindowSetupPnpTool</c> with <c>WindowSetupPnpToolModelo</c>.
/// </summary>
/// <remarks>
/// Not here, because the port has nothing behind them: sending usage statistics, floating
/// settings windows, and the pages of the printing company the window could show.
/// </remarks>
public sealed partial class ProgramSettingsViewModel : ViewModelBase
{
    public ProgramSettingsViewModel()
    {
        Language = AppLanguage.Current;
        SelectedUnit = AppSettings.MeasurementSystem == UnitType.Millimeter ? UnitType.Millimeter : UnitType.Inch;

        UpdateOptions =
        [
            new UpdateChannelOption(UpdateChannel.None, StringResources.Get("ComboBox_Actualizacion_Ninguna")),
            new UpdateChannelOption(UpdateChannel.Major, StringResources.Get("ComboBox_Actualizacion_Mayor")),
            new UpdateChannelOption(UpdateChannel.Minor, StringResources.Get("ComboBox_Actualizacion_Menor")),
            new UpdateChannelOption(UpdateChannel.Tester, StringResources.Get("ComboBox_Actualizacion_Tester")),
        ];
        _selectedUpdates = UpdateOptions.First(option => option.Channel == AppSettings.UpdateNotifications);

        _minimumDpiText = AppSettings.MinimumDpi.ToString();
        _maximumDpiText = AppSettings.MaximumDpi.ToString();
        _targetDpiText = AppSettings.TargetDpi.ToString();

        _rgbProfile = AppSettings.RgbIccPath;
        _cmykProfile = AppSettings.CmykIccPath;

        ResolutionOptions =
        [
            .. Enum.GetValues<ImageResolutionType>().Select(type => new PanelResolutionOption(StringResources.Get("Resolucion_" + type), type)),
        ];
        _selectedResolution = ResolutionOptions.First(option => option.Value == AppSettings.PanelResolution);

        _marginXText = AppSettings.PrinterMargin.X.ToDisplayString();
        _marginYText = AppSettings.PrinterMargin.Y.ToDisplayString();

        AddImagesOptions =
        [
            new AddImagesChoiceOption(StringResources.Get("Form_SubirImagenes_OpcionPreguntar"), AddImagesResult.Ask),
            new AddImagesChoiceOption(StringResources.Get("Form_SubirImagenes_RadioButton_11"), AddImagesResult.OneFrontOneBack),
            new AddImagesChoiceOption(StringResources.Get("Form_SubirImagenes_RadioButton_N1"), AddImagesResult.ManyFrontsOneBack),
            new AddImagesChoiceOption(StringResources.Get("Form_SubirImagenes_RadioButton_1N"), AddImagesResult.OneFrontManyBacks),
            new AddImagesChoiceOption(StringResources.Get("Form_SubirImagenes_RadioButton_NN"), AddImagesResult.ManyFrontsManyBacks),
        ];
        _selectedAddImages = AddImagesOptions.First(option => option.Value == AppSettings.DefaultAddImagesChoice);

        _showPathOnHover = AppSettings.ShowPathOnHover;
        _openPdfWhenDone = AppSettings.OpenPdfWhenDone;
        _showCmykWarning = AppSettings.ShowCmykWarning;
        _startMaximized = AppSettings.StartMaximized;
        _writeLogs = AppSettings.WriteLogs;
        _embedColorProfile = AppSettings.EmbedColorProfile;

        _smallFileSizeText = AppSettings.SmallFileSizeKb.ToString();
        _performanceCountText = AppSettings.PerformanceWarningImageCount.ToString();
        _tempFolder = AppSettings.TempFolder;
    }

    /// <summary>Tells the window of something the user has to read.</summary>
    public event Action<string>? Notice;

    /// <summary>The language chosen; the window sets it from its list. It takes effect when the settings are accepted.</summary>
    public SupportedLanguage Language
    {
        get;
        set
        {
            if (field != null && value.Code != field.Code && !value.IsUpToDate)
            {
                Notice?.Invoke(StringResources.Get("Idioma_Actualizar"));
            }

            field = value;
        }
    }

    /// <summary>Millimetres or inches; the window sets it from its selector.</summary>
    public UnitType SelectedUnit { get; set; }

    public IReadOnlyList<UpdateChannelOption> UpdateOptions { get; }

    [ObservableProperty]
    private UpdateChannelOption _selectedUpdates;

    // ---- Resolution of the images, in dots per inch ----

    [ObservableProperty]
    private string _minimumDpiText;

    [ObservableProperty]
    private string _maximumDpiText;

    [ObservableProperty]
    private string _targetDpiText;

    // ---- Default colour profiles. Original: Control_PerfilesICCDefecto ----

    [ObservableProperty]
    private string _rgbProfile;

    [ObservableProperty]
    private string _cmykProfile;

    // ---- Pictures of the cards on screen. Original: ComboBox_ResolucionImagenes ----

    public IReadOnlyList<PanelResolutionOption> ResolutionOptions { get; }

    [ObservableProperty]
    private PanelResolutionOption _selectedResolution;

    /// <summary>Another quality warns of what it costs. Original: <c>ComboBoxResolucion_SelectedIndexChanged</c>.</summary>
    partial void OnSelectedResolutionChanged(PanelResolutionOption value)
    {
        if (value != null && value.Value != AppSettings.PanelResolution)
        {
            Notice?.Invoke(StringResources.Get("Resolucion_Warning"));
        }
    }

    // ---- Printer margin ----

    [ObservableProperty]
    private string _marginXText;

    [ObservableProperty]
    private string _marginYText;

    // ---- Adding images to cards of two faces. Original: ComboBox_SubirImagenes ----

    public IReadOnlyList<AddImagesChoiceOption> AddImagesOptions { get; }

    [ObservableProperty]
    private AddImagesChoiceOption _selectedAddImages;

    // ---- Switches ----

    [ObservableProperty]
    private bool _showPathOnHover;

    [ObservableProperty]
    private bool _openPdfWhenDone;

    [ObservableProperty]
    private bool _showCmykWarning;

    [ObservableProperty]
    private bool _startMaximized;

    [ObservableProperty]
    private bool _writeLogs;

    [ObservableProperty]
    private bool _embedColorProfile;

    // ---- Numbers and the folder for temporary files ----

    /// <summary>In kilobytes.</summary>
    [ObservableProperty]
    private string _smallFileSizeText;

    [ObservableProperty]
    private string _performanceCountText;

    [ObservableProperty]
    private string _tempFolder;

    // ---- Accepting ----

    /// <summary>
    /// What stops the settings from being accepted, or null: a folder for temporary files
    /// that is not there, resolutions out of order, or a default profile that is not a file.
    /// Original: <c>WindowSetupPnpTool.ConfiguracionValida</c> and the model's.
    /// </summary>
    public string? Problem()
    {
        if (string.IsNullOrEmpty(TempFolder) || !Directory.Exists(TempFolder))
        {
            return "Temporary files folder do not exist or must be specified " + Path.GetTempPath();
        }

        int minimum = IntegerTextBox.Parse(MinimumDpiText);
        int maximum = IntegerTextBox.Parse(MaximumDpiText);
        int target = IntegerTextBox.Parse(TargetDpiText);
        if (minimum > maximum)
        {
            return "DPI MIN > DPI MAX";
        }

        if (target < minimum || target > maximum)
        {
            return "DPI MIN > DPI OBJ OR DPI OBJ > DPI MAX";
        }

        foreach (string profile in new[] { RgbProfile, CmykProfile })
        {
            if (!File.Exists(profile))
            {
                return StringResources.Get("Form_Setup_PnpTool_Label_Perfiles_ICC_Defecto") + ": " + profile;
            }
        }

        return null;
    }

    /// <summary>
    /// Puts the settings to use and saves them. Gives what changed that the main window
    /// shows. Original: <c>WindowSetupPnpToolModelo.Boton_Aceptar</c>.
    /// </summary>
    public ProgramSettingsChange Apply()
    {
        var change = new ProgramSettingsChange(
            Language: Language.Code != AppLanguage.Current.Code,
            Unit: SelectedUnit != MeasurementSettings.Current,
            PanelResolution: SelectedResolution.Value != AppSettings.PanelResolution,
            DefaultProfiles: RgbProfile != AppSettings.RgbIccPath || CmykProfile != AppSettings.CmykIccPath,
            PathOnHover: ShowPathOnHover != AppSettings.ShowPathOnHover);

        // Lengths are read in the unit they were typed in, before the unit changes.
        var margin = new UnitPoint { X = Parse(MarginXText), Y = Parse(MarginYText) };

        AppLanguage.Change(Language);
        AppSettings.MeasurementSystem = SelectedUnit;
        AppSettings.UpdateNotifications = SelectedUpdates.Channel;
        AppSettings.MinimumDpi = IntegerTextBox.Parse(MinimumDpiText);
        AppSettings.MaximumDpi = IntegerTextBox.Parse(MaximumDpiText);
        AppSettings.TargetDpi = IntegerTextBox.Parse(TargetDpiText);
        AppSettings.RgbIccPath = RgbProfile;
        AppSettings.CmykIccPath = CmykProfile;
        AppSettings.PanelResolution = SelectedResolution.Value;
        AppSettings.PrinterMargin = margin;
        AppSettings.DefaultAddImagesChoice = SelectedAddImages.Value;
        AppSettings.ShowPathOnHover = ShowPathOnHover;
        AppSettings.OpenPdfWhenDone = OpenPdfWhenDone;
        AppSettings.ShowCmykWarning = ShowCmykWarning;
        AppSettings.StartMaximized = StartMaximized;
        AppSettings.WriteLogs = WriteLogs;
        AppSettings.EmbedColorProfile = EmbedColorProfile;
        AppSettings.SmallFileSizeKb = IntegerTextBox.Parse(SmallFileSizeText);
        AppSettings.PerformanceWarningImageCount = IntegerTextBox.Parse(PerformanceCountText);
        AppSettings.TempFolder = TempFolder.EndsWith(Path.DirectorySeparatorChar) ? TempFolder : TempFolder + Path.DirectorySeparatorChar;
        AppSettings.Save();
        return change;
    }

    private static Unit Parse(string? text) => string.IsNullOrEmpty(text) ? Unit.Zero : Unit.FromDisplayString(text);
}
