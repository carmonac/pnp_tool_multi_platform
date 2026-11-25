using System.Xml.Serialization;
using PnpTool.Core.Primitives;

namespace PnpTool.Core.Infrastructure;

/// <summary>
/// The part of <see cref="AppSettings"/> that is saved, in the shape of the settings file.
/// A new instance holds the current settings. Original: <c>ConfiguracionProgramaNoEstatica</c>,
/// which copied the values by reflection; here each one is copied by name.
/// </summary>
/// <remarks>
/// Elements of the Windows file that are not written: <c>EnvioAnalitica</c> (no analytics),
/// and <c>SeLanzaProgramaDesdeWindows</c>, <c>MostrarPantallaNuevoAbrir</c> and
/// <c>V_Photoshop</c>, which the original saved but never used. They are ignored when read.
/// </remarks>
[XmlType("ConfiguracionProgramaNoEstatica")]
public sealed class StoredAppSettings
{
    [XmlElement("IdiomaActual")]
    public string LanguageCode { get; set; } = "es";

    [XmlElement("SistemaMetrico")]
    public UnitType MeasurementSystem { get; set; }

    [XmlElement("AvisarActualizaciones")]
    public UpdateChannel UpdateNotifications { get; set; }

    [XmlElement("MargenImpresora")]
    public UnitPoint PrinterMargin { get; set; } = new();

    [XmlElement("DPIMinimo")]
    public int MinimumDpi { get; set; }

    [XmlElement("DPIMaximo")]
    public int MaximumDpi { get; set; }

    [XmlElement("DPIObjetivo")]
    public int TargetDpi { get; set; }

    [XmlElement("NumeroImagenesAlarmaRendimiento")]
    public int PerformanceWarningImageCount { get; set; }

    [XmlElement("RutaFicherosTemporales")]
    public string TempFolder { get; set; } = string.Empty;

    [XmlElement("MostrarRutaAlPasarRaton")]
    public bool ShowPathOnHover { get; set; }

    [XmlElement("AbrirPDFAlTerminar")]
    public bool OpenPdfWhenDone { get; set; }

    [XmlElement("MostrarAvisoCMYK")]
    public bool ShowCmykWarning { get; set; }

    [XmlElement("IniciarVentanaMaximizada")]
    public bool StartMaximized { get; set; }

    [XmlElement("TerminoSistemaMetrico")]
    public string MeasurementSuffix { get; set; } = "mm";

    [XmlElement("FicheroPNPAbierto")]
    public string OpenProjectFile { get; set; } = string.Empty;

    [XmlElement("UltimaCarpeta")]
    public string LastFolder { get; set; } = string.Empty;

    [XmlElement("AbrirVentanasConfiguracionFlotante")]
    public bool FloatingSetupWindows { get; set; }

    [XmlElement("VersionPrograma")]
    public string ProgramVersion { get; set; } = string.Empty;

    [XmlElement("Formato_Panel_Setup")]
    public int SetupPanelScale { get; set; }

    [XmlElement("TamanoFisicoMonitor")]
    public PointI MonitorPhysicalSize { get; set; } = new();

    [XmlElement("CreaLogs")]
    public bool WriteLogs { get; set; }

    [XmlElement("InsertarPerfilColor")]
    public bool EmbedColorProfile { get; set; }

    [XmlElement("TamanoFicherosPequenos")]
    public int SmallFileSizeKb { get; set; }

    [XmlElement("OpcionDefectoAnadirImagenes")]
    public AddImagesResult DefaultAddImagesChoice { get; set; }

    [XmlElement("ResolucionPaneles")]
    public ImageResolutionType PanelResolution { get; set; }

    [XmlElement("TamanoVentanaAbierta")]
    public PointI WindowSize { get; set; } = new();

    [XmlElement("RutaIccRGB")]
    public string RgbIccPath { get; set; } = string.Empty;

    [XmlElement("RutaIccCMYKDefecto")]
    public string CmykIccPath { get; set; } = string.Empty;

    /// <summary>Starts as a copy of the current settings, like the original.</summary>
    public StoredAppSettings() => CopyFromAppSettings();

    /// <summary>Original: <c>CopiarDeConfiguracionPrograma</c>.</summary>
    public void CopyFromAppSettings()
    {
        LanguageCode = string.IsNullOrEmpty(AppSettings.LanguageCode) ? "es" : AppSettings.LanguageCode;
        MeasurementSystem = AppSettings.MeasurementSystem;
        UpdateNotifications = AppSettings.UpdateNotifications;
        PrinterMargin = new UnitPoint(AppSettings.PrinterMargin);
        MinimumDpi = AppSettings.MinimumDpi;
        MaximumDpi = AppSettings.MaximumDpi;
        TargetDpi = AppSettings.TargetDpi;
        PerformanceWarningImageCount = AppSettings.PerformanceWarningImageCount;
        TempFolder = AppSettings.TempFolder;
        ShowPathOnHover = AppSettings.ShowPathOnHover;
        OpenPdfWhenDone = AppSettings.OpenPdfWhenDone;
        ShowCmykWarning = AppSettings.ShowCmykWarning;
        StartMaximized = AppSettings.StartMaximized;
        MeasurementSuffix = AppSettings.MeasurementSuffix;
        OpenProjectFile = AppSettings.OpenProjectFile;
        LastFolder = AppSettings.LastFolder;
        FloatingSetupWindows = AppSettings.FloatingSetupWindows;
        ProgramVersion = AppSettings.ProgramVersion;
        SetupPanelScale = AppSettings.SetupPanelScale;
        MonitorPhysicalSize = new PointI(AppSettings.MonitorPhysicalSize);
        WriteLogs = AppSettings.WriteLogs;
        EmbedColorProfile = AppSettings.EmbedColorProfile;
        SmallFileSizeKb = AppSettings.SmallFileSizeKb;
        DefaultAddImagesChoice = AppSettings.DefaultAddImagesChoice;
        PanelResolution = AppSettings.PanelResolution;
        WindowSize = new PointI(AppSettings.WindowSize);
        RgbIccPath = AppSettings.RgbIccPath;
        CmykIccPath = AppSettings.CmykIccPath;
    }

    /// <summary>
    /// Original: <c>CopiarAConfiguracionPrograma</c>. The program version is not copied back,
    /// as in the original, where it is read-only.
    /// </summary>
    /// <remarks>
    /// Unlike the original, a temporary folder, last folder or ICC profile that no longer
    /// exists is not taken from the file and the current value stays: the settings file can
    /// outlive a move of the application, whose bundled profiles then have a new path.
    /// </remarks>
    public void CopyToAppSettings()
    {
        AppSettings.LanguageCode = string.IsNullOrEmpty(LanguageCode) ? "es" : LanguageCode;
        AppSettings.MeasurementSystem = MeasurementSystem;
        AppSettings.UpdateNotifications = UpdateNotifications;
        AppSettings.PrinterMargin = new UnitPoint(PrinterMargin);
        AppSettings.MinimumDpi = MinimumDpi;
        AppSettings.MaximumDpi = MaximumDpi;
        AppSettings.TargetDpi = TargetDpi;
        AppSettings.PerformanceWarningImageCount = PerformanceWarningImageCount;
        AppSettings.ShowPathOnHover = ShowPathOnHover;
        AppSettings.OpenPdfWhenDone = OpenPdfWhenDone;
        AppSettings.ShowCmykWarning = ShowCmykWarning;
        AppSettings.StartMaximized = StartMaximized;
        AppSettings.MeasurementSuffix = MeasurementSuffix;
        AppSettings.OpenProjectFile = OpenProjectFile;
        AppSettings.FloatingSetupWindows = FloatingSetupWindows;
        AppSettings.SetupPanelScale = SetupPanelScale;
        AppSettings.MonitorPhysicalSize = new PointI(MonitorPhysicalSize);
        AppSettings.WriteLogs = WriteLogs;
        AppSettings.EmbedColorProfile = EmbedColorProfile;
        AppSettings.SmallFileSizeKb = SmallFileSizeKb;
        AppSettings.DefaultAddImagesChoice = DefaultAddImagesChoice;
        AppSettings.PanelResolution = PanelResolution;
        AppSettings.WindowSize = new PointI(WindowSize);

        if (Directory.Exists(TempFolder))
        {
            AppSettings.TempFolder = TempFolder;
        }

        if (Directory.Exists(LastFolder))
        {
            AppSettings.LastFolder = LastFolder;
        }

        if (File.Exists(RgbIccPath))
        {
            AppSettings.RgbIccPath = RgbIccPath;
        }

        if (File.Exists(CmykIccPath))
        {
            AppSettings.CmykIccPath = CmykIccPath;
        }
    }
}
