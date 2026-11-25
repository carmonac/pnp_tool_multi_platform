using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PnpTool.App.ViewModels.Panel;
using PnpTool.App.ViewModels.Ribbon;
using PnpTool.Core.Cards;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.ViewModels;

/// <summary>
/// The main window: the command bar with its tabs, the quick-access buttons and the status
/// line. Original: <c>Window_Principal_v2</c> and the ribbon controls under <c>Controls/Ribbon</c>.
/// </summary>
/// <remarks>
/// Left out of the command bar: the button that sent the PDF to a printing company, the
/// Windows-only helper programs (nanDECK, PosteRazor, screen capture) and the developer's
/// variable viewer.
/// </remarks>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
    {
        ImagePanel.CountsChanged += (_, _) => CountsText = ImagePanel.CountsText;
        ImagePanel.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(ImagePanelViewModel.CanSwapFaces))
            {
                foreach (RelayCommand command in _conditionalCommands)
                {
                    command.NotifyCanExecuteChanged();
                }
            }
        };

        RibbonGroupViewModel output = Group(Button("Form_Principal_Boton_Preview", "preview"), Button("Form_Principal_Boton_CrearFichero", "create"));

        Tabs =
        [
            new RibbonTabViewModel(
                "ShellMenuBarItem_File",
                Group(
                    Button("Form_Principal_Boton_Nuevo", "new"),
                    Button("Form_Principal_Boton_Abrir", "open"),
                    Button("Form_Principal_Boton_Guardar", "save"),
                    Button("ShellMenuItem_File_GuardarComo", "save-as")),
                Group(Button("ShellMenuBarItem_Herramientas_EmpacarProyecto", "pack")),
                output),
            new RibbonTabViewModel(
                "ShellMenuBarItem_Acciones",
                Group(
                    Action("Form_Principal_Boton_AnadirImagen", "add-image", AddImagesAsync),
                    Action("Form_Principal_Boton_AnadirImagenBlanca", "add-blank", () => ImagePanel.AddBlankCard()),
                    Action("Form_Principal_Boton_AnadirImagenVacia", "add-empty", () => ImagePanel.AddEmptyCard())),
                Group(
                    Button("Form_Principal_Boton_CambiarImagenes_Frontales", "fronts", "Form_Principal_Boton_AnadirImagenEnBloque"),
                    Button("Form_Principal_Boton_CambiarImagenes_Traseras", "backs", "Form_Principal_Boton_AnadirImagenEnBloque"),
                    Action("Form_Principal_Boton_IntercambiarImagenes", "swap-faces", () => ImagePanel.SwapFaces(), () => ImagePanel.CanSwapFaces),
                    Action("Form_Principal_Boton_RefreshPanels", "refresh", () => ImagePanel.RefreshImages())),
                Group(
                    Action("Form_Principal_Boton_Eliminar", "delete", () => ImagePanel.DeleteSelected()),
                    Action("Form_Principal_Boton_BorrarSeleccion", "deselect", () => ImagePanel.ClearSelection()),
                    Action("Form_Principal_Boton_SeleccionarTodos", "select-all", SelectAll)),
                output),
            new RibbonTabViewModel(
                "ShellMenuBarItem_Configuracion",
                Group(
                    Button("Form_Setup_Boton_SalvarEstilo", "save-as"),
                    Button("Boton_Configurar_Disposicion", "layout"),
                    Button("Boton_Configurar_Cara_Frontal", "fronts"),
                    Button("Boton_Configurar_Cara_Trasera", "backs"),
                    Button("Boton_Configurar_EncabezadoPie", "header-footer"),
                    Button("Boton_Configurar_OtrosParametros", "settings"),
                    Button("Boton_Configurar_Libros", "book")),
                output),
            new RibbonTabViewModel(
                "ShellMenuBarItem_Herramientas",
                Group(
                    Button("ShellMenuBarItem_Herramientas_Extraer", "extract"),
                    Button("ShellMenuBarItem_Herramientas_ProcesarCarpeta", "open"),
                    Button("ShellMenuBarItem_Herramientas_Unir", "merge"),
                    Button("ShellMenuBarItem_Herramientas_ModificarImagenes", "edit"),
                    Button("ShellMenuBarItem_Herramientas_CortarImagen", "cut"),
                    Button("ShellMenuBarItem_Herramientas_Separar", "split"),
                    Button("ShellMenuBarItem_Herramientas_HacerPortadas", "book"),
                    Button("ShellMenuBarItem_Herramientas_ModificarPDF", "modify-pdf"),
                    Button("ShellMenuBarItem_Herramientas_PlantillasAvanzadas", "template"),
                    Button("ShellMenuBarItem_Herramientas_ImportarPDFCartas", "import-pdf"),
                    Button("ShellMenuBarItem_Herramientas_Scryfall", "card-search")),
                Group(
                    Button("ShellMenuBarItem_Herramientas_CreaMazoPersonalizado", "deck"),
                    Button("ShellMenuBarItem_Herramientas_CreaMazoPersonalizadoDeportes", "sports-deck")),
                Group(
                    Link("ShellMenuBarItem_Herramientas_TemplateMaker", "template-maker", AppLinks.TemplateMaker),
                    Link("TemplatemakerBoardToolStripMenuItem", "board", AppLinks.BoardTemplates))),
            new RibbonTabViewModel(
                "ShellMenuBarItem_Ayuda",
                Group(
                    new RibbonButtonViewModel("ShellMenuBarItem_Ayuda_Foro", "forum", new RelayCommand(() => Platform.Services.OpenUrl(StringResources.Get("url_foro")))),
                    Link("ShellMenuBarItem_Ayuda_Sleeve", "sleeves", AppLinks.Sleeves),
                    Link("ShellMenuBarItem_Ayuda_BGGGuiaCartas", "bgg", AppLinks.SleeveSizeGuide),
                    Link("ShellMenuBarItem_Ayuda_Youtube", "youtube", AppLinks.YouTube))),
        ];

        QuickAccess =
        [
            Button("QATButton_Preview", "preview", "Form_Principal_Boton_Preview"),
            Button("QATButton_Crear", "create", "Form_Principal_Boton_CrearFichero"),
            Button("QATButton_Abrir", "open", "Form_Principal_Boton_Abrir"),
            Button("QATButton_Guardar", "save", "Form_Principal_Boton_Guardar"),
            Button("QATButton_Undo", "undo"),
        ];

        // The original opens on the second tab, the one with the image actions.
        SelectedTab = Tabs[1];
        CountsText = ImagePanel.CountsText;
    }

    /// <summary>The cards of the project. Original: <c>PanelImagenesPrincipal</c>.</summary>
    public ImagePanelViewModel ImagePanel { get; } = new();

    /// <summary>
    /// Asks the user for image files; set by the window. Gives no files when the user
    /// cancels. Original: <c>Dialogos.DialogoSeleccionarFicherosImagenes</c>.
    /// </summary>
    public Func<Task<IReadOnlyList<string>>>? PickImages { get; set; }

    /// <summary>Shows a message the user has to read; set by the window. Original: <c>Dialogos.DialogoError</c>.</summary>
    public Func<string, Task>? ShowMessage { get; set; }

    public string Title { get; } = AppInfo.Name;

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
    /// Asks for image files and adds a card for each. Original:
    /// <c>Form_Principal_Boton_AnadirImagen_Click</c> and <c>AnadirImagenNormal</c>.
    /// </summary>
    public async Task AddImagesAsync()
    {
        if (PickImages == null)
        {
            return;
        }

        IReadOnlyList<string> files = await PickImages();
        if (files.Count == 0)
        {
            return;
        }

        var progress = new Progress<double>(value => Progress = value);
        (_, AppError error) = await ImagePanel.AddImagesAsync(files, -1, progress);
        Progress = 0;
        if ((error.HasError || error.HasWarning) && ShowMessage != null)
        {
            string title = error.HasWarning ? StringResources.Get("Imagen_no_encontrada") + Environment.NewLine : string.Empty;
            await ShowMessage(title + error.Message);
        }
    }

    /// <summary>
    /// Selects every card. The original first asks, in a window that comes with a later
    /// task, for a range and for whether to select cards, faces or both.
    /// </summary>
    private void SelectAll() => ImagePanel.Select(0, ImagePanel.PanelCount, cards: true, FaceType.None);

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

    private static RibbonButtonViewModel Action(string key, string icon, Func<Task> action) =>
        new(key, icon, new AsyncRelayCommand(action));

    /// <summary>A button whose function arrives with a later part of the port; for now it says so.</summary>
    private RibbonButtonViewModel Button(string key, string icon, string? textKey = null)
    {
        RibbonButtonViewModel? button = null;
        ICommand command = new RelayCommand(() => StatusText = button!.ToolTipTitle + ": " + NotYetAvailable);
        button = new RibbonButtonViewModel(key, icon, command, textKey);
        return button;
    }

    private static RibbonButtonViewModel Link(string key, string icon, string url) =>
        new(key, icon, new RelayCommand(() => Platform.Services.OpenUrl(url)));

    private static RibbonGroupViewModel Group(params RibbonButtonViewModel[] buttons) => new(buttons);

    /// <summary>Shown when a button without function yet is pressed. Not translated: it goes away as the port advances.</summary>
    private const string NotYetAvailable = "not available yet in this version";
}
