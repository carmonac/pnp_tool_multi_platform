using PnpTool.Core.Files;
using PnpTool.Core.Primitives;

namespace PnpTool.Core.Infrastructure;

/// <summary>
/// Global program settings. Original: the static <c>ConfiguracionPrograma</c>.
/// <see cref="Load"/> and <see cref="Save"/> keep them in <see cref="AppPaths.SettingsFile"/>.
/// </summary>
/// <remarks>
/// Not ported: the update, blacklist and download addresses and the analytics switch (the
/// port makes no such connections), the printing company's constants, the Windows Explorer
/// registry lookup, and the screen-scale values, which belong to the user interface.
/// </remarks>
public static class AppSettings
{
    /// <summary>Colours are stored as "A_R_G_B" text. Original: <c>Color_Blanco</c>.</summary>
    public const string ColorWhite = "255_255_255_255";

    /// <summary>Original: <c>Color_Negro</c>. The alpha part is 0 in the original and is ignored when the colour is read.</summary>
    public const string ColorBlack = "0_0_0_0";

    /// <summary>Original: <c>CadenaFinProceso</c>.</summary>
    public const string ProcessEndMarker = "End";

    public const string ProjectExtension = ".pnp";

    public const string StyleExtension = ".pnps";

    public const string CustomDeckExtension = ".pnpm";

    public const string AdvancedTemplateExtension = ".pnpa";

    /// <summary>Original: <c>ExtensionPDFContorno</c>.</summary>
    public const string PdfOutlineExtension = ".pnpc";

    static AppSettings() => Reset();

    /// <summary>Written in the comment at the top of every saved file. Original: <c>VersionPrograma</c>.</summary>
    public static string ProgramVersion { get; set; } = "20.11.1 - With Magic";

    // ------------------------------------------------------------ saved settings

    /// <summary>Code of the interface language, such as "es" or "en-GB". Original: <c>IdiomaActual.ValorIdioma</c>.</summary>
    public static string LanguageCode { get; set; } = "es";

    /// <summary>Original: <c>SistemaMetrico</c>.</summary>
    public static UnitType MeasurementSystem { get; set; }

    /// <summary>Text shown after lengths. Original: <c>TerminoSistemaMetrico</c>.</summary>
    public static string MeasurementSuffix { get; set; } = "mm";

    /// <summary>Which new versions to announce. Original: <c>AvisarActualizaciones</c>.</summary>
    public static UpdateChannel UpdateNotifications { get; set; }

    /// <summary>Area at the edges of the sheet the printer cannot print. Original: <c>MargenImpresora</c>.</summary>
    public static UnitPoint PrinterMargin { get; set; } = new();

    /// <summary>Original: <c>DPIMinimo</c>.</summary>
    public static int MinimumDpi { get; set; }

    /// <summary>Original: <c>DPIMaximo</c>.</summary>
    public static int MaximumDpi { get; set; }

    /// <summary>Original: <c>DPIObjetivo</c>.</summary>
    public static int TargetDpi { get; set; }

    /// <summary>Number of images above which the program warns about speed. Original: <c>NumeroImagenesAlarmaRendimiento</c>.</summary>
    public static int PerformanceWarningImageCount { get; set; }

    /// <summary>Folder for temporary images, ending in a separator. Original: <c>RutaFicherosTemporales</c>.</summary>
    public static string TempFolder { get; set; } = Path.GetTempPath();

    /// <summary>Original: <c>MostrarRutaAlPasarRaton</c>.</summary>
    public static bool ShowPathOnHover { get; set; }

    /// <summary>Original: <c>AbrirPDFAlTerminar</c>.</summary>
    public static bool OpenPdfWhenDone { get; set; }

    /// <summary>Original: <c>MostrarAvisoCMYK</c>.</summary>
    public static bool ShowCmykWarning { get; set; }

    /// <summary>Original: <c>IniciarVentanaMaximizada</c>.</summary>
    public static bool StartMaximized { get; set; }

    /// <summary>Project open when the settings were saved. Original: <c>FicheroPNPAbierto</c>.</summary>
    public static string OpenProjectFile { get; set; } = string.Empty;

    /// <summary>Folder file dialogs start in. Original: <c>UltimaCarpeta</c>.</summary>
    public static string LastFolder { get; set; } = string.Empty;

    /// <summary>Original: <c>AbrirVentanasConfiguracionFlotante</c>.</summary>
    public static bool FloatingSetupWindows { get; set; }

    /// <summary>Size of the setup panel, in percent. Original: <c>Formato_Panel_Setup</c>.</summary>
    public static int SetupPanelScale { get; set; }

    /// <summary>Physical size of the monitor, entered by the user. Original: <c>TamanoFisicoMonitor</c>.</summary>
    public static PointI MonitorPhysicalSize { get; set; } = new();

    /// <summary>Whether <see cref="Log.Write"/> and unforced enter/exit lines are written. Original: <c>CreaLogs</c>.</summary>
    public static bool WriteLogs { get; set; }

    /// <summary>Embed the colour profile in the PDF. Original: <c>InsertarPerfilColor</c>.</summary>
    public static bool EmbedColorProfile { get; set; }

    /// <summary>Images below this size in kilobytes count as small when cutting. Original: <c>TamanoFicherosPequenos</c>.</summary>
    public static int SmallFileSizeKb { get; set; }

    /// <summary>Original: <c>OpcionDefectoAnadirImagenes</c>.</summary>
    public static AddImagesResult DefaultAddImagesChoice { get; set; }

    /// <summary>Resolution of the thumbnails in the image panel. Original: <c>ResolucionPaneles</c>.</summary>
    public static ImageResolutionType PanelResolution { get; set; }

    /// <summary>Size of the main window when it was last open. Original: <c>TamanoVentanaAbierta</c>.</summary>
    public static PointI WindowSize { get; set; } = new();

    /// <summary>Default RGB ICC profile file. Original: <c>RutaIccRGB</c>.</summary>
    public static string RgbIccPath { get; set; } = string.Empty;

    /// <summary>Default CMYK ICC profile file. Original: <c>RutaIccCMYKDefecto</c>.</summary>
    public static string CmykIccPath { get; set; } = string.Empty;

    // -------------------------------------------------------- not saved

    /// <summary>Original: <c>MostrarAvisoCMYK_1Vez</c>.</summary>
    public static bool ShowCmykWarningOnce { get; set; }

    /// <summary>Command-line arguments. Original: <c>Argumentos</c>.</summary>
    public static string Arguments { get; set; } = string.Empty;

    /// <summary>Folder of the bundled images, ending in a separator. Original: <c>RutaArchivosImagenes</c>.</summary>
    public static string ImagesFolder { get; set; } = string.Empty;

    /// <summary>Folder of advanced templates (.pnpa), ending in a separator. Original: <c>RutaArchivosEstilosAvanzados</c>.</summary>
    public static string AdvancedStylesFolder { get; set; } = string.Empty;

    /// <summary>
    /// Puts every setting back to the value the program starts with. Folders set by
    /// <see cref="AppPaths.Initialize"/> are left alone. Original: the static constructor.
    /// </summary>
    public static void Reset()
    {
        LanguageCode = "es";
        MeasurementSystem = UnitType.Millimeter;
        MeasurementSuffix = "mm";
        UpdateNotifications = UpdateChannel.Minor;
        PrinterMargin = new UnitPoint { X = Unit.FromMillimeter(1.0), Y = Unit.FromMillimeter(1.0) };
        MinimumDpi = 100;
        MaximumDpi = 1000;
        TargetDpi = 300;
        PerformanceWarningImageCount = 300;
        TempFolder = Path.GetTempPath();
        ShowPathOnHover = false;
        OpenPdfWhenDone = true;
        ShowCmykWarning = false;
        ShowCmykWarningOnce = true;
        StartMaximized = true;
        OpenProjectFile = string.Empty;
        FloatingSetupWindows = true;
        SetupPanelScale = 100;
        MonitorPhysicalSize = new PointI();
        WriteLogs = false;
        EmbedColorProfile = false;
        SmallFileSizeKb = 5;
        DefaultAddImagesChoice = AddImagesResult.Ask;
        PanelResolution = ImageResolutionType.MediumBitmap;
        WindowSize = new PointI();
    }

    /// <summary>Reads the settings file of this user. Original: <c>AbrirXMLAPPSettings_V2</c>.</summary>
    public static AppError Load() => ProjectFileStore.LoadAppSettings(AppPaths.SettingsFile);

    /// <summary>Writes the settings file of this user. Original: <c>GuardarXMLAPPSettings</c>.</summary>
    public static void Save() => ProjectFileStore.SaveAppSettings(AppPaths.SettingsFile);
}
