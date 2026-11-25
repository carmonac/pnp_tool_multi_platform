using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PnpTool.App.ViewModels;
using PnpTool.App.Views;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Folders, log and saved settings. Until the error dialog exists (main window task),
        // a failure is written to the console and the program carries on.
        PnpTool.Imaging.MagickImageInspector.Register();
        AppError startupError = Startup.Initialize();
        if (startupError.HasError)
        {
            Console.Error.WriteLine(startupError.Message);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = new MainWindowViewModel() };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
