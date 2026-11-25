using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PnpTool.App.ViewModels;
using PnpTool.App.ViewModels.Ribbon;
using PnpTool.App.Views;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Primitives;

namespace PnpTool.App.Tests;

[Collection(UserInterface.Name)]
public sealed class MainWindowTests(HeadlessFixture fixture)
{
    /// <summary>Set to a folder to keep pictures of the window as the tests render it.</summary>
    private static readonly string? ScreenshotFolder = Environment.GetEnvironmentVariable("PNPTOOL_SCREENSHOTS");

    [Fact]
    public void Command_bar_has_the_five_tabs_of_the_windows_program_in_spanish()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var model = new MainWindowViewModel();

            Assert.Equal(["Archivo", "Acciones", "Configuración", "Herramientas", "Ayuda"], model.Tabs.Select(tab => tab.Header));
            Assert.Equal("Acciones", model.SelectedTab.Header);
            Assert.Equal([3, 4, 2, 3, 1], model.Tabs.Select(tab => tab.Groups.Count));
            Assert.Equal(5, model.QuickAccess.Count);
        });
    }

    [Fact]
    public void Every_button_has_an_icon_a_label_and_a_tooltip()
    {
        fixture.Run(LanguageId.EnglishUk, () =>
        {
            var model = new MainWindowViewModel();
            List<RibbonButtonViewModel> buttons = [.. model.Tabs.SelectMany(tab => tab.Groups).SelectMany(group => group.Buttons)];

            Assert.Equal(47, buttons.Count);
            Assert.Equal(43, buttons.Select(button => button.Key).Distinct().Count());   // preview and create are on three tabs
            Assert.All(buttons, button => Assert.True(button.Icon != null, button.Key + " has no icon"));
            Assert.All(model.QuickAccess, button => Assert.True(button.Icon != null, button.Key + " has no icon"));

            string[] unlabelled = [.. buttons.Where(button => button.Label.Length == 0).Select(button => button.Key)];
            Assert.True(unlabelled.Length == 0, "No label: " + string.Join(", ", unlabelled));
            Assert.All(buttons.Concat(model.QuickAccess), button => Assert.DoesNotContain(StringResources.MissingPrefix, button.Label + button.ToolTipTitle + button.ToolTipDescription));
            Assert.All(model.QuickAccess, button => Assert.NotEqual(string.Empty, button.ToolTipTitle));
        });
    }

    [Theory]
    [InlineData(LanguageId.Spanish, "Nuevo", "Paneles: 0 / Cartas: 0")]
    [InlineData(LanguageId.EnglishUk, "New", "Panels: 0 / Cards: 0")]
    [InlineData(LanguageId.German, "Neu", null)]
    public void Labels_follow_the_language(LanguageId language, string newLabel, string? counts)
    {
        fixture.Run(language, () =>
        {
            var model = new MainWindowViewModel();

            Assert.Equal(newLabel, model.Tabs[0].Groups[0].Buttons[0].Label);
            if (counts != null)
            {
                Assert.Equal(counts, model.CountsText);
            }
        });
    }

    [Fact]
    public void Button_without_function_yet_says_so_in_the_status_line()
    {
        fixture.Run(LanguageId.EnglishUk, () =>
        {
            var model = new MainWindowViewModel();
            RibbonButtonViewModel create = model.Tabs[0].Groups[2].Buttons[1];

            create.Command.Execute(null);

            Assert.StartsWith(create.ToolTipTitle, model.StatusText);
            Assert.Contains("not available yet", model.StatusText);
        });
    }

    [Fact]
    public void Help_buttons_open_their_web_pages()
    {
        var opened = new List<string>();
        IPlatformServices original = Platform.Services;
        try
        {
            Platform.Services = new RecordingServices(opened);
            fixture.Run(LanguageId.EnglishUk, () =>
            {
                var model = new MainWindowViewModel();
                foreach (RibbonButtonViewModel button in model.Tabs[4].Groups[0].Buttons)
                {
                    button.Command.Execute(null);
                }
            });
        }
        finally
        {
            Platform.Services = original;
        }

        Assert.Equal(4, opened.Count);
        Assert.All(opened, url => Assert.StartsWith("https://", url));
        Assert.Contains(opened, url => url.Contains("youtube.com"));
        Assert.Contains(opened, url => url.Contains("boardgamegeek.com/thread"));
    }

    [Theory]
    [InlineData(LanguageId.Spanish, "es")]
    [InlineData(LanguageId.EnglishUk, "en")]
    public void Window_shows_tabs_buttons_and_status_line(LanguageId language, string name)
    {
        fixture.Run(language, () =>
        {
            AppSettings.StartMaximized = false;
            AppSettings.WindowSize = new PointI(1280, 800);
            var window = new MainWindow { DataContext = new MainWindowViewModel() };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var tabs = window.FindControl<TabControl>("CommandBar")!;
            Assert.Equal(5, tabs.ItemCount);
            Assert.Equal(1, tabs.SelectedIndex);
            Assert.Equal(5, window.FindControl<ItemsControl>("QuickAccessBar")!.ItemCount);
            Assert.Contains(": 0", window.FindControl<TextBlock>("CountsLabel")!.Text);
            Assert.Equal(1280, window.Width);

            WriteableBitmap? frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            Assert.True(frame.PixelSize.Width >= 1280);
            if (ScreenshotFolder != null)
            {
                Directory.CreateDirectory(ScreenshotFolder);
                using FileStream stream = File.Create(Path.Combine(ScreenshotFolder, "ventana-" + name + ".png"));
#pragma warning disable CS0618 // the default PNG encoding is what is wanted here
                frame.Save(stream);
#pragma warning restore CS0618
            }

            // The tab with most buttons still shows them all.
            var model = (MainWindowViewModel)window.DataContext!;
            model.SelectedTab = model.Tabs[3];
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(3, tabs.SelectedIndex);
            if (ScreenshotFolder != null && window.CaptureRenderedFrame() is { } toolsFrame)
            {
                using FileStream stream = File.Create(Path.Combine(ScreenshotFolder, "ventana-" + name + "-herramientas.png"));
#pragma warning disable CS0618
                toolsFrame.Save(stream);
#pragma warning restore CS0618
            }

            window.Close();
        });
    }

    [Fact]
    public void Window_size_is_remembered_when_it_closes()
    {
        fixture.Run(LanguageId.EnglishUk, () =>
        {
            AppSettings.StartMaximized = false;
            AppSettings.WindowSize = new PointI(1000, 700);
            var window = new MainWindow { DataContext = new MainWindowViewModel() };
            window.Show();
            Assert.Equal(1000, window.Width);
            Assert.Equal(700, window.Height);

            window.Width = 1100;
            window.Height = 640;
            window.Close();

            Assert.Equal(1100, AppSettings.WindowSize.X);
            Assert.Equal(640, AppSettings.WindowSize.Y);
            Assert.False(AppSettings.StartMaximized);
            Assert.True(File.Exists(AppPaths.SettingsFile));
        });
    }

    [Fact]
    public void Saved_size_that_is_too_small_is_ignored()
    {
        fixture.Run(LanguageId.EnglishUk, () =>
        {
            AppSettings.StartMaximized = false;
            AppSettings.WindowSize = new PointI(200, 100);
            var window = new MainWindow { DataContext = new MainWindowViewModel() };

            Assert.Equal(1280, window.Width);
            Assert.Equal(800, window.Height);
        });
    }

    [Fact]
    public void Startup_problem_is_shown_in_a_message_window()
    {
        fixture.Run(LanguageId.EnglishUk, () =>
        {
            var window = new MessageWindow("Assets folder does not exist");
            window.Show();

            Assert.Equal("Assets folder does not exist", window.FindControl<TextBlock>("MessageText")!.Text);
            window.Close();
        });
    }

    private sealed class RecordingServices(List<string> opened) : IPlatformServices
    {
        public void OpenUrl(string url) => opened.Add(url);

        public void OpenFile(string path) => opened.Add(path);

        public void OpenFolder(string path) => opened.Add(path);
    }
}
