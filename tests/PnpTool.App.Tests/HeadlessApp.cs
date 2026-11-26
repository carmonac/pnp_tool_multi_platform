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
        AppPaths.PrepareUserData();
        AppSettings.Reset();
        PnpTool.Imaging.MagickImageInspector.Register();

        // No test may open a file, a folder or a web page on the machine it runs on.
        Platform.Services = new SilentPlatformServices();
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

    /// <summary>A folder for the files of a test, removed with the fixture.</summary>
    public string NewFolder() => Directory.CreateDirectory(Path.Combine(_root, "files", Guid.NewGuid().ToString("N"))).FullName;

    /// <summary>Waits for work that finishes on the user-interface thread, keeping that thread running meanwhile.</summary>
    public static void Wait(Task task, int timeoutSeconds = 60)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(2);
            Assert.True(watch.Elapsed.TotalSeconds < timeoutSeconds, "Timed out waiting for background work.");
        }

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        task.GetAwaiter().GetResult();
    }

    /// <summary>As <see cref="Wait"/>, giving the result of the work.</summary>
    public static T WaitFor<T>(Task<T> task, int timeoutSeconds = 60)
    {
        Wait(task, timeoutSeconds);
        return task.Result;
    }

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

/// <summary>
/// The tests that use the user interface share one headless session and run one after another:
/// the session, the program's folders and its language are global.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class UserInterface : ICollectionFixture<HeadlessFixture>
{
    public const string Name = "User interface";
}

/// <summary>Pictures of windows as the tests render them, kept when PNPTOOL_SCREENSHOTS names a folder.</summary>
public static class Screenshots
{
    private static readonly string? Folder = Environment.GetEnvironmentVariable("PNPTOOL_SCREENSHOTS");

    /// <summary>Renders the window, checks that something was drawn and keeps the picture if asked to.</summary>
    public static void Capture(Avalonia.Controls.Window window, string name)
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Avalonia.Media.Imaging.WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        if (Folder == null)
        {
            return;
        }

        Directory.CreateDirectory(Folder);
        using FileStream stream = File.Create(Path.Combine(Folder, name + ".png"));
#pragma warning disable CS0618 // the default PNG encoding is what is wanted here
        frame.Save(stream);
#pragma warning restore CS0618
    }
}

/// <summary>
/// Does nothing where the program would open something outside its windows. The tests that
/// check what is opened put their own recording services in its place for a while.
/// </summary>
public sealed class SilentPlatformServices : IPlatformServices
{
    public void OpenUrl(string url)
    {
    }

    public void OpenFile(string path)
    {
    }

    public void OpenFolder(string path)
    {
    }

    public void RevealFile(string path)
    {
    }
}
