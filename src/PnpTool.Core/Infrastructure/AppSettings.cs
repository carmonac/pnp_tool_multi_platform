namespace PnpTool.Core.Infrastructure;

/// <summary>
/// Global program settings. Original: the static <c>ConfiguracionPrograma</c>.
/// Only the members the model classes need are here so far; the rest, and loading
/// and saving, arrive with backlog task 7.
/// </summary>
public static class AppSettings
{
    /// <summary>Written in the comment at the top of every saved file. Original: <c>VersionPrograma</c>.</summary>
    public static string ProgramVersion { get; set; } = "20.11.1 - With Magic";

    /// <summary>Colours are stored as "A_R_G_B" text. Original: <c>Color_Blanco</c>.</summary>
    public const string ColorWhite = "255_255_255_255";

    /// <summary>Original: <c>Color_Negro</c>. The alpha part is 0 in the original and is ignored when the colour is read.</summary>
    public const string ColorBlack = "0_0_0_0";

    /// <summary>Default RGB ICC profile file. Original: <c>RutaIccRGB</c>.</summary>
    public static string RgbIccPath { get; set; } = string.Empty;

    /// <summary>Default CMYK ICC profile file. Original: <c>RutaIccCMYKDefecto</c>.</summary>
    public static string CmykIccPath { get; set; } = string.Empty;

    /// <summary>Folder of the bundled images, ending in a separator. Original: <c>RutaArchivosImagenes</c>.</summary>
    public static string ImagesFolder { get; set; } = string.Empty;

    /// <summary>Folder of advanced templates (.pnpa), ending in a separator. Original: <c>RutaArchivosEstilosAvanzados</c>.</summary>
    public static string AdvancedStylesFolder { get; set; } = string.Empty;

    /// <summary>Folder for temporary images, ending in a separator. Original: <c>RutaFicherosTemporales</c>.</summary>
    public static string TempFolder { get; set; } = Path.GetTempPath();

    /// <summary>Original: <c>MostrarAvisoCMYK</c>.</summary>
    public static bool ShowCmykWarning { get; set; }

    /// <summary>Original: <c>MostrarAvisoCMYK_1Vez</c>.</summary>
    public static bool ShowCmykWarningOnce { get; set; }
}
