using PnpTool.Core.Infrastructure;
using PnpTool.Core.Tests.Files;

namespace PnpTool.Core.Tests.Infrastructure;

[Collection(GlobalState.Name)]
public sealed class AppPathsTests : IDisposable
{
    private readonly GlobalStateScope _scope = new();
    private readonly TestFiles.TempFolder _temp = new();

    public void Dispose()
    {
        _scope.Dispose();
        _temp.Dispose();
    }

    private static Func<string, string?> Environment(params (string Name, string Value)[] variables) =>
        name => variables.FirstOrDefault(variable => variable.Name == name).Value;

    [Fact]
    public void User_data_is_under_application_support_on_macos()
    {
        Assert.Equal(
            "/Users/ana/Library/Application Support/PnpTool",
            AppPaths.UserDataFolderFor(HostSystem.MacOS, Environment(), "/Users/ana"));
    }

    [Fact]
    public void User_data_is_under_dot_config_on_linux()
    {
        Assert.Equal("/home/ana/.config/PnpTool", AppPaths.UserDataFolderFor(HostSystem.Linux, Environment(), "/home/ana"));
    }

    [Fact]
    public void Linux_follows_xdg_config_home_when_it_is_absolute()
    {
        Assert.Equal(
            "/data/config/PnpTool",
            AppPaths.UserDataFolderFor(HostSystem.Linux, Environment(("XDG_CONFIG_HOME", "/data/config/")), "/home/ana"));
        Assert.Equal(
            "/home/ana/.config/PnpTool",
            AppPaths.UserDataFolderFor(HostSystem.Linux, Environment(("XDG_CONFIG_HOME", "relative")), "/home/ana"));
    }

    [Fact]
    public void User_data_is_under_roaming_app_data_on_windows()
    {
        Assert.Equal(
            @"C:\Users\ana\AppData\Roaming\PnpTool",
            AppPaths.UserDataFolderFor(HostSystem.Windows, Environment(("APPDATA", @"C:\Users\ana\AppData\Roaming")), @"C:\Users\ana"));
        Assert.Equal(
            @"C:\Users\ana\AppData\Roaming\PnpTool",
            AppPaths.UserDataFolderFor(HostSystem.Windows, Environment(), @"C:\Users\ana"));
    }

    [Theory]
    [InlineData(HostSystem.MacOS)]
    [InlineData(HostSystem.Linux)]
    [InlineData(HostSystem.Windows)]
    public void Environment_variable_overrides_the_user_data_folder(HostSystem system)
    {
        Assert.Equal(
            "/somewhere/else",
            AppPaths.UserDataFolderFor(system, Environment((AppPaths.DataFolderVariable, "/somewhere/else")), "/home/ana"));
    }

    [Fact]
    public void Default_user_data_folder_is_inside_the_home_folder()
    {
        if (System.Environment.GetEnvironmentVariable(AppPaths.DataFolderVariable) != null)
        {
            return;
        }

        string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        string folder = AppPaths.DefaultUserDataFolder();
        Assert.EndsWith(AppPaths.FolderName, folder);
        if (!OperatingSystem.IsLinux() || System.Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") == null)
        {
            Assert.StartsWith(home, folder);
        }
    }

    [Fact]
    public void Assets_are_found_beside_the_executable_first()
    {
        Directory.CreateDirectory(_temp.File("bin", "assets"));
        Assert.Equal(_temp.File("bin", "assets"), AppPaths.FindAssetsFolder(_temp.File("bin")));
    }

    [Fact]
    public void Assets_are_found_in_the_resources_of_a_macos_bundle()
    {
        Directory.CreateDirectory(_temp.File("PnpTool.app", "Contents", "MacOS"));
        Directory.CreateDirectory(_temp.File("PnpTool.app", "Contents", "Resources", "assets"));
        Assert.Equal(
            _temp.File("PnpTool.app", "Contents", "Resources", "assets"),
            AppPaths.FindAssetsFolder(_temp.File("PnpTool.app", "Contents", "MacOS")));
    }

    [Fact]
    public void Assets_are_found_in_the_source_tree_when_running_a_build()
    {
        string found = AppPaths.FindAssetsFolder(AppContext.BaseDirectory);
        Assert.True(File.Exists(Path.Combine(found, "styles", "default", "Cartas_Standar.pnps")), found);
    }

    [Fact]
    public void Folders_split_between_assets_and_user_data()
    {
        AppPaths.Initialize(_temp.File("assets"), _temp.File("user"));
        char s = Path.DirectorySeparatorChar;
        string assets = _temp.File("assets") + s;
        string user = _temp.File("user") + s;

        Assert.Equal(assets, AppPaths.AssetsFolder);
        Assert.Equal(user, AppPaths.UserDataFolder);
        Assert.Equal($"{assets}styles{s}default{s}", AppPaths.DefaultStylesFolder);
        Assert.Equal($"{assets}custom-decks{s}", AppPaths.CustomDecksFolder);
        Assert.Equal($"{assets}images{s}flags{s}", AppPaths.FlagsFolder);
        Assert.Equal($"{assets}images{s}suits{s}", AppPaths.SuitsFolder);
        Assert.Equal($"{assets}icc{s}", AppPaths.IccFolder);
        Assert.Equal($"{assets}tickets{s}", AppPaths.TicketsFolder);
        Assert.Equal($"{user}styles{s}custom{s}", AppPaths.CustomStylesFolder);
        Assert.Equal($"{user}styles{s}advanced{s}", AppPaths.AdvancedStylesFolder);
        Assert.Equal($"{user}temp{s}", AppPaths.AppTempFolder);
        Assert.Equal(user + "default_appsettings.xml", AppPaths.SettingsFile);
        Assert.Equal(user + "LastPNPFiles.txt", AppPaths.RecentFilesFile);
        Assert.Equal(user + "CustomColors.txt", AppPaths.CustomColorsFile);
        Assert.Equal(user + "logs.txt", AppPaths.LogFile);

        Assert.Equal(AppPaths.ImagesFolder, AppSettings.ImagesFolder);
        Assert.Equal(AppPaths.AdvancedStylesFolder, AppSettings.AdvancedStylesFolder);
        Assert.Equal($"{assets}icc{s}sRGB-IEC61966-2.1.icc", AppSettings.RgbIccPath);
        Assert.Equal($"{assets}icc{s}CoatedFOGRA39.icc", AppSettings.CmykIccPath);
    }

    [Fact]
    public void Preparing_user_data_creates_folders_and_copies_the_bundled_templates_once()
    {
        string assets = AppPaths.FindAssetsFolder(AppContext.BaseDirectory);
        AppPaths.Initialize(assets, _temp.File("user"));
        AppSettings.TempFolder = _temp.File("tmp") + Path.DirectorySeparatorChar;

        AppError error = AppPaths.PrepareUserData();

        Assert.False(error.HasError, error.Message);
        Assert.True(Directory.Exists(AppPaths.AppTempFolder));
        Assert.True(Directory.Exists(AppSettings.TempFolder));
        Assert.True(File.Exists(AppPaths.RecentFilesFile));
        Assert.True(File.Exists(AppPaths.CustomColorsFile));
        Assert.True(File.Exists(AppPaths.AdvancedStylesFolder + "1.pnpa"));
        Assert.True(File.Exists(AppPaths.CustomStylesFolder + "EstiloAvanzado.pnps"));

        // A template the user deleted is not brought back on the next start.
        File.Delete(AppPaths.AdvancedStylesFolder + "1.pnpa");
        Assert.False(AppPaths.PrepareUserData().HasError);
        Assert.False(File.Exists(AppPaths.AdvancedStylesFolder + "1.pnpa"));
    }

    [Fact]
    public void Bundled_assets_are_not_modified_by_preparing_user_data()
    {
        string assets = AppPaths.FindAssetsFolder(AppContext.BaseDirectory);
        DateTime before = Directory.GetLastWriteTimeUtc(Path.Combine(assets, "styles", "advanced"));
        AppPaths.Initialize(assets, _temp.File("user"));

        AppPaths.PrepareUserData();

        Assert.Equal(before, Directory.GetLastWriteTimeUtc(Path.Combine(assets, "styles", "advanced")));
    }

    [Fact]
    public void Missing_assets_are_reported()
    {
        AppPaths.Initialize(_temp.File("nothing-here"), _temp.File("user"));

        AppError error = AppPaths.PrepareUserData();

        Assert.True(error.HasError);
        Assert.Contains("reinstall", error.Message);
        Assert.False(Directory.Exists(_temp.File("user")));
    }

    [Fact]
    public void Read_only_recent_files_list_is_reported()
    {
        AppPaths.Initialize(AppPaths.FindAssetsFolder(AppContext.BaseDirectory), _temp.File("user"));
        Assert.False(AppPaths.PrepareUserData().HasError);
        var recent = new FileInfo(AppPaths.RecentFilesFile) { IsReadOnly = true };
        try
        {
            AppError error = AppPaths.PrepareUserData();
            Assert.True(error.HasError);
            Assert.Contains("ReadOnly", error.Message);
        }
        finally
        {
            recent.IsReadOnly = false;
        }
    }
}
