using Avalonia;

namespace PnpTool.App;

internal static class Program
{
    // Avalonia configuration; also used by the visual designer. Do not touch
    // Avalonia APIs before AppMain is called.
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
