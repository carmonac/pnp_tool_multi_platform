namespace PnpTool.Core.Infrastructure;

/// <summary>The operating systems the folder layout distinguishes.</summary>
public enum HostSystem
{
    Windows,
    MacOS,
    Linux,
}

/// <summary>
/// Where the program finds its files. Original: the <c>Ruta…</c> members of
/// <c>ConfiguracionPrograma</c> and <c>EstableceRuta_ConfiguracionProgramaGlobal</c>.
/// </summary>
/// <remarks>
/// The Windows program kept everything in a <c>config</c> folder beside the executable and
/// wrote into it. Here the files shipped with the program (<see cref="AssetsFolder"/>) are
/// read-only, and everything the program writes goes to a per-user folder
/// (<see cref="UserDataFolder"/>):
/// <list type="bullet">
/// <item>macOS: <c>~/Library/Application Support/PnpTool</c></item>
/// <item>Linux: <c>$XDG_CONFIG_HOME/PnpTool</c>, by default <c>~/.config/PnpTool</c></item>
/// <item>Windows: <c>%APPDATA%\PnpTool</c></item>
/// </list>
/// The environment variable <c>PNPTOOL_DATA_DIR</c> overrides the per-user folder.
/// Every folder property ends with a directory separator, as in the original.
/// </remarks>
public static class AppPaths
{
    /// <summary>Name of the per-user folder.</summary>
    public const string FolderName = "PnpTool";

    /// <summary>Environment variable that overrides the per-user folder.</summary>
    public const string DataFolderVariable = "PNPTOOL_DATA_DIR";

    /// <summary>Default RGB profile. Original: the file name in <c>RutaIccRGB</c>.</summary>
    public const string DefaultRgbProfile = "sRGB-IEC61966-2.1.icc";

    /// <summary>Default CMYK profile. Original: the file name in <c>RutaIccCMYKDefecto</c>.</summary>
    public const string DefaultCmykProfile = "CoatedFOGRA39.icc";

    static AppPaths()
    {
        AssetsFolder = string.Empty;
        UserDataFolder = string.Empty;
    }

    /// <summary>Files shipped with the program; never written to. Original: <c>RutaAconfiguracion</c>.</summary>
    public static string AssetsFolder { get; private set; }

    /// <summary>Folder for everything the program writes.</summary>
    public static string UserDataFolder { get; private set; }

    /// <summary>Bundled styles, one file per card or paper format. Original: <c>RutaArchivosEstilos + "defecto"</c>.</summary>
    public static string DefaultStylesFolder => Folder(AssetsFolder, "styles", "default");

    /// <summary>Styles saved by the user. Original: <c>RutaArchivosEstilosPersonalizados</c>.</summary>
    public static string CustomStylesFolder => Folder(UserDataFolder, "styles", "custom");

    /// <summary>Advanced templates (.pnpa), bundled ones included. Original: <c>RutaArchivosEstilosAvanzados</c>.</summary>
    public static string AdvancedStylesFolder => Folder(UserDataFolder, "styles", "advanced");

    /// <summary>Bundled custom decks and their images. Original: <c>RutaMazosPersonalizados</c>.</summary>
    public static string CustomDecksFolder => Folder(AssetsFolder, "custom-decks");

    /// <summary>Bundled ticket projects.</summary>
    public static string TicketsFolder => Folder(AssetsFolder, "tickets");

    /// <summary>Original: <c>RutaArchivosImagenes</c>.</summary>
    public static string ImagesFolder => Folder(AssetsFolder, "images");

    /// <summary>Original: <c>RutaBanderas</c>.</summary>
    public static string FlagsFolder => Folder(AssetsFolder, "images", "flags");

    /// <summary>Suit symbols and masks. Original: <c>RutaArchivosImagenes + "barajas"</c>.</summary>
    public static string SuitsFolder => Folder(AssetsFolder, "images", "suits");

    /// <summary>Fonts shipped with the program. Original: <c>RutaAconfiguracion + "fonts"</c>.</summary>
    public static string FontsFolder => Folder(AssetsFolder, "fonts");

    /// <summary>Original: <c>RutaIcc</c>.</summary>
    public static string IccFolder => Folder(AssetsFolder, "icc");

    /// <summary>Scratch folder inside the user data. Original: <c>RutaTemporalenApp</c>.</summary>
    public static string AppTempFolder => Folder(UserDataFolder, "temp");

    /// <summary>Original: <c>NombreFicheroConfiguracionAPP</c>.</summary>
    public static string SettingsFile => UserDataFolder + "default_appsettings.xml";

    /// <summary>Original: <c>RutaUltimosFicheros</c>.</summary>
    public static string RecentFilesFile => UserDataFolder + "LastPNPFiles.txt";

    /// <summary>Original: <c>RutaColoresPersonalizados</c>.</summary>
    public static string CustomColorsFile => UserDataFolder + "CustomColors.txt";

    /// <summary>Original: <c>RutaCreaLogs</c>.</summary>
    public static string LogFile => UserDataFolder + "logs.txt";

    /// <summary>
    /// Sets the two root folders and points <see cref="AppSettings"/> at them. With no
    /// arguments the assets are looked up near the executable and the user folder is the
    /// one for this operating system.
    /// </summary>
    public static void Initialize(string? assetsFolder = null, string? userDataFolder = null)
    {
        AssetsFolder = WithSeparator(assetsFolder ?? FindAssetsFolder(AppContext.BaseDirectory));
        UserDataFolder = WithSeparator(userDataFolder ?? DefaultUserDataFolder());

        AppSettings.ImagesFolder = ImagesFolder;
        AppSettings.AdvancedStylesFolder = AdvancedStylesFolder;
        AppSettings.RgbIccPath = Path.Combine(IccFolder, DefaultRgbProfile);
        AppSettings.CmykIccPath = Path.Combine(IccFolder, DefaultCmykProfile);
        AppSettings.LastFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    /// <summary>The per-user folder for the system the program is running on.</summary>
    public static string DefaultUserDataFolder()
    {
        HostSystem system = OperatingSystem.IsWindows() ? HostSystem.Windows
            : OperatingSystem.IsMacOS() ? HostSystem.MacOS
            : HostSystem.Linux;
        return UserDataFolderFor(
            system,
            Environment.GetEnvironmentVariable,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    /// <summary>
    /// The per-user folder for a given system, environment and home folder. Paths are joined
    /// with the separator of that system, so the result can be checked on any machine.
    /// </summary>
    public static string UserDataFolderFor(HostSystem system, Func<string, string?> environment, string home)
    {
        string? overridden = environment(DataFolderVariable);
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            return overridden;
        }

        switch (system)
        {
            case HostSystem.Windows:
                string? roaming = environment("APPDATA");
                return (string.IsNullOrWhiteSpace(roaming) ? home + @"\AppData\Roaming" : roaming) + @"\" + FolderName;
            case HostSystem.MacOS:
                return home + "/Library/Application Support/" + FolderName;
            default:
                // The XDG specification says a relative value must be ignored.
                string? xdg = environment("XDG_CONFIG_HOME");
                bool usable = !string.IsNullOrWhiteSpace(xdg) && xdg.StartsWith('/');
                return (usable ? xdg!.TrimEnd('/') : home + "/.config") + "/" + FolderName;
        }
    }

    /// <summary>
    /// Finds the <c>assets</c> folder: beside the executable, in <c>../Resources</c> (a macOS
    /// application bundle), or in a folder above it (running from the source tree). When none
    /// exists the place beside the executable is returned and <see cref="PrepareUserData"/> reports it.
    /// </summary>
    public static string FindAssetsFolder(string executableFolder)
    {
        string beside = Path.Combine(executableFolder, "assets");
        if (Directory.Exists(beside))
        {
            return beside;
        }

        string inBundle = Path.GetFullPath(Path.Combine(executableFolder, "..", "Resources", "assets"));
        if (Directory.Exists(inBundle))
        {
            return inBundle;
        }

        for (DirectoryInfo? folder = new DirectoryInfo(executableFolder).Parent; folder != null; folder = folder.Parent)
        {
            string candidate = Path.Combine(folder.FullName, "assets");
            if (Directory.Exists(Path.Combine(candidate, "styles", "default")))
            {
                return candidate;
            }
        }

        return beside;
    }

    /// <summary>
    /// Creates the per-user folders and files and checks that the program can work with them.
    /// On the first run the bundled user style and advanced templates are copied to the user
    /// folder, where the program lists and saves them. Original: <c>CompruebaPermisosArchivos</c>.
    /// </summary>
    public static AppError PrepareUserData()
    {
        Log.Enter(nameof(PrepareUserData), force: true);
        var error = new AppError();
        if (!Directory.Exists(AssetsFolder))
        {
            error.Kind = ErrorKind.Error;
            error.Message = "Assets folder does not exist (" + AssetsFolder + "). Please reinstall the software";
            Log.Exit(error.Message, force: true);
            return error;
        }

        var problems = new List<string>();
        try
        {
            Directory.CreateDirectory(UserDataFolder);
            Directory.CreateDirectory(AppTempFolder);
            SeedFolder(Folder(AssetsFolder, "styles", "custom"), CustomStylesFolder);
            SeedFolder(Folder(AssetsFolder, "styles", "advanced"), AdvancedStylesFolder);
            CreateIfMissing(RecentFilesFile);
            CreateIfMissing(CustomColorsFile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            problems.Add(UserDataFolder + " " + exception.Message);
        }

        if (File.Exists(RecentFilesFile) && new FileInfo(RecentFilesFile).IsReadOnly)
        {
            problems.Add("Error " + RecentFilesFile + " ReadOnly");
        }

        if (!Directory.Exists(DefaultStylesFolder))
        {
            problems.Add(DefaultStylesFolder + " not exist");
        }

        string? tempProblem = CheckWritable(AppSettings.TempFolder);
        if (tempProblem != null)
        {
            problems.Add(tempProblem);
        }

        if (problems.Count > 0)
        {
            error.Kind = ErrorKind.Error;
            error.Message = string.Join(" ", problems);
        }

        Log.Exit(error.Message, force: true);
        return error;
    }

    /// <summary>
    /// Whether files can be created in the folder, creating the folder if needed. Returns the
    /// problem, or null. The original inspected Windows access-control lists instead.
    /// </summary>
    private static string? CheckWritable(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            string probe = Path.Combine(folder, "pnptool-" + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return folder + " not found  / problems";
        }
    }

    /// <summary>Copies the files of a bundled folder the first time, when the user folder does not exist yet.</summary>
    private static void SeedFolder(string bundled, string user)
    {
        if (Directory.Exists(user))
        {
            return;
        }

        Directory.CreateDirectory(user);
        if (!Directory.Exists(bundled))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(bundled))
        {
            File.Copy(file, Path.Combine(user, Path.GetFileName(file)));
        }
    }

    private static void CreateIfMissing(string file)
    {
        if (!File.Exists(file))
        {
            File.Create(file).Close();
        }
    }

    private static string Folder(string root, params string[] parts) =>
        WithSeparator(Path.Combine([root, .. parts]));

    private static string WithSeparator(string folder) =>
        folder.Length == 0 || Path.EndsInDirectorySeparator(folder) ? folder : folder + Path.DirectorySeparatorChar;
}
