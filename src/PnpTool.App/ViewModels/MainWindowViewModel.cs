using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PnpTool.App.ViewModels.Panel;
using PnpTool.App.ViewModels.Ribbon;
using PnpTool.App.Views;
using PnpTool.Core.Cards;
using PnpTool.Core;
using PnpTool.Controls;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Core.Templates;

namespace PnpTool.App.ViewModels;

/// <summary>
/// The main window: the command bar with its tabs, the quick-access buttons and the status
/// line. Original: <c>Window_Principal_v2</c> and the ribbon controls under <c>Controls/Ribbon</c>.
/// </summary>
/// <remarks>
/// Left out of the command bar: the button that sent the PDF to a printing company, the
/// Windows-only program nanDECK and the developer's variable viewer. PosteRazor and
/// AutoCaptura are replaced by tools of the program's own.
/// </remarks>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
    {
        ImagePanel.CountsChanged += (_, _) => CountsText = ImagePanel.CountsText;
        ImagePanel.ProjectChanged += (_, _) => MarkChanged();
        ProjectSettings.SettingsChanged += (_, _) => MarkChanged();
        SharedBack.Changed += (_, _) => MarkChanged();
        ImagePanel.ProjectFileDropped += path => _ = OpenAsync(path);
        ImagePanel.Notify = message => ShowMessage?.Invoke(message) ?? Task.CompletedTask;
        ImagePanel.ColorSpaceOfProject = () => ProjectSettings.Settings.Other.ColorSpace;
        ImagePanel.RequestImages = AddImagesAsync;
        ProjectSettings.SettingsChanged += (_, _) => OnSettingsChanged();
        ProjectSettings.PanelTypeChanged += OnPanelTypeChanged;
        ProjectSettings.ProblemFound += message => _ = ShowMessage?.Invoke(message);
        SharedBack.Changed += (_, _) => ProjectSettings.Settings.SharedBack.CopyFrom(SharedBack.Face);
        ImagePanel.History.Changed += (_, _) => NotifyConditionalCommands();

        ImagePanel.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName is nameof(ImagePanelViewModel.CanSwapFaces) or nameof(ImagePanelViewModel.PanelCount) or nameof(ImagePanelViewModel.IsDouble))
            {
                NotifyConditionalCommands();
            }
        };

        RibbonGroupViewModel output = Group(
            Action("Form_Principal_Boton_Preview", "preview", () => CreatePdfAsync(preview: true)),
            Action("Form_Principal_Boton_CrearFichero", "create", () => CreatePdfAsync(preview: false)));

        Tabs =
        [
            new RibbonTabViewModel(
                "ShellMenuBarItem_File",
                Group(
                    Action("Form_Principal_Boton_Nuevo", "new", () => NewAsync()),
                    Action("Form_Principal_Boton_Abrir", "open", () => OpenAsync()),
                    Action("Form_Principal_Boton_Guardar", "save", () => SaveAsync(askPath: false)),
                    Action("ShellMenuItem_File_GuardarComo", "save-as", () => SaveAsync(askPath: true))),
                Group(Action("ShellMenuBarItem_Herramientas_EmpacarProyecto", "pack", PackProjectAsync)),
                output)
            {
                Leading = new RecentFilesList(this),
            },
            new RibbonTabViewModel(
                "ShellMenuBarItem_Acciones",
                Group(
                    Action("Form_Principal_Boton_AnadirImagen", "add-image", () => AddImagesAsync()),
                    Action("Form_CortarImagenes_Boton_SeleccionarCarpeta", "folder", AddFolderAsync),
                    BlankButton(),
                    Action("Form_Principal_Boton_AnadirImagenVacia", "add-empty", () => ImagePanel.AddEmptyCard())),
                Group(
                    Action("Form_Principal_Boton_CambiarImagenes_Frontales", "fronts", () => ChangeImagesAsync(FaceType.Front), () => ImagePanel.PanelCount > 0, "Form_Principal_Boton_AnadirImagenEnBloque"),
                    Action("Form_Principal_Boton_CambiarImagenes_Traseras", "backs", () => ChangeImagesAsync(FaceType.Back), () => ImagePanel.PanelCount > 0 && ImagePanel.IsDouble, "Form_Principal_Boton_AnadirImagenEnBloque"),
                    Action("Form_Principal_Boton_IntercambiarImagenes", "swap-faces", () => ImagePanel.SwapFaces(), () => ImagePanel.CanSwapFaces),
                    Action("Form_Principal_Boton_RefreshPanels", "refresh", () => ImagePanel.RefreshImages())),
                Group(
                    Action("Form_Principal_Boton_Eliminar", "delete", () => ImagePanel.DeleteSelected()),
                    Action("Form_Principal_Boton_BorrarSeleccion", "deselect", () => ImagePanel.ClearSelection()),
                    Action("Form_Principal_Boton_SeleccionarTodos", "select-all", SelectAsync)),
                output),
            new RibbonTabViewModel(
                "ShellMenuBarItem_Configuracion",
                Group(
                    Action("Form_Setup_Boton_SalvarEstilo", "save-as", SaveStyleAsync),
                    Action("Boton_Configurar_Disposicion", "layout", EditLayoutAsync, () => ProjectSettings.CanEditLayout),
                    Action("Boton_Configurar_Cara_Frontal", "fronts", () => EditFaceAsync(FaceType.Front), () => ProjectSettings.CanEditFront),
                    Action("Boton_Configurar_Cara_Trasera", "backs", () => EditFaceAsync(FaceType.Back), () => ProjectSettings.CanEditBack),
                    Action("Boton_Configurar_EncabezadoPie", "header-footer", EditHeaderFooterAsync, () => ProjectSettings.CanEditHeaderFooter),
                    Button("Boton_Configurar_OtrosParametros", "settings", canExecute: () => ProjectSettings.CanEditOther),
                    Button("Boton_Configurar_Libros", "book", canExecute: () => ProjectSettings.CanEditBooks)),
                output)
            {
                Leading = ProjectSettings,
            },
            new RibbonTabViewModel(
                "ShellMenuBarItem_Herramientas",
                Group(
                    Action("ShellMenuBarItem_Herramientas_Extraer", "extract", ExtractPdfAsync),
                    Action("ShellMenuBarItem_Herramientas_ProcesarCarpeta", "open", () => ProcessFoldersAsync()),
                    Action("ShellMenuBarItem_Herramientas_Unir", "merge", MergePdfAsync),
                    Action("ShellMenuBarItem_Herramientas_ModificarImagenes", "edit", EditImagesAsync),
                    Action("ShellMenuBarItem_Herramientas_CortarImagen", "cut", CutImagesAsync),
                    Action("ShellMenuBarItem_Herramientas_Separar", "split", SplitFrontsBacksAsync),
                    Action("ShellMenuBarItem_Herramientas_AutoCaptura", "capture", ScreenCaptureAsync),
                    Action("ShellMenuBarItem_Herramientas_HacerPortadas", "book", BookCoverAsync),
                    Action("ShellMenuBarItem_Herramientas_ModificarPDF", "modify-pdf", ModifyPdfAsync),
                    Action("ShellMenuBarItem_Herramientas_PlantillasAvanzadas", "template", EditTemplatesAsync),
                    Action("ShellMenuBarItem_Herramientas_ImportarPDFCartas", "import-pdf", () => ImportPdfCartasAsync()),
                    Action("ShellMenuBarItem_Herramientas_Scryfall", "card-search", SearchCardsAsync),
                    new RibbonButtonViewModel(PosterButtonKey, "poster", new AsyncRelayCommand(PosterAsync)) { Label = PosterButtonText, ToolTipTitle = PosterButtonText }),
                Group(
                    Action("ShellMenuBarItem_Herramientas_CreaMazoPersonalizado", "deck", CreateCustomDeckAsync),
                    Action("ShellMenuBarItem_Herramientas_CreaMazoPersonalizadoDeportes", "sports-deck", CreateSportsDeckAsync)),
                Group(
                    Action("ShellMenuBarItem_Herramientas_Calculadora", "calculator", OpenCalculator),
                    Link("ShellMenuBarItem_Herramientas_TemplateMaker", "template-maker", AppLinks.TemplateMaker),
                    Link("TemplatemakerBoardToolStripMenuItem", "board", AppLinks.BoardTemplates))),
            new RibbonTabViewModel(
                "ShellMenuBarItem_Ayuda",
                Group(
                    new RibbonButtonViewModel("ShellMenuBarItem_Ayuda_Foro", "forum", new RelayCommand(() => Platform.Services.OpenUrl(StringResources.Get("url_foro")))),
                    Link("ShellMenuBarItem_Ayuda_Sleeve", "sleeves", AppLinks.Sleeves),
                    Link("ShellMenuBarItem_Ayuda_BGGGuiaCartas", "bgg", AppLinks.SleeveSizeGuide),
                    Link("ShellMenuBarItem_Ayuda_Youtube", "youtube", AppLinks.YouTube)),
                Group(
                    Link("ShellMenuBarItem_Ayuda_Telegram", "telegram", AppLinks.Telegram),
                    Action(AboutButtonKey, "about", AboutAsync))),
        ];

        QuickAccess =
        [
            Action("QATButton_Preview", "preview", () => CreatePdfAsync(preview: true), "Form_Principal_Boton_Preview"),
            Action("QATButton_Crear", "create", () => CreatePdfAsync(preview: false), "Form_Principal_Boton_CrearFichero"),
            Action("QATButton_Abrir", "open", () => OpenAsync(), "Form_Principal_Boton_Abrir"),
            Action("QATButton_Guardar", "save", () => SaveAsync(askPath: false), "Form_Principal_Boton_Guardar"),
            Action("QATButton_Undo", "undo", () => ImagePanel.History.Undo(), () => ImagePanel.History.CanUndo),
        ];

        // The original opens on the second tab, the one with the image actions.
        SelectedTab = Tabs[1];
        CountsText = ImagePanel.CountsText;
        UpdateTitle();
    }

    /// <summary>The cards of the project. Original: <c>PanelImagenesPrincipal</c>.</summary>
    public ImagePanelViewModel ImagePanel { get; } = new();

    /// <summary>
    /// Asks the user for image files; set by the window. Gives no files when the user
    /// cancels. Original: <c>Dialogos.DialogoSeleccionarFicherosImagenes</c>.
    /// </summary>
    public Func<bool, Task<IReadOnlyList<string>>>? PickImages { get; set; }

    /// <summary>
    /// Asks how the fronts and the backs will be chosen; set by the window. Gives null when
    /// the user cancels. Original: <c>Window_SubirImagenes</c>.
    /// </summary>
    public Func<Task<AddImagesAnswer?>>? AskFrontsAndBacks { get; set; }

    /// <summary>Shows a message the user has to read; set by the window. Original: <c>Dialogos.DialogoError</c>.</summary>
    public Func<string, Task>? ShowMessage { get; set; }


    public IReadOnlyList<RibbonTabViewModel> Tabs { get; }

    public IReadOnlyList<RibbonButtonViewModel> QuickAccess { get; }

    [ObservableProperty]
    private RibbonTabViewModel _selectedTab;

    /// <summary>Panels and cards in the project. Original: <c>Label_cuantas_imagenes</c>.</summary>
    [ObservableProperty]
    private string _countsText = string.Empty;

    /// <summary>What the last command did, shown beside the counts.</summary>
    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>Progress of the task in hand, 0 to 100. Original: <c>BarraProgreso</c>.</summary>
    [ObservableProperty]
    private double _progress;

    /// <summary>Size of the thumbnails in the image panel, in percent. Original: <c>Control_Slider_Local</c>.</summary>
    [ObservableProperty]
    private double _zoom = 100;

    partial void OnZoomChanged(double value)
    {
        // The slider reports 0 for its first value; see ZoomSlider.
        if (value > 0)
        {
            ImagePanel.Zoom = value;
        }
    }

    /// <summary>
    /// Asks for image files (or compressed files of images) and adds a card for each, at the
    /// position or at the end. With cards of two faces it first asks how fronts and backs
    /// will be chosen, unless the user said not to be asked again, and then for the fronts
    /// and for the backs. Original: <c>Form_Principal_Boton_AnadirImagen_Click</c>,
    /// <c>AnadirImagenNormal</c> and <c>FormPrincipalModeloLocal.SeleccionarImagenes</c>.
    /// </summary>
    public async Task AddImagesAsync(int position = -1)
    {
        if (PickImages == null)
        {
            return;
        }

        if (!ImagePanel.IsDouble)
        {
            await AddFilesAsync(await PickImages(true), position);
            return;
        }

        AddImagesResult choice = AppSettings.DefaultAddImagesChoice;
        bool remember = false;
        if (choice == AddImagesResult.Ask)
        {
            AddImagesAnswer? answer = AskFrontsAndBacks == null ? null : await AskFrontsAndBacks();
            if (answer == null)
            {
                return;
            }

            (choice, remember) = (answer.Choice, answer.Remember);
        }

        (bool manyFronts, bool manyBacks) = FrontBackFiles.MultiSelect(choice);
        (string[] fronts, AppError unread) = await ExpandAsync(await PickImages(manyFronts));
        string[] backs = [];
        if (fronts.Length != 0 && FrontBackFiles.AsksForBacks(choice))
        {
            (backs, AppError unreadBacks) = await ExpandAsync(await PickImages(manyBacks));
            unread.Merge(unreadBacks);
            if (backs.Length == 0)
            {
                fronts = [];
            }
        }

        if (fronts.Length == 0)
        {
            await ReportAddedAsync(new AppError(), unread);
            return;
        }

        if (remember)
        {
            AppSettings.DefaultAddImagesChoice = choice;
        }

        (fronts, backs) = FrontBackFiles.Pair(fronts, backs, choice);
        var progress = new Progress<double>(value => Progress = value);
        (_, AppError error) = await ImagePanel.AddPairsAsync(fronts, backs, position, progress);
        Progress = 0;
        await ReportAddedAsync(error, unread);
    }

    /// <summary>
    /// Asks for a folder and adds its images. The original took folders only when they were
    /// dropped on the cards.
    /// </summary>
    public async Task AddFolderAsync()
    {
        string? folder = AskFolder == null ? null : await AskFolder();
        if (!string.IsNullOrEmpty(folder))
        {
            await AddFilesAsync([folder], -1);
        }
    }

    /// <summary>
    /// Adds the images among files, folders and compressed files, as when they are dropped on
    /// the cards: in pairs of front and back when the cards show two faces. With one face and
    /// a great many images it first offers the tool that makes PDFs straight from folders.
    /// Original: the first half of <c>Subir_imagenes_WPF</c>, with <c>CompruebaAlarmaImagenes</c>.
    /// </summary>
    public async Task AddFilesAsync(IReadOnlyList<string> picked, int position)
    {
        (string[] images, AppError unread) = await ExpandAsync(picked);
        if (images.Length == 0)
        {
            await ReportAddedAsync(new AppError(), unread);
            return;
        }

        int most = AppSettings.PerformanceWarningImageCount;
        if (!ImagePanel.IsDouble && most > 0 && images.Length > most && Ask != null)
        {
            // The original went on to add the images after Cancel, and after Yes did nothing.
            bool? useFolders = await Ask(StringResources.Get("Muchas_imagenes_Drag"), true);
            if (useFolders == null)
            {
                return;
            }

            if (useFolders == true)
            {
                await ProcessFoldersAsync(Path.GetDirectoryName(images[0]));
                return;
            }
        }

        var progress = new Progress<double>(value => Progress = value);
        (_, AppError error) = await ImagePanel.AddImagesAsync(images, position, progress);
        Progress = 0;
        await ReportAddedAsync(error, unread);
    }

    private static async Task<(string[] Images, AppError Error)> ExpandAsync(IReadOnlyList<string> picked) =>
        picked.Count == 0 ? ([], new AppError()) : await Task.Run(() => ImagePanelViewModel.Expand(picked));

    /// <summary>Tells of the images that were not found and of the compressed files that could not be read.</summary>
    private async Task ReportAddedAsync(AppError error, AppError unread)
    {
        if ((error.HasError || error.HasWarning) && ShowMessage != null)
        {
            string title = error.HasWarning ? StringResources.Get("Imagen_no_encontrada") + Environment.NewLine : string.Empty;
            await ShowMessage(title + error.Message);
        }

        if (unread.HasError && ShowMessage != null)
        {
            await ShowMessage(unread.Message);
        }
    }

    /// <summary>
    /// Asks what to select, in a window; set by the main window. Gives null when the user
    /// closes it without accepting. Original: <c>Window_SeleccionarPanelesCaras</c>.
    /// </summary>
    public Func<int, Task<CardSelection?>>? AskSelection { get; set; }

    /// <summary>
    /// Asks for an image and the cards to put it on, in a window; set by the main window.
    /// The arguments are the number of cards, the face and the number of selected faces.
    /// Original: <c>Window_Anadir_Muchas_Imagenes_v2</c>.
    /// </summary>
    public Func<int, FaceType, int, Task<ImageChange?>>? AskImageChange { get; set; }

    /// <summary>
    /// Selects cards, faces or both in a range the user gives. Without the window, as in a
    /// test, it selects every card. Original: <c>PanelImagenes.SeleccionarTodo</c>.
    /// </summary>
    public async Task SelectAsync()
    {
        if (ImagePanel.PanelCount == 0)
        {
            return;
        }

        if (AskSelection == null)
        {
            ImagePanel.Select(0, ImagePanel.PanelCount, cards: true, FaceType.None);
            return;
        }

        CardSelection? selection = await AskSelection(ImagePanel.PanelCount);
        if (selection != null && (selection.Cards || selection.Faces != FaceType.None))
        {
            ImagePanel.Select(selection.From, selection.To, selection.Cards, selection.Faces);
        }
    }

    /// <summary>
    /// Puts one image on the fronts, or the backs, of the selected cards or of a range.
    /// Original: <c>Form_Principal_Boton_AnadirImagenEnBloque_Click</c>.
    /// </summary>
    public async Task ChangeImagesAsync(FaceType face)
    {
        if (AskImageChange == null || ImagePanel.PanelCount == 0)
        {
            return;
        }

        ImageChange? change = await AskImageChange(ImagePanel.PanelCount, face, ImagePanel.SelectedFaces.Count);
        if (change == null)
        {
            return;
        }

        if (change.OnSelected)
        {
            ImagePanel.ChangeSelectedImages(change.Image, change.Face);
        }
        else
        {
            ImagePanel.ChangeImages(change.Image, change.Face, change.From, change.To);
        }
    }

    /// <summary>Undoes the last change to the cards. Original: <c>QATButton_Undo_Click</c> and Ctrl+Z.</summary>
    public void Undo() => ImagePanel.History.Undo();

    /// <summary>Does again the last change undone. The original could not redo.</summary>
    public void Redo() => ImagePanel.History.Redo();

    /// <summary>Deletes the selected cards. Original: the Delete key in <c>PulsarTecla</c>.</summary>
    public void DeleteSelected() => ImagePanel.DeleteSelected();

    private void NotifyConditionalCommands()
    {
        foreach (RelayCommand command in _conditionalCommands)
        {
            command.NotifyCanExecuteChanged();
        }

        foreach (AsyncRelayCommand command in _conditionalAsyncCommands)
        {
            command.NotifyCanExecuteChanged();
        }
    }

    /// <summary>The commands that are not always available.</summary>
    private readonly List<RelayCommand> _conditionalCommands = [];

    /// <summary>A button that does something, always or while a condition holds.</summary>
    private RibbonButtonViewModel Action(string key, string icon, Action action, Func<bool>? canExecute = null)
    {
        var command = canExecute == null ? new RelayCommand(action) : new RelayCommand(action, canExecute);
        if (canExecute != null)
        {
            _conditionalCommands.Add(command);
        }

        return new RibbonButtonViewModel(key, icon, command);
    }

    private static RibbonButtonViewModel Action(string key, string icon, Func<Task> action, string? textKey = null) =>
        new(key, icon, new AsyncRelayCommand(action), textKey);

    /// <summary>A button that does something that takes a while, while a condition holds.</summary>
    private RibbonButtonViewModel Action(string key, string icon, Func<Task> action, Func<bool> canExecute, string? textKey = null)
    {
        var command = new AsyncRelayCommand(action, canExecute);
        _conditionalAsyncCommands.Add(command);
        return new RibbonButtonViewModel(key, icon, command, textKey);
    }

    private readonly List<AsyncRelayCommand> _conditionalAsyncCommands = [];

    /// <summary>A button whose function arrives with a later part of the port; for now it says so.</summary>
    private RibbonButtonViewModel Button(string key, string icon, string? textKey = null, Func<bool>? canExecute = null)
    {
        RibbonButtonViewModel? button = null;
        void Say() => StatusText = button!.ToolTipTitle + ": " + NotYetAvailable;
        RelayCommand command = canExecute == null ? new RelayCommand(Say) : new RelayCommand(Say, canExecute);
        if (canExecute != null)
        {
            _conditionalCommands.Add(command);
        }

        button = new RibbonButtonViewModel(key, icon, command, textKey);
        return button;
    }

    /// <summary>
    /// The button that adds a blank card, with the narrow button beside it that shows the
    /// blank colour and changes it. Original: <c>ImagenBlanca</c>.
    /// </summary>
    private RibbonButtonViewModel BlankButton()
    {
        _blankButton = new RibbonButtonViewModel("Form_Principal_Boton_AnadirImagenBlanca", "add-blank", new RelayCommand(() => ImagePanel.AddBlankCard()))
        {
            SwatchCommand = new AsyncRelayCommand(ChooseBlankColorAsync),
            Swatch = ImagePanel.BlankColor.ToBrush(),
        };
        return _blankButton;
    }

    private RibbonButtonViewModel? _blankButton;

    /// <summary>Asks for a colour, in the colour dialog; set by the main window. Gives null when cancelled.</summary>
    public Func<ArgbColor, Task<ArgbColor?>>? AskColor { get; set; }

    /// <summary>
    /// Changes the colour of the blank cards added from now on.
    /// Original: <c>Form_Principal_Boton_ColorImagenBlanca_Click</c>.
    /// </summary>
    public async Task ChooseBlankColorAsync()
    {
        if (AskColor == null)
        {
            return;
        }

        ArgbColor? color = await AskColor(ImagePanel.BlankColor);
        if (color != null)
        {
            ImagePanel.BlankColor = color.Value;
            _blankButton!.Swatch = color.Value.ToBrush();
        }
    }

    // ---- Settings of the project ----

    /// <summary>The settings of the project and the lists that choose its style. Original: <c>Control_ConfiguracionProyecto_Local</c>.</summary>
    public ProjectSettingsViewModel ProjectSettings { get; } = new();

    /// <summary>The back image shared by every card. Original: <c>Control_ImagenTrasera_Local</c>.</summary>
    public SharedBackViewModel SharedBack { get; } = new();

    /// <summary>
    /// Asks what to do with the images when the cards change between one face and two; set
    /// by the main window. The argument is whether they are going to show two faces. Gives
    /// null when the window is closed without an answer. Original: <c>Window_PreguntarCambioCara</c>.
    /// </summary>
    public Func<bool, Task<FaceChangeChoice?>>? AskFaceChange { get; set; }

    /// <summary>
    /// Shows the layout window; set by the main window. Gives null when cancelled.
    /// Original: <c>WindowSetupImpresion</c>.
    /// </summary>
    public Func<LayoutViewModel, Task<LayoutResult?>>? AskLayout { get; set; }

    /// <summary>
    /// Shows the window of the fronts or of the backs; set by the main window. Gives null
    /// when cancelled. Original: <c>WindowConfigurarCara</c>.
    /// </summary>
    public Func<FaceSetupViewModel, Task<FaceSetupResult?>>? AskFaceSetup { get; set; }

    /// <summary>
    /// Changes the settings of the fronts or of the backs in their window.
    /// Original: <c>Boton_Configurar_Cara_Frontal_Click</c>, which serves both buttons.
    /// </summary>
    public async Task EditFaceAsync(FaceType side)
    {
        if (_loading)
        {
            await Say(StringResources.Get("Espera_Cargar_Proyecto"));
            return;
        }

        FaceSetupResult? result = AskFaceSetup == null ? null : await AskFaceSetup(new FaceSetupViewModel(ProjectSettings.Settings, side));
        if (result != null)
        {
            ProjectSettings.ApplyFace(side, result.Face, result.BackOffset, result.BackRotation);
        }
    }

    /// <summary>
    /// Shows the header window; set by the main window. Gives null when cancelled.
    /// Original: <c>WindowSetupEncabezado</c>.
    /// </summary>
    public Func<HeaderFooterViewModel, Task<HeaderFooterResult?>>? AskHeaderFooter { get; set; }

    /// <summary>
    /// Changes the colour bar, registration marks, signature, page number and watermark in
    /// their window. Original: <c>Boton_Configurar_EncabezadoPie_Click</c>.
    /// </summary>
    public async Task EditHeaderFooterAsync()
    {
        if (_loading)
        {
            await Say(StringResources.Get("Espera_Cargar_Proyecto"));
            return;
        }

        ProjectSettings settings = ProjectSettings.Settings;
        HeaderFooterResult? result = AskHeaderFooter == null
            ? null
            : await AskHeaderFooter(new HeaderFooterViewModel(settings.HeaderFooter, settings.Watermark));
        if (result != null)
        {
            ProjectSettings.ApplyHeaderFooter(result.Header, result.Watermark);
        }
    }

    /// <summary>
    /// Changes paper, sides, card size, spacing and table in the layout window.
    /// Original: <c>Boton_Configurar_Disposicion_Click</c>.
    /// </summary>
    public async Task EditLayoutAsync()
    {
        Project project = CurrentProject();
        if (project.Settings.Group.Group == SettingsGroup.AdvancedStyles)
        {
            // The layout is the template's: only sides and backs are chosen.
            LayoutResult? sides = AskTemplateSides == null
                ? null
                : await AskTemplateSides(new LayoutViewModel(ProjectSettings.Settings), project.AdvancedTemplate);
            if (sides != null)
            {
                ProjectSettings.ApplySides(sides.Layout.DoubleSided, sides.Layout.BackImage, sides.DoublePanel);
            }

            return;
        }

        var statistics = new DeckStatistics(project.Deck, project.Settings.PrintLayout.BackImage);
        LayoutResult? result = AskLayout == null ? null : await AskLayout(new LayoutViewModel(ProjectSettings.Settings, statistics));
        if (result != null)
        {
            ProjectSettings.ApplyLayout(result.Layout, result.DoublePanel, result.Grid, result.LockCardSize);
        }
    }

    /// <summary>
    /// Shows the sides and backs of a project made with an advanced template, beside the
    /// template; set by the main window. Gives null when cancelled.
    /// Original: <c>WindowSetupPlantillasAvanzadas</c>.
    /// </summary>
    public Func<LayoutViewModel, AdvancedTemplate, Task<LayoutResult?>>? AskTemplateSides { get; set; }

    /// <summary>
    /// Shows the creator of custom decks; set by the main window. Gives the folder to make the
    /// deck in, or null when the window is just closed.
    /// </summary>
    public Func<CustomDeckViewModel, Task<string?>>? ShowCustomDeckCreator { get; set; }

    /// <summary>The same for the creator of sports decks.</summary>
    public Func<SportsDeckViewModel, Task<string?>>? ShowSportsDeckCreator { get; set; }

    /// <summary>
    /// Opens the creator of custom decks. When it asks for the deck to be made, its window
    /// closes, the images and the PDF are made and the window comes back as it was.
    /// Original: <c>ShellMenuBarItem_Herramientas_CreaMazoPersonalizado_Click</c>.
    /// </summary>
    public async Task CreateCustomDeckAsync()
    {
        if (ShowCustomDeckCreator == null)
        {
            return;
        }

        var creator = new CustomDeckViewModel { MakePdf = (project, folder) => CreateDeckPdfAsync(project, folder) };
        await RunCreatorAsync(creator, () => ShowCustomDeckCreator(creator), creator.CreateAsync, handler => creator.Notice += handler, handler => creator.Notice -= handler);
    }

    /// <summary>Original: <c>ShellMenuBarItem_Herramientas_CreaMazoPersonalizadoDeportes_Click</c>.</summary>
    public async Task CreateSportsDeckAsync()
    {
        if (ShowSportsDeckCreator == null)
        {
            return;
        }

        var creator = new SportsDeckViewModel { MakePdf = (project, folder) => CreateDeckPdfAsync(project, folder) };
        await RunCreatorAsync(creator, () => ShowSportsDeckCreator(creator), creator.CreateAsync, handler => creator.Notice += handler, handler => creator.Notice -= handler);
    }

    private async Task RunCreatorAsync(
        System.ComponentModel.INotifyPropertyChanged creator,
        Func<Task<string?>> show,
        Func<string, Task> create,
        Action<Action<string>> listen,
        Action<Action<string>> ignore)
    {
        void Tell(string message) => _ = ShowMessage?.Invoke(message);
        void Follow(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CustomDeckViewModel.Progress))
            {
                double share = sender is CustomDeckViewModel deck ? deck.Progress : ((SportsDeckViewModel)sender!).Progress;
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Progress = (int)share);
            }
        }

        while (true)
        {
            string? folder = await show();
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            // While its window is closed, what the creator has to say is said here.
            listen(Tell);
            creator.PropertyChanged += Follow;
            try
            {
                await create(folder);
            }
            finally
            {
                creator.PropertyChanged -= Follow;
                ignore(Tell);
                Progress = 0;
            }
        }
    }

    /// <summary>Shows the editor of advanced templates until it is closed; set by the main window.</summary>
    public Func<TemplateEditorViewModel, Task>? ShowTemplateEditor { get; set; }

    /// <summary>
    /// Opens the editor of advanced templates; the list of styles follows what was saved or
    /// deleted there. Original: <c>ShellMenuBarItem_Herramientas_PlantillasAvanzadas_Click</c>.
    /// </summary>
    public async Task EditTemplatesAsync()
    {
        if (ShowTemplateEditor == null)
        {
            return;
        }

        var editor = new TemplateEditorViewModel();
        editor.TemplatesChanged += (_, _) => ProjectSettings.RefreshStyles();
        await ShowTemplateEditor(editor);
    }

    /// <summary>Asks where to save a style; set by the main window. Gives null when cancelled.</summary>
    public Func<Task<string?>>? AskStyleFile { get; set; }

    /// <summary>The project as it stands: the cards of the panel with the settings. Original: <c>ModeloVista.ProyectoActual</c>.</summary>
    public Project CurrentProject()
    {
        var project = new Project { Deck = ImagePanel.ToDeck(), AdvancedTemplate = ProjectSettings.Project.AdvancedTemplate.Clone() };
        project.Settings.CopyFrom(ProjectSettings.Settings);
        project.Settings.SharedBack.CopyFrom(SharedBack.Face);
        return project;
    }

    /// <summary>Original: <c>Form_Setup_Boton_SalvarEstilo_Click</c>.</summary>
    public async Task SaveStyleAsync()
    {
        string? path = AskStyleFile == null ? null : await AskStyleFile();
        if (!string.IsNullOrEmpty(path))
        {
            ProjectSettings.SaveStyle(path);
        }
    }

    /// <summary>Original: <c>CambioConfiguracion</c> and <c>Mostrar_TraseraIgual</c>.</summary>
    private void OnSettingsChanged()
    {
        ImagePanel.BackgroundColorOffered = ProjectSettings.PaintsBackground;
        SharedBack.IsVisible = ProjectSettings.ShowsSharedBack;
        NotifyConditionalCommands();
    }

    /// <summary>
    /// The cards change between showing one face and showing two. With cards in the panel
    /// the user is asked what to do with the images, unless the project now has a back per
    /// card, where they become fronts (or are paired with the back they shared).
    /// Original: <c>CambioTipoPanel</c>.
    /// </summary>
    private async void OnPanelTypeChanged(bool hadSharedBack, ProjectSettings before)
    {
        bool toDouble = ProjectSettings.Settings.DoublePanel;
        FaceChangeChoice choice = FaceChangeChoice.Fronts;
        if (ImagePanel.PanelCount != 0)
        {
            if (toDouble && ProjectSettings.Settings.PrintLayout.BackImage == BackImageMode.DifferentBack)
            {
                choice = hadSharedBack ? FaceChangeChoice.Group : FaceChangeChoice.Fronts;
            }
            else
            {
                FaceChangeChoice? asked = AskFaceChange == null ? FaceChangeChoice.Fronts : await AskFaceChange(toDouble);
                choice = asked ?? FaceChangeChoice.Cancel;
            }
        }

        var after = new ProjectSettings();
        after.CopyFrom(ProjectSettings.Settings);
        var sharedBack = new CardFace();
        sharedBack.CopyFrom(SharedBack.Face);
        ImagePanel.ConvertPanels(
            toDouble,
            choice,
            hadSharedBack,
            sharedBack,
            ProjectSettings.Settings.BlankFace,
            afterUndo: () => ProjectSettings.Load(before),
            afterRedo: () => ProjectSettings.Load(after));
    }

    private static RibbonButtonViewModel Link(string key, string icon, string url) =>
        new(key, icon, new RelayCommand(() => Platform.Services.OpenUrl(url)));

    private static RibbonGroupViewModel Group(params RibbonButtonViewModel[] buttons) => new(buttons);

    /// <summary>Shown when a button without function yet is pressed. Not translated: it goes away as the port advances.</summary>
    private const string NotYetAvailable = "not available yet in this version";
}

/// <summary>What the add-images window was answered: the way chosen and whether to keep it for the next times.</summary>
public sealed record AddImagesAnswer(AddImagesResult Choice, bool Remember);
