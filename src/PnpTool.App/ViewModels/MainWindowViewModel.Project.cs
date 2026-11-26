using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Core.Templates;
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
        return await MakePdfAsync(CurrentProject(), preview, name, ImagePanel.SizeOnDiskMb());
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

    /// <summary>Checks a project, asks what is needed and writes its PDF. Original: <c>BotonCrearFicheroSalida</c>.</summary>
    private async Task<PdfOutput?> MakePdfAsync(Project source, bool preview, string name, double sizeOnDiskMb)
    {
        Project project = PdfGenerator.ForOutput(source);
        ProjectSettings settings = project.Settings;
        if (settings.Group.ProjectType is ProjectType.Tabletop or ProjectType.Dice7Pixel or ProjectType.Papermini)
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
            : AskPdfToSave == null ? null : await AskPdfToSave(name);
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

        if (output != null)
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
