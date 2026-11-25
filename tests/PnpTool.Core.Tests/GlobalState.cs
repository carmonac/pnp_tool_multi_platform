using PnpTool.Core.Infrastructure;

namespace PnpTool.Core.Tests;

/// <summary>
/// Tests that change the program's static settings, folders or log join this collection so
/// they do not run at the same time as each other.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalState
{
    public const string Name = "Global state";
}

/// <summary>
/// Remembers the static state when created and puts it back when disposed, so one test
/// cannot change what another sees.
/// </summary>
public sealed class GlobalStateScope : IDisposable
{
    private readonly StoredAppSettings _settings = new();
    private readonly string _assets = AppPaths.AssetsFolder;
    private readonly string _userData = AppPaths.UserDataFolder;
    private readonly string _images = AppSettings.ImagesFolder;
    private readonly string _advanced = AppSettings.AdvancedStylesFolder;
    private readonly string _temp = AppSettings.TempFolder;
    private readonly string _last = AppSettings.LastFolder;
    private readonly string _rgb = AppSettings.RgbIccPath;
    private readonly string _cmyk = AppSettings.CmykIccPath;

    public void Dispose()
    {
        Log.Stop();
        Log.Sink = null;
        AppPaths.Initialize(_assets, _userData);
        _settings.CopyToAppSettings();
        AppSettings.ImagesFolder = _images;
        AppSettings.AdvancedStylesFolder = _advanced;
        AppSettings.TempFolder = _temp;
        AppSettings.LastFolder = _last;
        AppSettings.RgbIccPath = _rgb;
        AppSettings.CmykIccPath = _cmyk;
    }
}
