using System.Text;
using System.Xml.Linq;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Tests.Files;

namespace PnpTool.Core.Tests.Infrastructure;

[Collection(GlobalState.Name)]
public sealed class AppSettingsTests : IDisposable
{
    private readonly GlobalStateScope _scope = new();
    private readonly TestFiles.TempFolder _temp = new();

    public AppSettingsTests() => AppSettings.Reset();

    public void Dispose()
    {
        _scope.Dispose();
        _temp.Dispose();
    }

    [Fact]
    public void Defaults_match_the_windows_program()
    {
        Assert.Equal("es", AppSettings.LanguageCode);
        Assert.Equal(UnitType.Millimeter, AppSettings.MeasurementSystem);
        Assert.Equal("mm", AppSettings.MeasurementSuffix);
        Assert.Equal(UpdateChannel.Minor, AppSettings.UpdateNotifications);
        Assert.Equal(1.0, AppSettings.PrinterMargin.X.Millimeter, 9);
        Assert.Equal(1.0, AppSettings.PrinterMargin.Y.Millimeter, 9);
        Assert.Equal(100, AppSettings.MinimumDpi);
        Assert.Equal(1000, AppSettings.MaximumDpi);
        Assert.Equal(300, AppSettings.TargetDpi);
        Assert.Equal(300, AppSettings.PerformanceWarningImageCount);
        Assert.False(AppSettings.ShowPathOnHover);
        Assert.True(AppSettings.OpenPdfWhenDone);
        Assert.False(AppSettings.ShowCmykWarning);
        Assert.True(AppSettings.ShowCmykWarningOnce);
        Assert.True(AppSettings.StartMaximized);
        Assert.True(AppSettings.FloatingSetupWindows);
        Assert.Equal(100, AppSettings.SetupPanelScale);
        Assert.False(AppSettings.WriteLogs);
        Assert.False(AppSettings.EmbedColorProfile);
        Assert.Equal(5, AppSettings.SmallFileSizeKb);
        Assert.Equal(AddImagesResult.Ask, AppSettings.DefaultAddImagesChoice);
        Assert.Equal(ImageResolutionType.MediumBitmap, AppSettings.PanelResolution);
    }

    [Fact]
    public void Settings_survive_a_save_and_a_restart()
    {
        string file = _temp.File("settings.xml");
        string profile = _temp.File("my.icc");
        File.WriteAllBytes(profile, []);
        AppSettings.LanguageCode = "en-GB";
        AppSettings.MeasurementSystem = UnitType.Inch;
        AppSettings.MeasurementSuffix = "in";
        AppSettings.UpdateNotifications = UpdateChannel.None;
        AppSettings.PrinterMargin = new UnitPoint { X = Unit.FromMillimeter(4), Y = Unit.FromMillimeter(6.5) };
        AppSettings.MinimumDpi = 150;
        AppSettings.MaximumDpi = 600;
        AppSettings.TargetDpi = 450;
        AppSettings.PerformanceWarningImageCount = 42;
        AppSettings.TempFolder = _temp.Root;
        AppSettings.ShowPathOnHover = true;
        AppSettings.OpenPdfWhenDone = false;
        AppSettings.ShowCmykWarning = true;
        AppSettings.StartMaximized = false;
        AppSettings.OpenProjectFile = "/projects/game.pnp";
        AppSettings.LastFolder = _temp.Root;
        AppSettings.FloatingSetupWindows = false;
        AppSettings.SetupPanelScale = 125;
        AppSettings.MonitorPhysicalSize = new PointI(600, 340);
        AppSettings.WriteLogs = true;
        AppSettings.EmbedColorProfile = true;
        AppSettings.SmallFileSizeKb = 12;
        AppSettings.DefaultAddImagesChoice = AddImagesResult.ManyFrontsOneBack;
        AppSettings.PanelResolution = ImageResolutionType.LargeBitmap;
        AppSettings.WindowSize = new PointI(1400, 900);
        AppSettings.WindowPosition = new PointI(-1800, 40);
        AppSettings.RgbIccPath = profile;
        AppSettings.CmykIccPath = profile;

        ProjectFileStore.SaveAppSettings(file);
        AppSettings.Reset();
        AppSettings.TempFolder = Path.GetTempPath();
        AppSettings.LastFolder = string.Empty;
        AppSettings.RgbIccPath = string.Empty;
        AppSettings.CmykIccPath = string.Empty;
        AppError error = ProjectFileStore.LoadAppSettings(file);

        Assert.False(error.HasError, error.Message);
        Assert.Equal("en-GB", AppSettings.LanguageCode);
        Assert.Equal(UnitType.Inch, AppSettings.MeasurementSystem);
        Assert.Equal("in", AppSettings.MeasurementSuffix);
        Assert.Equal(UpdateChannel.None, AppSettings.UpdateNotifications);
        Assert.Equal(4, AppSettings.PrinterMargin.X.Millimeter, 9);
        Assert.Equal(6.5, AppSettings.PrinterMargin.Y.Millimeter, 9);
        Assert.Equal(150, AppSettings.MinimumDpi);
        Assert.Equal(600, AppSettings.MaximumDpi);
        Assert.Equal(450, AppSettings.TargetDpi);
        Assert.Equal(42, AppSettings.PerformanceWarningImageCount);
        Assert.Equal(_temp.Root, AppSettings.TempFolder);
        Assert.True(AppSettings.ShowPathOnHover);
        Assert.False(AppSettings.OpenPdfWhenDone);
        Assert.True(AppSettings.ShowCmykWarning);
        Assert.False(AppSettings.StartMaximized);
        Assert.Equal("/projects/game.pnp", AppSettings.OpenProjectFile);
        Assert.Equal(_temp.Root, AppSettings.LastFolder);
        Assert.False(AppSettings.FloatingSetupWindows);
        Assert.Equal(125, AppSettings.SetupPanelScale);
        Assert.Equal(340, AppSettings.MonitorPhysicalSize.Y);
        Assert.True(AppSettings.WriteLogs);
        Assert.True(AppSettings.EmbedColorProfile);
        Assert.Equal(12, AppSettings.SmallFileSizeKb);
        Assert.Equal(AddImagesResult.ManyFrontsOneBack, AppSettings.DefaultAddImagesChoice);
        Assert.Equal(ImageResolutionType.LargeBitmap, AppSettings.PanelResolution);
        Assert.Equal(1400, AppSettings.WindowSize.X);
        Assert.Equal(-1800, AppSettings.WindowPosition?.X);
        Assert.Equal(40, AppSettings.WindowPosition?.Y);
        Assert.Equal(profile, AppSettings.RgbIccPath);
        Assert.Equal(profile, AppSettings.CmykIccPath);
    }

    [Fact]
    public void Settings_file_has_the_windows_layout_and_names()
    {
        string file = _temp.File("settings.xml");
        ProjectFileStore.SaveAppSettings(file);

        Assert.Equal([0xFF, 0xFE], File.ReadAllBytes(file)[..2]);
        Assert.Contains("<!--Fichero del programa PNP Tool - APP Settings ", File.ReadAllText(file, Encoding.Unicode));
        XElement root = XDocument.Load(file).Root!;
        Assert.Equal("PNP_Tool_ConfiguracionPrograma", root.Name.LocalName);
        Assert.Equal("18", root.Element("Version")!.Value);
        XElement settings = root.Element("ConfiguracionProgramaNoEstatica")!;
        Assert.Equal(
            [
                "IdiomaActual", "SistemaMetrico", "AvisarActualizaciones", "MargenImpresora", "DPIMinimo", "DPIMaximo",
                "DPIObjetivo", "NumeroImagenesAlarmaRendimiento", "RutaFicherosTemporales", "MostrarRutaAlPasarRaton",
                "AbrirPDFAlTerminar", "MostrarAvisoCMYK", "IniciarVentanaMaximizada", "TerminoSistemaMetrico",
                "FicheroPNPAbierto", "UltimaCarpeta", "AbrirVentanasConfiguracionFlotante", "VersionPrograma",
                "Formato_Panel_Setup", "TamanoFisicoMonitor", "CreaLogs", "InsertarPerfilColor", "TamanoFicherosPequenos",
                "OpcionDefectoAnadirImagenes", "ResolucionPaneles", "TamanoVentanaAbierta", "RutaIccRGB", "RutaIccCMYKDefecto",
            ],
            settings.Elements().Select(element => element.Name.LocalName));
        Assert.Equal("mm", settings.Element("SistemaMetrico")!.Value);
        Assert.Equal("Menor", settings.Element("AvisarActualizaciones")!.Value);
        Assert.Equal("Ask", settings.Element("OpcionDefectoAnadirImagenes")!.Value);
    }

    [Fact]
    public void Reads_a_settings_file_written_by_windows()
    {
        // As written by PNP Tool 20.11 for Windows, with the elements the port does not keep.
        string file = _temp.File("default_appsettings.xml");
        File.WriteAllText(
            file,
            """
            <!--Fichero del programa PNP Tool - APP Settings 20.11.1 - With Magic-->
            <PNP_Tool_ConfiguracionPrograma>
              <Version>18</Version>
              <ConfiguracionProgramaNoEstatica xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
                <IdiomaActual>en-US</IdiomaActual>
                <SistemaMetrico>inch</SistemaMetrico>
                <AvisarActualizaciones>Ninguna</AvisarActualizaciones>
                <MargenImpresora>
                  <X><Point>14.173228346456694</Point></X>
                  <Y><Point>8.503937007874017</Point></Y>
                </MargenImpresora>
                <DPIMinimo>120</DPIMinimo>
                <DPIMaximo>900</DPIMaximo>
                <DPIObjetivo>600</DPIObjetivo>
                <NumeroImagenesAlarmaRendimiento>500</NumeroImagenesAlarmaRendimiento>
                <RutaFicherosTemporales>C:\Users\ana\AppData\Local\Temp\</RutaFicherosTemporales>
                <MostrarRutaAlPasarRaton>true</MostrarRutaAlPasarRaton>
                <AbrirPDFAlTerminar>false</AbrirPDFAlTerminar>
                <MostrarAvisoCMYK>true</MostrarAvisoCMYK>
                <IniciarVentanaMaximizada>false</IniciarVentanaMaximizada>
                <TerminoSistemaMetrico>inch</TerminoSistemaMetrico>
                <FicheroPNPAbierto />
                <UltimaCarpeta>C:\Users\ana\Documents\</UltimaCarpeta>
                <AbrirVentanasConfiguracionFlotante>false</AbrirVentanasConfiguracionFlotante>
                <VersionPrograma>20.11.1 - With Magic</VersionPrograma>
                <SeLanzaProgramaDesdeWindows>false</SeLanzaProgramaDesdeWindows>
                <MostrarPantallaNuevoAbrir>false</MostrarPantallaNuevoAbrir>
                <Formato_Panel_Setup>100</Formato_Panel_Setup>
                <TamanoFisicoMonitor><X>527</X><Y>296</Y></TamanoFisicoMonitor>
                <V_Photoshop />
                <CreaLogs>true</CreaLogs>
                <EnvioAnalitica>true</EnvioAnalitica>
                <InsertarPerfilColor>true</InsertarPerfilColor>
                <TamanoFicherosPequenos>8</TamanoFicherosPequenos>
                <OpcionDefectoAnadirImagenes>FNT1</OpcionDefectoAnadirImagenes>
                <ResolucionPaneles>SmallBitmap</ResolucionPaneles>
                <TamanoVentanaAbierta><X>1920</X><Y>1040</Y></TamanoVentanaAbierta>
                <RutaIccRGB>C:\PNP\config\icc\sRGB-IEC61966-2.1.icc</RutaIccRGB>
                <RutaIccCMYKDefecto>C:\PNP\config\icc\CoatedFOGRA39.icc</RutaIccCMYKDefecto>
              </ConfiguracionProgramaNoEstatica>
            </PNP_Tool_ConfiguracionPrograma>
            """,
            Encoding.Unicode);
        AppSettings.TempFolder = "/kept/tmp/";
        AppSettings.LastFolder = "/kept/last/";
        AppSettings.RgbIccPath = "/kept/rgb.icc";
        AppSettings.CmykIccPath = "/kept/cmyk.icc";

        AppError error = ProjectFileStore.LoadAppSettings(file);

        Assert.False(error.HasError, error.Message);
        Assert.Equal("en-US", AppSettings.LanguageCode);
        Assert.Equal(UnitType.Inch, AppSettings.MeasurementSystem);
        Assert.Equal(UpdateChannel.None, AppSettings.UpdateNotifications);
        Assert.Equal(5, AppSettings.PrinterMargin.X.Millimeter, 6);
        Assert.Equal(3, AppSettings.PrinterMargin.Y.Millimeter, 6);
        Assert.Equal(600, AppSettings.TargetDpi);
        Assert.True(AppSettings.ShowPathOnHover);
        Assert.False(AppSettings.OpenPdfWhenDone);
        Assert.Equal("inch", AppSettings.MeasurementSuffix);
        Assert.Equal(527, AppSettings.MonitorPhysicalSize.X);
        Assert.True(AppSettings.WriteLogs);
        Assert.True(AppSettings.EmbedColorProfile);
        Assert.Equal(8, AppSettings.SmallFileSizeKb);
        Assert.Equal(AddImagesResult.ManyFrontsOneBack, AppSettings.DefaultAddImagesChoice);
        Assert.Equal(ImageResolutionType.SmallBitmap, AppSettings.PanelResolution);
        Assert.Equal(1040, AppSettings.WindowSize.Y);

        // Folders and profiles of the other computer do not exist here, so the current ones stay.
        Assert.Equal("/kept/tmp/", AppSettings.TempFolder);
        Assert.Equal("/kept/last/", AppSettings.LastFolder);
        Assert.Equal("/kept/rgb.icc", AppSettings.RgbIccPath);
        Assert.Equal("/kept/cmyk.icc", AppSettings.CmykIccPath);
    }

    [Fact]
    public void Missing_settings_file_is_an_error_that_changes_nothing()
    {
        AppSettings.TargetDpi = 777;

        AppError error = ProjectFileStore.LoadAppSettings(_temp.File("none.xml"));

        Assert.True(error.HasError);
        Assert.Equal(777, AppSettings.TargetDpi);
    }

    [Fact]
    public void Startup_creates_the_user_folder_and_reloads_what_was_saved()
    {
        string assets = AppPaths.FindAssetsFolder(AppContext.BaseDirectory);
        string user = _temp.File("user");

        AppError first = Startup.Initialize(assets, user);
        Assert.False(first.HasError, first.Message);
        Assert.False(File.Exists(AppPaths.SettingsFile));
        Assert.True(File.Exists(AppSettings.RgbIccPath), AppSettings.RgbIccPath);
        Assert.True(File.Exists(AppSettings.CmykIccPath), AppSettings.CmykIccPath);

        AppSettings.TargetDpi = 450;
        AppSettings.LanguageCode = "pt";
        AppSettings.Save();
        AppSettings.Reset();

        AppError second = Startup.Initialize(assets, user);
        Assert.False(second.HasError, second.Message);
        Assert.Equal(450, AppSettings.TargetDpi);
        Assert.Equal("pt", AppSettings.LanguageCode);
        Assert.Contains("<Startup-Initialize>Initialize", File.ReadAllText(AppPaths.LogFile));
    }

    [Fact]
    public void Startup_survives_a_damaged_settings_file()
    {
        string assets = AppPaths.FindAssetsFolder(AppContext.BaseDirectory);
        string user = _temp.File("user");
        Directory.CreateDirectory(user);
        File.WriteAllText(Path.Combine(user, "default_appsettings.xml"), "<PNP_Tool_ConfiguracionPrograma><Version>18</Vers");

        AppError error = Startup.Initialize(assets, user);

        Assert.False(error.HasError, error.Message);
        Assert.Equal(300, AppSettings.TargetDpi);
        Assert.Contains("Settings not loaded", File.ReadAllText(AppPaths.LogFile));
    }
}
