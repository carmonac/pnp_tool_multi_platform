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
            _settingsItem = new NativeMenuItem(SettingsMenuText()) { Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Meta) };
            _settingsItem.Click += (_, _) => ShowSettings();
            NativeMenu.SetMenu(this, [_aboutItem, new NativeMenuItemSeparator(), _settingsItem]);
        }
    }

    private NativeMenuItem? _aboutItem;
    private NativeMenuItem? _settingsItem;

    /// <summary>The name of the program settings, with the dots macOS gives to what opens a window.</summary>
    public static string SettingsMenuText() => StringResources.Get("ShellMenuBarItem_Configuracion_Herramienta") + "…";

    /// <summary>Puts the items of the application menu in the language the program has been changed to.</summary>
    public void Relocalize()
    {
        _aboutItem?.Header = AboutMenuText();
        _settingsItem?.Header = SettingsMenuText();
    }

    /// <summary>Opens the program settings from the application menu, when the main window is the one in use.</summary>
    private void ShowSettings()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: MainWindow { IsActive: true, DataContext: MainWindowViewModel model } })
        {
            _ = model.EditProgramSettingsAsync();
        }
    }

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
        // Before the settings set the language: a first start begins in the system's.
        string systemLanguage = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        AppError startupError = Startup.Initialize();
        bool firstRun = !startupError.HasError && FirstRunWindow.IsNeeded;
        if (firstRun)
        {
            PnpTool.Core.Localization.AppLanguage.Change(FirstRunWindow.LanguageFor(systemLanguage));
        }

        // The language is known only now.
        Relocalize();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (firstRun)
            {
                // Language, units and terms first; the program goes on when they are accepted
                // and ends when the window is closed any other way.
                var first = new FirstRunWindow();
                first.Completed += (_, _) =>
                {
                    Relocalize();
                    MainWindow main = OpenMainWindow(desktop);
                    desktop.MainWindow = main;
                    main.Show();
                };
                desktop.MainWindow = first;
                base.OnFrameworkInitializationCompleted();
                return;
            }

            desktop.MainWindow = startupError.HasError ? new MessageWindow(startupError.Message) : OpenMainWindow(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Makes the main window, with what it does once it shows and when the system hands it a file.</summary>
    private MainWindow OpenMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var started = new MainWindowViewModel();
        var main = new MainWindow { DataContext = started, StartFile = desktop.Args?.FirstOrDefault(File.Exists) };

        // Only here, so that nothing but the real program goes to the network on its own.
        main.AfterStart = () => started.CheckForUpdatesAsync();

        // macOS does not put a double-clicked file on the command line: it tells the
        // running program to open it.
        if (this.TryGetFeature<IActivatableLifetime>() is { } activatable)
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

        return main;
    }
}
