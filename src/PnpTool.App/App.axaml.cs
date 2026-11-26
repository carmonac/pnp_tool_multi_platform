using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PnpTool.App.ViewModels;
using PnpTool.App.Views;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;
using PnpTool.Imaging;

namespace PnpTool.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Prepares folders, log, settings and language and opens the main window. When the
    /// program cannot work (its files are missing or it cannot write its own), a message is
    /// shown instead and the program ends when it is closed.
    /// Original: <c>FuncionesGeneralesPrograma.IniciarPrograma</c>.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        MagickImageInspector.Register();
        AppError startupError = Startup.Initialize();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = startupError.HasError
                ? new MessageWindow(startupError.Message)
                : new MainWindow { DataContext = new MainWindowViewModel(), StartFile = desktop.Args?.FirstOrDefault(File.Exists) };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
