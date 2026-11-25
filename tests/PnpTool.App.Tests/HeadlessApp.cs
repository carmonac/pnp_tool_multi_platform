using Avalonia;
using Avalonia.Headless;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;

namespace PnpTool.App.Tests;

/// <summary>The application with its styles and resources but without its start-up: no folders, settings or windows.</summary>
public sealed class TestApp : App
{
    public override void OnFrameworkInitializationCompleted()
    {
    }

    /// <summary>Used by the headless session to build the application. Drawing is real (Skia), into memory.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont();
}

/// <summary>
/// One headless Avalonia session for all the tests, with the program pointed at the bundled
/// assets and at a scratch user folder.
/// </summary>
public sealed class HeadlessFixture : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("pnptool-app-").FullName;

    public HeadlessFixture()
    {
        AppPaths.Initialize(AppPaths.FindAssetsFolder(AppContext.BaseDirectory), Path.Combine(_root, "user"));
        Directory.CreateDirectory(AppPaths.UserDataFolder);
        AppSettings.Reset();
        Session = HeadlessUnitTestSession.StartNew(typeof(TestApp));
    }

    public HeadlessUnitTestSession Session { get; }

    /// <summary>Runs something on the user-interface thread in the given language.</summary>
    public void Run(LanguageId language, Action action) =>
        Session.Dispatch(
            () =>
            {
                AppLanguage.Change(new SupportedLanguage(language));
                action();
            },
            CancellationToken.None).GetAwaiter().GetResult();

    public void Dispose()
    {
        Session.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
