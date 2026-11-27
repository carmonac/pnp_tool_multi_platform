using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using PnpTool.App.ViewModels;
using PnpTool.App.Views;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;
using PnpTool.Imaging;

namespace PnpTool.App;

public partial class App : Application
{
    private static bool _dialogKeysRegistered;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        if (!_dialogKeysRegistered)
        {
            _dialogKeysRegistered = true;
            InputElement.KeyDownEvent.AddClassHandler<Window>(CloseDialogOnKey);
        }

        // The menu named after the program, at the left of the menu bar of macOS. Without one
        // of its own the program would get Avalonia's, with "About Avalonia" in it. The items
        // to hide and quit the program are added by Avalonia after these.
        if (OperatingSystem.IsMacOS())
        {
            _aboutItem = new NativeMenuItem(AboutMenuText());
            _aboutItem.Click += (_, _) => ShowAbout();
            NativeMenu.SetMenu(this, [_aboutItem]);
        }
    }

    private NativeMenuItem? _aboutItem;

    /// <summary>
    /// "About PNP Tool", in Spanish or English as the credits themselves are; the program has
    /// no such text in its translations.
    /// </summary>
    public static string AboutMenuText() =>
        (System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName is "es" or "ca" ? "Acerca de " : "About ") + AppInfo.Name;

    /// <summary>
    /// Opens the credits over the window in use, as the button of the command bar does. While
    /// a task shows its progress, nothing is opened.
    /// </summary>
    private void ShowAbout()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        Window? owner = desktop.Windows.LastOrDefault(window => window.IsActive) ?? desktop.MainWindow;
        if (owner is { IsVisible: true } and not ProgressWindow)
        {
            _ = AboutWindow.ShowOnce(owner);
        }
    }

    /// <summary>
    /// Escape, and Command+W on macOS, close every window but the main one, as the close
    /// button of the window does: a question is left unanswered and a tool is put away.
    /// The window that shows the progress of a task has a cancel button of its own, which
    /// stops the task. The Windows version leaves this to each of its dialogs.
    /// </summary>
    private static void CloseDialogOnKey(Window window, KeyEventArgs e)
    {
        bool closes = (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
            || (e.Key == Key.W && e.KeyModifiers == KeyModifiers.Meta && OperatingSystem.IsMacOS());
        if (closes && !e.Handled && window is not (MainWindow or ProgressWindow))
        {
            e.Handled = true;
            window.Close();
        }
    }

    /// <summary>
    /// Prepares folders, log, settings and language and opens the main window. When the
    /// program cannot work (its files are missing or it cannot write its own), a message is
    /// shown instead and the program ends when it is closed.
    /// Original: <c>FuncionesGeneralesPrograma.IniciarPrograma</c>.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        MagickImageInspector.Register();
        DesktopScreenCapture.Register();
        AppError startupError = Startup.Initialize();

        // The language is known only now.
        _aboutItem?.Header = AboutMenuText();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = startupError.HasError
                ? new MessageWindow(startupError.Message)
                : new MainWindow { DataContext = new MainWindowViewModel(), StartFile = desktop.Args?.FirstOrDefault(File.Exists) };

            // macOS does not put a double-clicked file on the command line: it tells the
            // running program to open it.
            if (desktop.MainWindow is MainWindow { DataContext: MainWindowViewModel started } window)
            {
                // Only here, so that nothing but the real program goes to the network on its own.
                window.AfterStart = () => started.CheckForUpdatesAsync();
            }

            if (desktop.MainWindow is MainWindow main && this.TryGetFeature<IActivatableLifetime>() is { } activatable)
            {
                activatable.Activated += (_, e) =>
                {
                    if (e is FileActivatedEventArgs files
                        && files.Files.Select(file => file.TryGetLocalPath()).FirstOrDefault(File.Exists) is { } path)
                    {
                        main.OpenFromSystem(path);
                    }
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
