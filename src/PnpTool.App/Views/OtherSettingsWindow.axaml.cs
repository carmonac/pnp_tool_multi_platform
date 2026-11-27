using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PnpTool.App.ViewModels;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Projects;

namespace PnpTool.App.Views;

/// <summary>
/// The other settings of a project. It closes with the <see cref="OtherSettings"/> when
/// accepted and with nothing when cancelled. Original: <c>WindowSetupOtrosParametros</c>.
/// </summary>
public partial class OtherSettingsWindow : Window
{
    public OtherSettingsWindow()
        : this(new OtherSettingsViewModel(new OtherSettings(), SettingsGroup.Custom))
    {
    }

    public OtherSettingsWindow(OtherSettingsViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = StringResources.Get("Boton_Configurar_OtrosParametros");
        ColorSpaceLabel.Text = StringResources.Get("Form_Setup_Otros_Label_ColorSpace");
        PathLabel.Text = StringResources.Get("Form_Setup_Otros_Label_RutaImagenes");
        OutputLabel.Text = StringResources.Get("Form_Setup_Otros_Label_FicheroPDFSalida");
        BlockSizeLabel.Text = StringResources.Get("Tamano");
        MaximumSizeLabel.Text = StringResources.Get("Form_Setup_Otros_Label_FicheroPDFSalida_Tamano");
        FileIdCheck.Content = StringResources.Get("Form_Setup_Otros_Checkbox_ID");
        SeedLabel.Text = StringResources.Get("Form_Setup_Otros_Label_ID");
        CompressionLabel.Text = StringResources.Get("Form_Setup_Otros_Label_Comprimir");
        ToolTip.SetTip(BlockSizeLabel, "(int)");
        ToolTip.SetTip(BlockSizeBox, "(int)");
        ToolTip.SetTip(MaximumSizeLabel, "(MB)");
        ToolTip.SetTip(MaximumSizeBox, "(MB)");
        ToolTip.SetTip(PercentBox, "%");
        ToolTip.SetTip(ProfileButton, "Icc profile");

        FilterBox.SelectedFilter = model.ResizeFilter;
    }

    public OtherSettingsViewModel Model { get; }

    /// <summary>Shows a message; the test of the window replaces it.</summary>
    public Func<string, Task>? ShowMessage { get; set; }

    /// <summary>Asks for the file of a colour profile; the test of the window replaces it. Gives null when cancelled.</summary>
    public Func<Task<string?>>? PickProfile { get; set; }

    private Task Say(string message) => ShowMessage != null ? ShowMessage(message) : new MessageWindow(message).ShowDialog(this);

    /// <summary>Accepts the settings, as the accept button does. Original: <c>Boton_Ok_Click</c>.</summary>
    public async Task AcceptAsync()
    {
        Model.ResizeFilter = FilterBox.SelectedFilter;
        string? problem = Model.Problem();
        if (problem != null)
        {
            await Say(problem);
            return;
        }

        Close(Model.Result());
    }

    /// <summary>Chooses the colour profile, as its button does. Original: <c>Boton_PerfilIcc_Click</c>.</summary>
    public async Task PickProfileAsync()
    {
        string? path = PickProfile != null ? await PickProfile() : await AskProfileAsync();
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        string? problem = Model.UseProfileFile(path);
        if (problem != null)
        {
            await Say(problem);
        }
    }

    private async Task<string?> AskProfileAsync()
    {
        string? folder = Path.GetDirectoryName(Model.ProfilePath);
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

    private async void OnPickProfile(object? sender, RoutedEventArgs e) => await PickProfileAsync();
}
