using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Online;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Core.Templates;
using PnpTool.Imaging;
using PnpTool.Pdf.Layout;

namespace PnpTool.App.ViewModels;

/// <summary>A recently used project file, as the list shows it.</summary>
public sealed record RecentFileItem(string Path)
{
    public override string ToString() => RecentFiles.DisplayText(Path);
}

/// <summary>What the file tab shows before its buttons: the list of recent files of the window.</summary>
public sealed record RecentFilesList(MainWindowViewModel Owner);

/// <summary>
/// The project as a file: new, open, save, the list of recent files, the copy kept in case
/// the program ends unexpectedly, and making the PDF. Original: the file handling of
/// <c>Window_Principal_v2</c>, <c>Control_Comandos</c>, <c>Ficheros</c> and
/// <c>FormPrincipalModeloLocal.BotonCrearFicheroSalida</c>.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private readonly RecentFiles _recent = new();
    private bool _loading;

    // ---- What the window provides ----

    /// <summary>Asks a yes-or-no question, with cancel as a third answer when the flag says so. Gives null for cancel.</summary>
    public Func<string, bool, Task<bool?>>? Ask { get; set; }

    /// <summary>Asks which project or style file to open; null when cancelled.</summary>
    public Func<Task<string?>>? AskFileToOpen { get; set; }

    /// <summary>Asks where to save the project; null when cancelled.</summary>
    public Func<Task<string?>>? AskProjectToSave { get; set; }

    /// <summary>Asks where to save the PDF, given a suggested name; null when cancelled.</summary>
    public Func<string, Task<string?>>? AskPdfToSave { get; set; }

    /// <summary>
    /// Runs a long task showing its progress and letting the user stop it. Gives what went
    /// wrong, or null. Without a window, as in a test, the task simply runs.
    /// </summary>
    public Func<string, Func<Action<int>, CancellationToken, Task>, Task<Exception?>>? RunWithProgress { get; set; }

    /// <summary>Makes what looks for newer versions; replaced in tests, which never use the network.</summary>
    public Func<UpdateChecker> NewUpdateChecker { get; set; } = () => new UpdateChecker();

    // ---- State ----

    /// <summary>The copy of the project kept while it has unsaved changes. Original: <c>Ficheros.FicheroTemporal</c>.</summary>
    public static string TemporaryProjectFile => Path.Combine(AppPaths.AppTempFolder, "tempro.pnp");

    /// <summary>The project file in hand, or empty. Original: <c>ConfiguracionPrograma.FicheroPNPAbierto</c>.</summary>
    public string OpenFile
    {
        get => AppSettings.OpenProjectFile;
        private set
        {
            AppSettings.OpenProjectFile = value;
            UpdateTitle();
        }
    }

    /// <summary>Whether the project has changes that are not saved. Original: <c>ProyectoHaCambiado</c>.</summary>
    [ObservableProperty]
    private bool _hasChanged;

    /// <summary>The title of the main window. Original: <c>ActualizaBarraNombrePrograma</c>.</summary>
    [ObservableProperty]
    private string _windowTitle = AppInfo.Name;

    /// <summary>The recent project files, newest first. Original: <c>GaleriaUltimosFicheros</c>.</summary>
    [ObservableProperty]
    private IReadOnlyList<RecentFileItem> _recentFiles = [];

    /// <summary>Picking a recent file opens it. Original: <c>OnClick_UltimoFichero</c>.</summary>
    [ObservableProperty]
    private RecentFileItem? _selectedRecentFile;

    /// <summary>The text above the list of recent files.</summary>
    public static string RecentFilesLabel => StringResources.Get("Form_NuevoAbrir_Label");

    partial void OnHasChangedChanged(bool value) => UpdateTitle();

    partial void OnSelectedRecentFileChanged(RecentFileItem? value)
    {
        if (value != null)
        {
            _ = OpenAsync(value.Path);
            SelectedRecentFile = null;
        }
    }

    private void UpdateTitle() =>
        WindowTitle = AppInfo.Name + " " + AppInfo.Version
            + (string.IsNullOrEmpty(OpenFile) ? " - Unsaved" : " - " + OpenFile)
            + (HasChanged ? " *" : string.Empty);

    private void LoadRecentFiles()
    {
        _recent.Load();
        RecentFiles = [.. _recent.Files.Where(file => !string.IsNullOrEmpty(file)).Select(file => new RecentFileItem(file))];
    }

    private void Remember(string file)
    {
        if (_recent.Add(file))
        {
            LoadRecentFiles();
        }
    }

    /// <summary>
    /// Something in the project changed: it is marked as changed and a copy is saved, to be
    /// offered if the program ends unexpectedly. Original: <c>ProyectoCambia</c>.
    /// </summary>
    private void MarkChanged()
    {
        if (_loading)
        {
            return;
        }

        HasChanged = true;
        SaveTemporaryCopy();
    }

    private void SaveTemporaryCopy()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.AppTempFolder);
            ProjectFileStore.SaveProject(TemporaryProjectFile, CurrentProject());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Log.Write("Temporary project not saved: " + e.Message, force: true);
        }
    }

    // ---- Starting and closing ----

    /// <summary>
    /// What the program does when its window first shows: opens the file given on the command
    /// line, or offers the copy left by a run that ended unexpectedly, or starts a custom
    /// project with the settings of the standard card style.
    /// Original: <c>Window_OnContentRendered</c> and <c>Ficheros.ProgramaMalCerrado</c>.
    /// </summary>
    public async Task StartAsync(string? fileFromCommandLine = null)
    {
        LoadRecentFiles();
        if (!string.IsNullOrEmpty(fileFromCommandLine))
        {
            await OpenAsync(fileFromCommandLine);
            return;
        }

        if (File.Exists(TemporaryProjectFile))
        {
            bool? recover = Ask == null ? false : await Ask(StringResources.Get("Proyecto_Temporal"), true);
            if (recover == true && await OpenProjectFileAsync(TemporaryProjectFile))
            {
                // Still unsaved work: the copy is kept until it is saved or discarded.
                HasChanged = true;
                SaveTemporaryCopy();
                return;
            }

            DeleteTemporaryProject();
        }

        StartCustomProject();
    }

    /// <summary>Original: <c>AbrirFicheroEstiloPersonalizado</c>.</summary>
    private void StartCustomProject()
    {
        _loading = true;
        var settings = new ProjectSettings();
        AppError error = ProjectFileStore.LoadStyle(StyleCatalog.PathOf(SettingsGroup.Cards, "Cartas_Standar.pnps"), ref settings).Error;
        if (!error.HasError)
        {
            settings.Group.Group = SettingsGroup.Custom;
            settings.Group.SubgroupIndex = 0;
            ProjectSettings.Load(settings);
        }

        _loading = false;
        HasChanged = false;
        UpdateTitle();
    }

    /// <summary>
    /// Asks about unsaved changes before the window closes. Gives false when the user cancels,
    /// and the window must then stay open. Original: <c>Window_OnClosing</c> and <c>Ficheros.CierraPrograma</c>.
    /// </summary>
    public async Task<bool> CloseAsync()
    {
        if (await KeepsUnsavedChangesAsync())
        {
            return false;
        }

        DeleteTemporaryProject();
        OpenFile = string.Empty;
        return true;
    }

    private static void DeleteTemporaryProject()
    {
        try
        {
            File.Delete(TemporaryProjectFile);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write("Temporary project not deleted: " + e.Message, force: true);
        }
    }

    /// <summary>
    /// With unsaved changes, asks whether to save them. True when the user cancelled, or
    /// chose to save and the saving did not happen, so that what was going to replace the
    /// project must not go on. Original: <c>ComprobarSiHaCambiado</c>.
    /// </summary>
    private async Task<bool> KeepsUnsavedChangesAsync()
    {
        if (!HasChanged || Ask == null)
        {
            return false;
        }

        bool? answer = await Ask(StringResources.Get("proyecto_ha_cambiado"), true);
        return answer switch
        {
            true => !await SaveAsync(askPath: false),
            null => true,
            _ => false,
        };
    }

    // ---- New, open, save ----

    /// <summary>
    /// Empties the project, after asking about unsaved changes. Gives false when the user
    /// cancelled. The settings stay as they are. Original: <c>NuevoToolStripMenuItem_ClickFunction</c>.
    /// </summary>
    public async Task<bool> NewAsync()
    {
        if (_loading)
        {
            await Say(StringResources.Get("Espera_Cargar_Proyecto"));
            return false;
        }

        if (await KeepsUnsavedChangesAsync())
        {
            return false;
        }

        _loading = true;
        ImagePanel.Clear();
        SharedBack.Clear();
        ProjectSettings.Settings.SharedBack.ResetAsSharedBack();
        _loading = false;
        OpenFile = string.Empty;
        HasChanged = false;
        DeleteTemporaryProject();
        return true;
    }

    /// <summary>
    /// Looks for a newer version, as the setting of update notifications says, and offers to
    /// open its page. Says nothing when there is none or the look fails. Nothing is downloaded
    /// or installed. Original: <c>Actualizaciones.DescargaActualizacion</c>, at startup.
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        using UpdateChecker checker = NewUpdateChecker();
        UpdateCheckResult result = await checker.CheckAsync(AppSettings.UpdateNotifications, AppInfo.Version);
        if (result.Status == UpdateCheckStatus.Failed)
        {
            Log.Write("Update check: " + result.Message);
        }

        if (result is { Status: UpdateCheckStatus.Newer, Release: { } release } && Ask != null)
        {
            string question = StringResources.Get("Actualizacion_Disponible") + Environment.NewLine + AppInfo.Version + " → " + release.Version;
            if (await Ask(question, false) == true)
            {
                Platform.Services.OpenUrl(release.Url);
            }
        }

        return result;
    }

    /// <summary>The system's dialog to pick a project of PdfCartas (a ".Bsk" file).</summary>
    public Func<Task<string?>>? AskPdfCartasFile { get; set; }

    /// <summary>What the original says before it imports a project of PdfCartas; it has these words in no other language.</summary>
    public const string ImportWarning = "Se hará lo que se pueda. No somos responsables de nada";

    /// <summary>Original: the text shown by <c>ImportarPDFCartas_ClickAccion</c> with the path of the new project.</summary>
    public const string ImportDone = "Fichero transformado a PNP Tool -> ";

    public const string ImportAskToOpen = "¿Cargar proyecto?";

    /// <summary>
    /// Turns a project of PdfCartas into a PNP Tool project beside it and offers to open it.
    /// Gives the path of the new project, or null. Original:
    /// <c>ShellMenuBarItem_Herramientas_ImportarPDFCartas_Click</c>.
    /// </summary>
    public async Task<string?> ImportPdfCartasAsync()
    {
        await Say(ImportWarning);
        string? file = AskPdfCartasFile == null ? null : await AskPdfCartasFile();
        if (string.IsNullOrEmpty(file))
        {
            return null;
        }

        AppError result = await Task.Run(() => PdfCartasImporter.Import(file));
        if (result.HasError)
        {
            await Say(result.Message);
            return null;
        }

        await Say(ImportDone + result.Message);
        if (Ask != null && await Ask(ImportAskToOpen, false) == true)
        {
            await OpenAsync(result.Message);
        }

        return result.Message;
    }

    /// <summary>What the original answers when given a ticket project. Original: <c>Window_Principal_v2.AbrirProyecto</c>.</summary>
    public const string ObsoleteFeature = "Funcionalidad Obsoleta";

    /// <summary>
    /// Opens a file: a project, or a style or advanced template, whose settings replace those
    /// of the project. Without a path the user is asked for the file.
    /// Original: <c>AbrirProyecto</c>.
    /// </summary>
    public async Task<bool> OpenAsync(string? path = null)
    {
        if (string.IsNullOrEmpty(path))
        {
            path = AskFileToOpen == null ? null : await AskFileToOpen();
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }
        }

        switch (Path.GetExtension(path))
        {
            case ".pnp":
                return await OpenProjectFileAsync(path);
            case ".pnps":
            case ".pnpa":
                return await OpenStyleFileAsync(path);
            case ".pnpt":
                // Ticket projects: the original no longer opens them either, and says so with
                // these words (it asks for a text of this name, which no language has).
                await Say(ObsoleteFeature);
                return false;
            default:
                await Say(Path.GetFileName(path) + ": " + StringResources.Get("selecciona_fichero_pnp"));
                return false;
        }
    }

    /// <summary>Original: <c>AbrirProyectoPNP</c> and <c>CrearPaneles_DesdeMazo</c>.</summary>
    private async Task<bool> OpenProjectFileAsync(string path)
    {
        AppError error;
        Project project;
        try
        {
            (error, project) = await Task.Run(() => ProjectFileStore.LoadProject(path));
        }
        catch (Exception e) when (e is System.Xml.XmlException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            // A file that is not a project at all.
            (error, project) = (new AppError { Kind = ErrorKind.Error, Message = Path.GetFileName(path) + ": " + e.Message }, new Project());
        }

        if (error.HasError)
        {
            await Say(error.Message);
            return false;
        }

        if (!await NewAsync())
        {
            return false;
        }

        _loading = true;
        try
        {
            bool isTemporary = path == TemporaryProjectFile;
            if (!isTemporary)
            {
                Remember(path);
                AppSettings.LastFolder = Path.GetDirectoryName(path) ?? string.Empty;
            }

            ProjectSettings.Load(project.Settings, project.AdvancedTemplate);
            ImagePanel.IsDouble = project.Settings.DoublePanel;
            SharedBack.Load(project.Settings.SharedBack);
            foreach (Card card in project.Deck.Cards)
            {
                // The project is shown with the images it names, not with copies made for an earlier PDF.
                foreach (CardFace face in new[] { card.Front, card.Back })
                {
                    if (!string.IsNullOrEmpty(face.OriginalPath))
                    {
                        face.Path = face.OriginalPath;
                    }
                }
            }

            ImagePanel.AddCards(project.Deck.Cards);
            ImagePanel.History.Clear();
            OnSettingsChanged();
            OpenFile = isTemporary ? string.Empty : path;
            HasChanged = false;

            string[] missing =
            [
                .. project.Deck.Cards
                    .SelectMany((card, index) => new[] { (index, card.Front) }.Concat(project.Settings.DoublePanel ? [(index, card.Back)] : []))
                    .Where(entry => entry.Item2.ImageKind == ImageKind.Normal && !File.Exists(entry.Item2.Path))
                    .Select(entry => "#" + (entry.index + 1) + " " + entry.Item2.Path),
            ];
            if (missing.Length != 0)
            {
                await Say(StringResources.Get("Imagen_no_encontrada") + Environment.NewLine + string.Join(Environment.NewLine, missing));
            }
        }
        finally
        {
            _loading = false;
        }

        return true;
    }

    /// <summary>Original: <c>AbrirFicheroEstilo</c>.</summary>
    private async Task<bool> OpenStyleFileAsync(string path)
    {
        var read = new Project();
        AppError error;
        if (Path.GetExtension(path) == ".pnpa")
        {
            error = AdvancedTemplate.LoadInto(path, read);
        }
        else
        {
            ProjectSettings settings = read.Settings;
            error = ProjectFileStore.LoadStyle(path, ref settings).Error;
            read.Settings = settings;
        }

        if (error.HasError)
        {
            await Say(error.Message);
            return false;
        }

        if (!await NewAsync())
        {
            return false;
        }

        ProjectSettings.Load(read.Settings, read.AdvancedTemplate);
        SharedBack.Load(read.Settings.SharedBack);
        ImagePanel.IsDouble = read.Settings.DoublePanel;
        return true;
    }

    /// <summary>
    /// Saves the project, asking where when it has no file yet or when asked to. Gives false
    /// when nothing was saved. Original: <c>Guardar</c> and <c>Ficheros.Guardar</c>.
    /// </summary>
    public async Task<bool> SaveAsync(bool askPath)
    {
        string path = OpenFile;
        if (askPath || string.IsNullOrEmpty(path))
        {
            string? chosen = AskProjectToSave == null ? null : await AskProjectToSave();
            if (string.IsNullOrEmpty(chosen))
            {
                return false;
            }

            path = chosen;
        }

        try
        {
            if (File.Exists(path) && new FileInfo(path).IsReadOnly)
            {
                await Say(path + ": read only");
                return false;
            }

            ProjectFileStore.SaveProject(path, CurrentProject());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await Say(e.Message);
            return false;
        }

        Remember(path);
        OpenFile = path;
        HasChanged = false;
        DeleteTemporaryProject();
        return true;
    }

    // ---- Making the PDF ----

    /// <summary>
    /// Makes the PDF of the project, or only its first sheet as a preview, and opens it.
    /// Original: <c>BotonCrearFicheroSalida</c> in the window and in <c>FormPrincipalModeloLocal</c>,
    /// and <c>BotonCrearSalidaDefecto</c>.
    /// </summary>
    public async Task<PdfOutput?> CreatePdfAsync(bool preview)
    {
        if (_loading)
        {
            await Say(StringResources.Get("Espera_Cargar_Proyecto"));
            return null;
        }

        string name = string.IsNullOrEmpty(OpenFile)
            ? FileNames.DocumentId(ProjectSettings.Settings.Other.FileIdSeed)
            : Path.GetFileNameWithoutExtension(OpenFile) + ".pdf";
        Project project = CurrentProject();
        if (project.Settings.Other.StackedLayout.LayoutType != LayoutType.Standard)
        {
            // Sheets that will be piled up and cut together take the cards in another order.
            PrintLayout layout = project.Settings.PrintLayout;
            project.Deck = DeckStacking.Arrange(project.Deck.Cards, project.Settings.Other.StackedLayout, layout.Columns, layout.Rows);
        }

        return await MakePdfAsync(project, preview, name, ImagePanel.SizeOnDiskMb());
    }

    /// <summary>
    /// Makes the PDF of the project of a custom deck, asking where to save it, and leaves the
    /// project beside the images of its cards. Original: the end of <c>Procesar</c> in the
    /// two windows that create decks.
    /// </summary>
    public async Task<PdfOutput?> CreateDeckPdfAsync(Project project, string folder)
    {
        PdfOutput? output = await MakePdfAsync(project, preview: false, Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)) + ".pdf", 0.0);
        try
        {
            ProjectFileStore.SaveProject(Path.Combine(folder, PnpTool.Imaging.CustomDeckProjects.ProjectFileName), project);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await Say(exception.Message);
        }

        return output;
    }

    // ---- Tools that work on images and folders ----

    /// <summary>Asks for a folder; set by the main window. Gives null when cancelled.</summary>
    public Func<Task<string?>>? AskFolder { get; set; }

    /// <summary>Shows the tool that cuts images until it is closed; set by the main window.</summary>
    public Func<CutImagesViewModel, Task>? ShowCutImages { get; set; }

    /// <summary>Shows the tool that changes images until it is closed; set by the main window.</summary>
    public Func<EditImagesViewModel, Task>? ShowEditImages { get; set; }

    /// <summary>Shows the window that packs the project until it is closed; set by the main window.</summary>
    public Func<PackProjectViewModel, Task>? ShowPackProject { get; set; }

    /// <summary>Shows the list of folders to process; set by the main window. Gives true to go on.</summary>
    public Func<ProcessFoldersViewModel, Task<bool?>>? ShowProcessFolders { get; set; }

    /// <summary>Shows the tool that takes images out of PDFs; set by the main window. Gives the images to bring to the panel, or null.</summary>
    public Func<ExtractPdfViewModel, Task<List<string>?>>? ShowExtractPdf { get; set; }

    /// <summary>Shows the tool that joins PDFs until it is closed; set by the main window.</summary>
    public Func<MergePdfViewModel, Task>? ShowMergePdf { get; set; }

    /// <summary>Shows the tool that sorts fronts and backs until it is closed; set by the main window.</summary>
    public Func<SplitFrontsBacksViewModel, Task>? ShowSplitFrontsBacks { get; set; }

    /// <summary>Shows the tool that changes a PDF until it is closed; set by the main window.</summary>
    public Func<ModifyPdfViewModel, Task>? ShowModifyPdf { get; set; }

    /// <summary>Shows the tool that makes book covers until it is closed; set by the main window.</summary>
    public Func<BookCoverViewModel, Task>? ShowBookCover { get; set; }

    /// <summary>
    /// Takes images out of PDFs and, when asked to, adds them to the panel as cards.
    /// Original: <c>ExtraeImagenesDeUnPDFToolStripMenuItem_Click</c>.
    /// </summary>
    public async Task ExtractPdfAsync()
    {
        if (ShowExtractPdf == null)
        {
            return;
        }

        List<string>? images = await ShowExtractPdf(new ExtractPdfViewModel());
        if (images is { Count: > 0 })
        {
            await ImagePanel.AddImagesAsync([.. images.Where(PnpTool.Imaging.ImageFormats.IsImage)]);
        }
    }

    /// <summary>
    /// Key of the button that opens the credits and terms. The Windows program has no such
    /// button: it shows those pages inside its settings window. The key has no translated
    /// text, so the button is labelled with the name of the program.
    /// </summary>
    public const string AboutButtonKey = "ShellMenuBarItem_Ayuda_AcercaDe";

    /// <summary>Shows the credits and terms until the window is closed; set by the main window.</summary>
    public Func<AboutViewModel, Task>? ShowAbout { get; set; }

    /// <summary>Opens the credits, the terms and the links to support the program.</summary>
    public Task AboutAsync() => ShowAbout == null ? Task.CompletedTask : ShowAbout(new AboutViewModel());

    /// <summary>Shows the search of cards until it is closed; set by the main window.</summary>
    public Func<CardSearchViewModel, Task>? ShowCardSearch { get; set; }

    /// <summary>
    /// Opens the search of Magic cards and, when asked to, adds the images downloaded to the
    /// panel, each with the copies its deck list asked for.
    /// Original: <c>ShellMenuBarItem_Herramientas_Scryfall_Click</c>, which added every card once.
    /// </summary>
    public async Task SearchCardsAsync()
    {
        if (ShowCardSearch == null)
        {
            return;
        }

        var search = new CardSearchViewModel();
        await ShowCardSearch(search);
        await ImportCardsAsync(search.ToImport);
    }

    /// <summary>Adds downloaded cards at the end of the panel with their copies.</summary>
    public async Task ImportCardsAsync(IReadOnlyList<PnpTool.Core.Online.CardDownload> cards)
    {
        if (cards.Count == 0)
        {
            return;
        }

        int first = ImagePanel.Cards.Count;
        (int added, _) = await ImagePanel.AddImagesAsync([.. cards.Select(card => card.File)]);
        if (added != cards.Count || ImagePanel.IsDouble)
        {
            return;     // some image was not taken, or images went in pairs: copies cannot be matched
        }

        ImagePanel.ClearCardSelection();    // copies set on a card also go to the selected ones
        for (int index = 0; index < cards.Count; index++)
        {
            if (cards[index].Quantity > 1)
            {
                ImagePanel.SetCopies(first + index, cards[index].Quantity);
            }
        }
    }

    /// <summary>Original: <c>UnirPDFsToolStripMenuItem_Click</c>.</summary>
    public Task MergePdfAsync() => ShowMergePdf?.Invoke(new MergePdfViewModel()) ?? Task.CompletedTask;

    /// <summary>Original: <c>SepararFrontalesTraserasToolStripMenuItem_Click</c>.</summary>
    public Task SplitFrontsBacksAsync() => ShowSplitFrontsBacks?.Invoke(new SplitFrontsBacksViewModel()) ?? Task.CompletedTask;

    /// <summary>Original: <c>ShellMenuBarItem_Herramientas_ModificarPDF_SelectionChanged</c>.</summary>
    public Task ModifyPdfAsync() => ShowModifyPdf?.Invoke(new ModifyPdfViewModel()) ?? Task.CompletedTask;

    /// <summary>
    /// Key of the button of the poster tool. In the Windows program the button is on the tab
    /// of external tools, starts PosteRazor and carries that program's name, which is not
    /// used here: the button is labelled with a word that reads the same in most languages.
    /// </summary>
    public const string PosterButtonKey = "ShellMenuBarItem_Herramientas_Poster";

    public const string PosterButtonText = "Poster";

    /// <summary>Shows the poster tool until it is closed; set by the main window.</summary>
    public Func<PosterViewModel, Task>? ShowPoster { get; set; }

    /// <summary>Opens the tool that splits a picture over several sheets. Original: <c>PosterazorToolStripMenuItem_Click</c>.</summary>
    public Task PosterAsync() => ShowPoster?.Invoke(new PosterViewModel()) ?? Task.CompletedTask;

    /// <summary>
    /// Starts the calculator of the system, or says in the status line that there is none.
    /// Original: <c>CalculadoraToolStripMenuItem_Click</c>.
    /// </summary>
    public void OpenCalculator()
    {
        if (!Platform.Services.OpenCalculator())
        {
            StatusText = StringResources.Get("ShellMenuBarItem_Herramientas_Calculadora") + ": " + NoCalculator;
        }
    }

    /// <summary>Not translated: the original has no text for it, as its calculator is always there.</summary>
    public const string NoCalculator = "no calculator program was found";

    /// <summary>Shows the tool that takes pictures of the screen until it is closed; set by the main window.</summary>
    public Func<ScreenCaptureViewModel, Task>? ShowScreenCapture { get; set; }

    /// <summary>
    /// Opens the tool that takes pictures of the screen and, when asked to, adds them to the
    /// panel as cards. Original: <c>AutoCapturaToolStripMenuItem_Click</c>, which started the
    /// separate program.
    /// </summary>
    public async Task ScreenCaptureAsync()
    {
        if (ShowScreenCapture == null)
        {
            return;
        }

        var capture = new ScreenCaptureViewModel();
        await ShowScreenCapture(capture);
        if (capture.ToImport.Count > 0)
        {
            await ImagePanel.AddImagesAsync([.. capture.ToImport]);
        }
    }

    /// <summary>Original: <c>ShellMenuBarItem_Herramientas_HacerPortadas_Click</c>.</summary>
    public Task BookCoverAsync() => ShowBookCover?.Invoke(new BookCoverViewModel()) ?? Task.CompletedTask;

    /// <summary>Original: <c>CortarImagenToolStripMenuItem_Click</c>.</summary>
    public async Task CutImagesAsync()
    {
        if (ShowCutImages != null)
        {
            await ShowCutImages(new CutImagesViewModel());
        }
    }

    /// <summary>Original: <c>ModificarImagenesToolStripMenuItem_Click</c>.</summary>
    public async Task EditImagesAsync()
    {
        if (ShowEditImages != null)
        {
            await ShowEditImages(new EditImagesViewModel(ProjectSettings.Settings));
        }
    }

    /// <summary>Packs the project as it stands, saved or not. Original: <c>EmpacarProyecto_Click</c>.</summary>
    public async Task PackProjectAsync()
    {
        if (ShowPackProject != null)
        {
            await ShowPackProject(new PackProjectViewModel(CurrentProject(), OpenFile));
        }
    }

    /// <summary>
    /// Makes PDFs straight from folders of images with the settings of the project: one PDF
    /// of all the folders, or a PDF a folder, named after it. The cards of the panel are left
    /// alone. Original: <c>FormPrincipalModeloLocal.ProcesarCarpeta</c> and <c>ProcesarCarpetaModelo</c>.
    /// </summary>
    /// <param name="firstFolder">A folder to start the list with.</param>
    public async Task<List<string>> ProcessFoldersAsync(string? firstFolder = null)
    {
        var made = new List<string>();
        ProjectSettings settings = ProjectSettings.Settings;
        // Sheets for Tabletop Simulator ask for their table and their files one by one.
        bool tabletop = settings.Group.ProjectType == ProjectType.Tabletop;

        // The layout the PDFs will have, to confirm. Original: MensajeConfiguracion.
        string layout = StringResources.Get("Filas") + ":" + settings.PrintLayout.Rows + Environment.NewLine
            + StringResources.Get("Columnas") + ":" + settings.PrintLayout.Columns + Environment.NewLine
            + StringResources.Get("Form_Setup_GroupBox_Papel") + ":" + settings.PrintLayout.Paper.Type + Environment.NewLine
            + "X:" + settings.Grid.CardSize.X.ToDisplayString(withSuffix: true, newLine: true)
            + "Y:" + settings.Grid.CardSize.Y.ToDisplayString(withSuffix: true, newLine: false);
        if (!tabletop && Ask != null && await Ask(layout, false) != true)
        {
            return made;
        }

        var folders = new ProcessFoldersViewModel();
        if (!string.IsNullOrEmpty(firstFolder))
        {
            folders.Folders.Add(firstFolder);
        }

        if (ShowProcessFolders == null || await ShowProcessFolders(folders) != true || folders.Folders.Count == 0)
        {
            return made;
        }

        bool onePerFolder = folders.OnePdfPerFolder;
        string? destination = tabletop ? string.Empty
            : onePerFolder ? AskFolder == null ? null : await AskFolder()
            : AskPdfToSave == null ? null : await AskPdfToSave(string.Empty);
        if (!tabletop && string.IsNullOrEmpty(destination))
        {
            return made;
        }

        destination ??= string.Empty;
        if (!tabletop && !onePerFolder && !destination.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            destination += ".pdf";
        }

        (string[] Folders, string Pdf)[] jobs = onePerFolder
            ? [.. folders.Folders.Select(folder => (new[] { folder }, Path.Combine(destination, Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)) + ".pdf")))]
            : [([.. folders.Folders], destination)];
        foreach ((string[] group, string pdf) in jobs)
        {
            // The folders in the order of the list, the images of each by name.
            string[] images = await Task.Run(() => group.SelectMany(folder => Panel.ImagePanelViewModel.ImagesAmong([folder])).ToArray());
            if (images.Length == 0)
            {
                await Say(StringResources.Get("no_hay_imagenes") + Environment.NewLine + string.Join(Environment.NewLine, group));
                continue;
            }

            // Every image is checked first. Original: ProcesarCarpeta_GeneraMazo.
            string[] found = images;
            Task Check(Action<int> report, CancellationToken cancellation)
            {
                images = ImageValidation.Usable(found, report, cancellation);
                return Task.CompletedTask;
            }

            Exception? failure = null;
            if (RunWithProgress != null)
            {
                failure = await RunWithProgress(StringResources.Get("Form_ValidarImagenes"), Check);
            }
            else
            {
                await Task.Run(() => Check(_ => { }, CancellationToken.None));
            }

            if (failure is OperationCanceledException)
            {
                return made;
            }

            if (failure != null)
            {
                Log.Write("Images not checked: " + failure, force: true);
                await Say(failure.Message);
                return made;
            }

            if (images.Length == 0)
            {
                await Say(StringResources.Get("no_hay_imagenes") + Environment.NewLine + string.Join(Environment.NewLine, group));
                continue;
            }

            Project project = CurrentProject();
            var deck = new Deck();
            await Task.Run(() => deck.LoadFromFiles(images, project.Settings.PrintLayout.DoubleSided, project.Settings.BlankFace, clearFirst: true));
            project.Deck = deck;
            if (await OfferToCutDoublePagesAsync(project))
            {
                return made;
            }

            if (tabletop)
            {
                made.AddRange(await MakeTabletopAsync(project, Path.GetFileNameWithoutExtension(pdf)));
                continue;
            }

            PdfOutput? output = await MakePdfAsync(project, preview: false, Path.GetFileName(pdf), 0.0, fixedPath: pdf, open: !onePerFolder);
            if (output != null)
            {
                made.AddRange(output.Files);
            }
        }

        if (!tabletop && onePerFolder && made.Count != 0)
        {
            Platform.Services.OpenFolder(destination);
        }

        return made;
    }

    /// <summary>
    /// In a book, pages much wider than the rest are probably two pages in one image: the
    /// user is offered to cut them, and then no PDF is made. Original: <c>ComprobarImagenesGrandes</c>.
    /// </summary>
    private async Task<bool> OfferToCutDoublePagesAsync(Project project)
    {
        if (project.Settings.Group.ProjectType != ProjectType.Book)
        {
            return false;
        }

        double mean = new DeckStatistics(project.Deck, BackImageMode.NoBack).Mean.X.Millimeter;
        string[] wide = [.. project.Deck.Cards.Where(card => card.Front.Size.X.Millimeter > 1.5 * mean).Select(card => card.Front.Path)];
        if (wide.Length == 0 || Ask == null || await Ask(StringResources.Get("Libro_PaginasDobles"), false) != true)
        {
            return false;
        }

        if (ShowCutImages != null)
        {
            await ShowCutImages(new CutImagesViewModel(wide) { Columns = 2, Rows = 1 });
        }

        return true;
    }

    /// <summary>
    /// Asks for the table and the files of the sheets for Tabletop Simulator; set by the main
    /// window. Gives the folder and beginning of the names of the files, or null when
    /// cancelled. Original: <c>Window_Tabletop</c>.
    /// </summary>
    public Func<TabletopViewModel, Task<string?>>? AskTabletop { get; set; }

    /// <summary>
    /// Writes the cards of a project as sheets for Tabletop Simulator and opens their folder.
    /// Gives the files written. Original: <c>BotonCrearSalidaTabletop</c> and the work of
    /// <c>Window_Tabletop</c>.
    /// </summary>
    /// <param name="source">The project; it is not changed.</param>
    /// <param name="name">A name to offer for the files.</param>
    public async Task<List<string>> MakeTabletopAsync(Project source, string name = "")
    {
        var written = new List<string>();
        Project project = PdfGenerator.ForOutput(source);
        ProjectSettings settings = project.Settings;
        project.Deck.RecountCopies();
        if (project.Deck.TotalCopies == 0)
        {
            await Say(StringResources.Get("no_hay_imagenes"));
            return written;
        }

        // Fronts and backs in turn when the cards show both, or share one back.
        bool twoFaces = settings.DoublePanel || settings.PrintLayout.BackImage == BackImageMode.SameBack;
        PdfImage[] images = PdfImageList.Generate(project);
        var sheets = new PnpTool.Pdf.Tabletop.TabletopSheets(images, twoFaces, settings);
        PointI cell = sheets.CellSize();
        if (cell.X < 1 || cell.Y < 1)
        {
            // Cards without a size have no place on a sheet.
            await Say(StringResources.Get("faltan_datos"));
            return written;
        }

        var request = new TabletopViewModel(sheets, settings.Grid.CardSize)
        {
            FileName = name,
            Folder = Directory.Exists(AppSettings.LastFolder) ? AppSettings.LastFolder : string.Empty,
        };
        string? basePath = AskTabletop == null ? null : await AskTabletop(request);
        if (string.IsNullOrEmpty(basePath))
        {
            return written;
        }

        Task Work(Action<int> report, CancellationToken cancellation)
        {
            written = sheets.Write(basePath, done => report(done * 100 / images.Length), cancellation);
            return Task.CompletedTask;
        }

        Exception? failure;
        if (RunWithProgress != null)
        {
            failure = await RunWithProgress(StringResources.Get("Form_Tabletop"), Work);
        }
        else
        {
            failure = null;
            try
            {
                await Task.Run(() => Work(percent => Progress = percent, CancellationToken.None));
            }
            catch (Exception e)
            {
                failure = e;
            }

            Progress = 0;
        }

        if (failure is OperationCanceledException)
        {
            return written;
        }

        if (failure != null)
        {
            Log.Write("Tabletop sheets not made: " + failure, force: true);
            await Say(failure is OutOfMemoryException ? StringResources.Get("memoria_agotada") : failure.Message);
            return written;
        }

        string? folder = Path.GetDirectoryName(basePath);
        if (!string.IsNullOrEmpty(folder))
        {
            AppSettings.LastFolder = folder;
            Platform.Services.OpenFolder(folder);
        }

        return written;
    }

    /// <summary>
    /// Asks where to write the templates of "seven pixels" dice; set by the main window.
    /// Gives the folder and beginning of the names of the files, or null when cancelled.
    /// Original: <c>Window_Dice7Pixels</c>.
    /// </summary>
    public Func<DiceViewModel, Task<string?>>? AskDice { get; set; }

    /// <summary>
    /// Writes the images of a project as templates of the die its style names, says so and
    /// opens their folder. Gives the files written. Original: <c>BotonCrearSalidaDice7Pixels</c>
    /// and the work of <c>Window_Dice7Pixels</c>.
    /// </summary>
    /// <param name="source">The project; it is not changed.</param>
    public async Task<List<string>> MakeDiceAsync(Project source)
    {
        var written = new List<string>();
        Project project = PdfGenerator.ForOutput(source);
        ProjectSettings settings = project.Settings;
        var dice = new PnpTool.Pdf.Tabletop.SevenPixelDice(
            project.Deck.Cards,
            PnpTool.Pdf.Tabletop.SevenPixelDice.FromStyleIndex(settings.Group.SubgroupIndex),
            settings.Front.BackgroundFillColor);
        if (dice.ImageCount == 0)
        {
            await Say(StringResources.Get("no_hay_imagenes"));
            return written;
        }

        var request = new DiceViewModel(dice) { Folder = Directory.Exists(AppSettings.LastFolder) ? AppSettings.LastFolder : string.Empty };
        string? basePath = AskDice == null ? null : await AskDice(request);
        if (string.IsNullOrEmpty(basePath))
        {
            return written;
        }

        Task Work(Action<int> report, CancellationToken cancellation)
        {
            written = dice.Write(basePath, done => report(done * 100 / dice.DiceCount), cancellation);
            return Task.CompletedTask;
        }

        Exception? failure;
        if (RunWithProgress != null)
        {
            failure = await RunWithProgress(StringResources.Get("Form_Dice7Pixels"), Work);
        }
        else
        {
            failure = null;
            try
            {
                await Task.Run(() => Work(percent => Progress = percent, CancellationToken.None));
            }
            catch (Exception e)
            {
                failure = e;
            }

            Progress = 0;
        }

        if (failure is OperationCanceledException)
        {
            return written;
        }

        if (failure != null)
        {
            Log.Write("Dice not made: " + failure, force: true);
            await Say(failure is OutOfMemoryException ? StringResources.Get("memoria_agotada") : failure.Message);
            return written;
        }

        await Say(StringResources.Get("proceso_completado"));
        string? folder = Path.GetDirectoryName(basePath);
        if (!string.IsNullOrEmpty(folder))
        {
            AppSettings.LastFolder = folder;
            Platform.Services.OpenFolder(folder);
        }

        return written;
    }

    /// <summary>Checks a project, asks what is needed and writes its PDF. Original: <c>BotonCrearFicheroSalida</c>.</summary>
    private async Task<PdfOutput?> MakePdfAsync(Project source, bool preview, string name, double sizeOnDiskMb, string? fixedPath = null, bool open = true)
    {
        Project project = PdfGenerator.ForOutput(source);
        ProjectSettings settings = project.Settings;
        if (settings.Group.ProjectType == ProjectType.Tabletop)
        {
            // Not a PDF: images for Tabletop Simulator. Original: BotonCrearSalidaTabletop.
            await MakeTabletopAsync(source, Path.GetFileNameWithoutExtension(name));
            return null;
        }

        if (settings.Group.ProjectType == ProjectType.Dice7Pixel)
        {
            // Not a PDF either: templates of dice. Original: BotonCrearSalidaDice7Pixels.
            await MakeDiceAsync(source);
            return null;
        }

        if (settings.Group.ProjectType == ProjectType.Papermini)
        {
            await Say(settings.Group.ProjectType + ": " + NotYetAvailable);
            return null;
        }

        project.Deck.RecountCopies();
        AppError invalid = SettingsValidator.Check(settings, project.Deck.TotalCopies);
        settings.FillFaces = SettingsValidator.FillFaces(settings);
        if (invalid.HasError)
        {
            await Say(invalid.Message);
            return null;
        }

        if (settings.Other.PdfOutput != PdfOutputMode.MaximumSize && sizeOnDiskMb > 5120.0)
        {
            await Say(StringResources.Get("FicheroMuyGrande"));
        }

        AppError missing = SettingsValidator.MissingFiles(project.Deck.Cards);
        if (settings.PrintLayout.BackImage is BackImageMode.OnlyOne or BackImageMode.Pattern && !File.Exists(settings.SharedBack.Path))
        {
            missing = new AppError { Kind = ErrorKind.Error, Message = settings.SharedBack.Path + " Not Found" };
        }

        if (missing.HasError)
        {
            bool? goOn = Ask == null ? true : await Ask(missing.Message + Environment.NewLine + StringResources.Get("Continuar"), false);
            if (goOn != true)
            {
                return null;
            }
        }

        AppError warnings = PdfGenerator.Warnings(settings, PdfGenerator.Prepare(project, preview), project.Deck);
        if (warnings.HasWarning)
        {
            await Say(warnings.Message);
        }

        string? path = preview
            ? Path.Combine(AppSettings.TempFolder, DateTime.Now.ToString("yyyy_M_d_H_s_fff") + ".pdf")
            : fixedPath ?? (AskPdfToSave == null ? null : await AskPdfToSave(name));
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (settings.Group.ProjectType == ProjectType.SpanishDeck)
        {
            // The masks of these decks leave five millimetres on every side of the image.
            settings.Front.Size.X = (double)settings.Front.Size.X - (double)Unit.FromMillimeter(10.0);
            settings.Front.Size.Y = (double)settings.Front.Size.Y - (double)Unit.FromMillimeter(10.0);
        }

        settings.InputParameters.OutputPdf = path;
        string workFolder = AppPaths.AppTempFolder.TrimEnd(Path.DirectorySeparatorChar);
        Directory.CreateDirectory(workFolder);
        PdfOutput? output = null;
        Task Work(Action<int> report, CancellationToken cancellation)
        {
            output = PdfGenerator.GenerateCancellable(project, workFolder, (percent, _) => report(percent), preview, cancellation);
            return Task.CompletedTask;
        }

        Exception? failure;
        if (RunWithProgress != null)
        {
            failure = await RunWithProgress(StringResources.Get("ShellMenuItem_File_Crear"), Work);
        }
        else
        {
            failure = null;
            try
            {
                await Task.Run(() => Work(percent => Progress = percent, CancellationToken.None));
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                failure = e;
            }

            Progress = 0;
        }

        if (failure != null)
        {
            Log.Write("PDF not made: " + failure, force: true);
            await Say(failure.Message);
            return null;
        }

        // A preview is made to be looked at; a finished PDF opens unless the settings say not to.
        // The Windows program has the setting and never reads it: it always opens the PDF.
        if (output != null && open && (preview || AppSettings.OpenPdfWhenDone))
        {
            if (output.SingleFile && output.Files.Count != 0)
            {
                Platform.Services.OpenFile(output.Files[0]);
            }
            else
            {
                Platform.Services.OpenFolder(output.Folder);
            }
        }

        return output;
    }

    private Task Say(string message) => ShowMessage?.Invoke(message) ?? Task.CompletedTask;
}
