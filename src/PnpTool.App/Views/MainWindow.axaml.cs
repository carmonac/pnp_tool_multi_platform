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
        Closing += OnClosing;
        Opened += async (_, _) =>
        {
            if (DataContext is MainWindowViewModel model)
            {
                await model.StartAsync(StartFile);
            }
        };

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

    /// <summary>A project or style file given on the command line, opened when the window shows. Original: <c>ConfiguracionPrograma.Argumentos</c>.</summary>
    public string? StartFile { get; set; }

    private bool _closeConfirmed;

    /// <summary>
    /// Asks about unsaved changes before closing; the window stays open when the user cancels.
    /// Original: <c>Window_OnClosing</c>.
    /// </summary>
    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed || DataContext is not MainWindowViewModel model)
        {
            SavePlacement();
            return;
        }

        e.Cancel = true;
        if (await model.CloseAsync())
        {
            _closeConfirmed = true;
            Close();
        }
    }

    /// <summary>The system's dialog to open a project, a style or an advanced template.</summary>
    private async Task<string?> AskFileToOpenAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = StringResources.Get("selecciona_fichero_pnp"),
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(AppSettings.LastFolder),
            FileTypeFilter = [new FilePickerFileType("PNP Tool") { Patterns = ["*.pnp", "*.pnps", "*.pnpa"] }, FilePickerFileTypes.All],
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    /// <summary>The system's dialog to save a file of the given kind.</summary>
    private async Task<string?> AskFileToSaveAsync(string suggestedName, string kind, string extension)
    {
        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(AppSettings.LastFolder),
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = [new FilePickerFileType(kind) { Patterns = ["*." + extension] }],
        });
        return file?.TryGetLocalPath();
    }

    /// <summary>Shows the progress window while a task runs. Original: <c>Window_BarraProgreso</c>.</summary>
    private async Task<Exception?> RunWithProgressAsync(string caption, Func<Action<int>, CancellationToken, Task> work)
    {
        var window = new ProgressWindow(caption, work);
        await window.ShowDialog(this);
        return window.Failure;
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
            model.AskLayout = layout => new LayoutWindow(layout).ShowDialog<LayoutResult?>(this);
            model.AskTemplateSides = (layout, template) => new TemplateSetupWindow(layout, template).ShowDialog<LayoutResult?>(this);
            model.ShowTemplateEditor = editor => new TemplateEditorWindow(editor).ShowDialog(this);
            model.AskFolder = () => DeckWindows.PickFolderAsync(this);
            model.ShowCutImages = cut => new CutImagesWindow(cut).ShowDialog(this);
            model.ShowEditImages = edit => new EditImagesWindow(edit).ShowDialog(this);
            model.ShowPackProject = pack => new PackProjectWindow(pack).ShowDialog(this);
            model.ShowProcessFolders = folders => new ProcessFoldersWindow(folders).ShowDialog<bool?>(this);
            model.ShowExtractPdf = extract => new ExtractPdfWindow(extract).ShowDialog<List<string>?>(this);
            model.ShowMergePdf = merge => new MergePdfWindow(merge).ShowDialog(this);
            model.ShowSplitFrontsBacks = split => new SplitFrontsBacksWindow(split).ShowDialog(this);
            model.ShowModifyPdf = modify => new ModifyPdfWindow(modify).ShowDialog(this);
            model.ShowBookCover = cover => new BookCoverWindow(cover).ShowDialog(this);
            model.ShowCardSearch = search => new CardSearchWindow(search).ShowDialog(this);
            model.ShowAbout = about => new AboutWindow(about).ShowDialog(this);
            model.ShowScreenCapture = capture => new ScreenCaptureWindow(capture).ShowDialog(this);
            model.ShowPoster = poster => new PosterWindow(poster).ShowDialog(this);
            model.ShowCustomDeckCreator = creator => new CustomDeckWindow(creator).ShowDialog<string?>(this);
            model.ShowSportsDeckCreator = creator => new SportsDeckWindow(creator).ShowDialog<string?>(this);
            model.Ask = (message, withCancel) => new ConfirmWindow(message, withCancel).ShowDialog<bool?>(this);
            model.AskFileToOpen = AskFileToOpenAsync;
            model.AskProjectToSave = () => AskFileToSaveAsync(string.Empty, "PNP Tool project", "pnp");
            model.AskPdfToSave = name => AskFileToSaveAsync(Path.GetFileNameWithoutExtension(name), "PDF", "pdf");
            model.RunWithProgress = RunWithProgressAsync;
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
