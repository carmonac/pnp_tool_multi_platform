using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using PnpTool.App.Services;
using PnpTool.Controls;
using PnpTool.Core;
using PnpTool.App.ViewModels;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.App.Views;

/// <summary>The main window. Original: <c>Window_Principal_v2</c>.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        RestorePlacement();
        Closing += (_, _) => SavePlacement();

        // Original: Control_Slider_Local.Inicializa(25, 1, 75, 300, 100).
        ZoomSlider.ValueChangedCallback = value =>
        {
            if (DataContext is MainWindowViewModel model)
            {
                model.Zoom = value;
            }
        };
        ZoomSlider.Initialize(tick: 25, smallChange: 1, minimum: 75, maximum: 300, value: 100);
        CardsPanel.ZoomStepRequested += step => ZoomSlider.Step(step);
        AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        CardsPanel.ProblemFound += message => _ = new MessageWindow(message).ShowDialog(this);
    }

    /// <summary>
    /// The system's dialog to save a style, starting in the folder of the user's own styles.
    /// Original: <c>Dialogos.DialogoSalvar</c> for style files.
    /// </summary>
    private async Task<string?> AskStyleFileAsync()
    {
        IStorageFolder? folder = await StorageProvider.TryGetFolderFromPathAsync(AppPaths.CustomStylesFolder);
        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedStartLocation = folder,
            SuggestedFileName = "EstiloAvanzado",
            DefaultExtension = "pnps",
            FileTypeChoices = [new FilePickerFileType("PNP Tool style") { Patterns = ["*.pnps"] }],
        });
        return file?.TryGetLocalPath();
    }

    /// <summary>
    /// The keys of the main window: the system's undo and redo shortcuts (Ctrl+Z and Ctrl+Y,
    /// or Cmd+Z and Shift+Cmd+Z on macOS) and Delete, which removes the selected cards. While
    /// the user is typing in a box the keys are left to the box.
    /// Original: <c>PulsarTecla</c>, which had Ctrl+Z and Delete.
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel model || FocusManager?.GetFocusedElement() is TextBox)
        {
            return;
        }

        PlatformHotkeyConfiguration? keys = Application.Current?.PlatformSettings?.HotkeyConfiguration;
        if (keys?.Redo.Any(gesture => gesture.Matches(e)) == true)
        {
            model.Redo();
            e.Handled = true;
        }
        else if (keys?.Undo.Any(gesture => gesture.Matches(e)) == true)
        {
            model.Undo();
            e.Handled = true;
        }
        else if (e.Key is Key.Delete or Key.Back && e.KeyModifiers == KeyModifiers.None)
        {
            model.DeleteSelected();
            e.Handled = true;
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel model)
        {
            model.PickImages = () => ImageFiles.PickAsync(this, multiple: true);
            model.ShowMessage = message => new MessageWindow(message).ShowDialog(this);
            model.AskSelection = cards => new SelectCardsWindow(cards).ShowDialog<CardSelection?>(this);
            model.AskColor = color => new ColorPickerDialog(color).ShowDialog<ArgbColor?>(this);
            model.AskFaceChange = toDouble => new FaceChangeWindow(toDouble).ShowDialog<FaceChangeChoice?>(this);
            model.AskStyleFile = AskStyleFileAsync;
            model.AskImageChange = (cards, face, selected) => new ChangeImagesWindow(cards, face, selected).ShowDialog<ImageChange?>(this);
        }
    }

    /// <summary>
    /// Opens maximised, or at the size it had when it was last closed, as the settings say.
    /// A saved size larger than the screen it opens on is ignored. Original: the start of
    /// <c>Window_OnLoad</c> with <c>IniciarVentanaMaximizada</c> and <c>TamanoVentanaAbierta</c>.
    /// </summary>
    private void RestorePlacement()
    {
        PointI saved = AppSettings.WindowSize;
        if (saved.X >= MinWidth && saved.Y >= MinHeight)
        {
            PixelRect? area = Screens.Primary?.WorkingArea;
            double scale = Screens.Primary?.Scaling ?? 1.0;
            if (area == null || (saved.X <= area.Value.Width / scale && saved.Y <= area.Value.Height / scale))
            {
                Width = saved.X;
                Height = saved.Y;
            }
        }

        if (AppSettings.StartMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    /// <summary>Remembers the size of the window, unless it is maximised or minimised, and saves the settings.</summary>
    private void SavePlacement()
    {
        if (WindowState == WindowState.Normal)
        {
            AppSettings.WindowSize = new PointI((int)Width, (int)Height);
        }

        AppSettings.StartMaximized = WindowState == WindowState.Maximized;
        try
        {
            AppSettings.Save();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Write("Settings not saved: " + exception.Message, force: true);
        }
    }
}
