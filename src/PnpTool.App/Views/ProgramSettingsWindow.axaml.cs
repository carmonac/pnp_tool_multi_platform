using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PnpTool.App.ViewModels;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.Views;

/// <summary>
/// The settings of the program. It closes with a <see cref="ProgramSettingsChange"/> after
/// putting the settings to use and saving them, or with nothing when cancelled.
/// Original: <c>WindowSetupPnpTool</c>.
/// </summary>
/// <remarks>
/// The original changed the language of the program as soon as one was picked in the list,
/// and put the old one back on cancel. Here the language changes when the window is accepted.
/// </remarks>
public partial class ProgramSettingsWindow : Window
{
    public ProgramSettingsWindow()
        : this(new ProgramSettingsViewModel())
    {
    }

    public ProgramSettingsWindow(ProgramSettingsViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = StringResources.Get("ShellMenuBarItem_Configuracion_Herramienta");
        LanguageHeading.Text = StringResources.Get("Form_Setup_PnpTool_Label_Idioma");
        UnitHeading.Text = StringResources.Get("Form_Setup_PnpTool_GroupBox_Unidades");
        UpdatesHeading.Text = StringResources.Get("Form_Setup_PnpTool_Label_Actualizacion");
        DpiHeading.Text = StringResources.Get("Form_Setup_PnpTool_GroupBox_DPI");
        MinimumDpiLabel.Text = StringResources.Get("Form_Setup_PnpTool_Label_DPIMinimo");
        MaximumDpiLabel.Text = StringResources.Get("Form_Setup_PnpTool_Label_DPIMaximo");
        TargetDpiLabel.Text = StringResources.Get("Form_Setup_PnpTool_Label_DPIObjetivo");
        ProfilesHeading.Text = StringResources.Get("Form_Setup_PnpTool_Label_Perfiles_ICC_Defecto");
        ResolutionHeading.Text = StringResources.Get("Form_Setup_PnpTool_GroupBox_ResolucionImagenes");
        MarginHeading.Text = StringResources.Get("Form_Setup_PnpTool_GroupBox_MargenImpresora");
        MarginXLabel.Text = StringResources.Get("Ancho");
        MarginYLabel.Text = StringResources.Get("Alto");
        AddImagesHeading.Text = StringResources.Get("Form_Setup_PnpTool_Label_PreguntarSubirImagenes");
        OthersHeading.Text = StringResources.Get("Form_Setup_Resto");
        PathOnHoverCheck.Content = StringResources.Get("Form_Setup_PnpTool_CheckBox_mostar_ruta_panel");
        OpenPdfCheck.Content = StringResources.Get("Form_Setup_PnpTool_CheckBox_CheckBox_abrir_fin");
        CmykWarningCheck.Content = StringResources.Get("Form_Setup_PnpTool_CheckBox_CheckBox_AvisoCMYK_RGB");
        MaximizedCheck.Content = StringResources.Get("Form_Setup_PnpTool_CheckBox_CheckBox_Maximizado");
        LogsCheck.Content = StringResources.Get("Form_Setup_PnpTool_CheckBox_CheckBoxLogs");
        EmbedProfileCheck.Content = StringResources.Get("Form_Setup_PnpTool_CheckBox_InsertarPerfilColor");
        SmallFileLabel.Text = StringResources.Get("Form_Extraer_CheckBox_BorrarImagenesXTamano");
        PerformanceLabel.Text = StringResources.Get("Form_Setup_PnpTool_AlarmaRendimiento");
        TempFolderLabel.Text = StringResources.Get("Form_Setup_PnpTool_Label_CarpetaTemporal");
        string unit = "(" + LayoutViewModel.UnitName + ")";
        ToolTip.SetTip(MarginXBox, unit);
        ToolTip.SetTip(MarginYBox, unit);

        UnitBox.SelectedUnit = model.SelectedUnit;
        Opened += (_, _) =>
        {
            LanguageBox.Load();
            LanguageBox.LanguageChanged += (_, _) => model.Language = LanguageBox.SelectedLanguage;
        };
        model.Notice += message => _ = Say(message);
    }

    public ProgramSettingsViewModel Model { get; }

    /// <summary>Shows a message; the test of the window replaces it.</summary>
    public Func<string, Task>? ShowMessage { get; set; }

    /// <summary>Asks for the file of a colour profile; the test of the window replaces it. Gives null when cancelled.</summary>
    public Func<Task<string?>>? PickProfile { get; set; }

    /// <summary>Asks for a folder; the test of the window replaces it. Gives null when cancelled.</summary>
    public Func<Task<string?>>? PickFolder { get; set; }

    private Task Say(string message) => ShowMessage != null ? ShowMessage(message) : new MessageWindow(message).ShowDialog(this);

    /// <summary>Accepts the settings, as the accept button does. Original: <c>Boton_Aceptar_Click</c>.</summary>
    public async Task AcceptAsync()
    {
        Model.SelectedUnit = UnitBox.SelectedUnit;
        string? problem = Model.Problem();
        if (problem != null)
        {
            await Say(problem);
            return;
        }

        Close(Model.Apply());
    }

    /// <summary>Chooses a default colour profile, as its button does. Original: <c>BuscarCarpeta</c> for profiles.</summary>
    public async Task PickProfileAsync(bool cmyk)
    {
        string? path = PickProfile != null ? await PickProfile() : await AskProfileAsync(cmyk ? Model.CmykProfile : Model.RgbProfile);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (cmyk)
        {
            Model.CmykProfile = path;
        }
        else
        {
            Model.RgbProfile = path;
        }
    }

    /// <summary>Chooses the folder for temporary files, as its button does.</summary>
    public async Task PickTempFolderAsync()
    {
        string? path = PickFolder != null ? await PickFolder() : await DeckWindows.PickFolderAsync(this);
        if (!string.IsNullOrEmpty(path))
        {
            Model.TempFolder = path;
        }
    }

    private async Task<string?> AskProfileAsync(string current)
    {
        string? folder = Path.GetDirectoryName(current);
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Icc profile",
            AllowMultiple = false,
            SuggestedStartLocation = string.IsNullOrEmpty(folder) || !Directory.Exists(folder) ? null : await StorageProvider.TryGetFolderFromPathAsync(folder),
            FileTypeFilter = [new FilePickerFileType("Icc profile") { Patterns = ["*.icc", "*.icm"] }, FilePickerFileTypes.All],
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    private async void OnAccepted(object? sender, EventArgs e) => await AcceptAsync();

    private void OnCancelled(object? sender, EventArgs e) => Close(null);

    private async void OnPickRgb(object? sender, RoutedEventArgs e) => await PickProfileAsync(cmyk: false);

    private async void OnPickCmyk(object? sender, RoutedEventArgs e) => await PickProfileAsync(cmyk: true);

    private async void OnPickTempFolder(object? sender, RoutedEventArgs e) => await PickTempFolderAsync();
}
