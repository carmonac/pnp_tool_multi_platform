using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using PnpTool.App.Services;
using PnpTool.App.ViewModels;
using PnpTool.App.Views;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Primitives;

namespace PnpTool.App.Tests;

/// <summary>Where the main window opens, and the menu bar of macOS.</summary>
[Collection(UserInterface.Name)]
public sealed class AppShellTests(HeadlessFixture fixture)
{
    private static readonly WorkArea Laptop = new(new PixelRect(0, 25, 1440, 875), 1.0);
    private static readonly WorkArea Monitor = new(new PixelRect(1440, 0, 2560, 1440), 1.0);
    private static readonly Size Window = new(1280, 800);

    /// <summary>The items of a menu that do something: a separator is a kind of item too.</summary>
    private static List<NativeMenuItem> Commands(NativeMenuItem menu) =>
        [.. menu.Menu!.Items.OfType<NativeMenuItem>().Where(item => item is not NativeMenuItemSeparator)];

    [Fact]
    public void Window_goes_back_to_the_monitor_it_was_on()
    {
        Assert.Equal(Monitor, WindowPlacement.ScreenFor(new PixelPoint(2000, 100), Window, [Laptop, Monitor]));
        Assert.Equal(Laptop, WindowPlacement.ScreenFor(new PixelPoint(60, 40), Window, [Laptop, Monitor]));

        // Astride both, it belongs to the one that shows more of its title bar.
        Assert.Equal(Laptop, WindowPlacement.ScreenFor(new PixelPoint(700, 100), Window, [Laptop, Monitor]));
        Assert.Equal(Monitor, WindowPlacement.ScreenFor(new PixelPoint(900, 100), Window, [Laptop, Monitor]));
    }

    [Fact]
    public void Window_whose_monitor_is_gone_has_no_place()
    {
        Assert.Null(WindowPlacement.ScreenFor(new PixelPoint(2000, 100), Window, [Laptop]));
        Assert.Null(WindowPlacement.ScreenFor(new PixelPoint(2000, 100), Window, []));
    }

    [Theory]
    [InlineData(-1160, 100, true)] // 120 of its title bar still in sight at the left
    [InlineData(-1200, 100, false)] // only 80
    [InlineData(1320, 100, true)] // 120 at the right
    [InlineData(1350, 100, false)]
    [InlineData(100, 0, false)] // the title bar under the menu bar of the system
    [InlineData(100, 25, true)]
    [InlineData(100, 880, false)] // the title bar cut by the bottom of the screen
    public void Window_must_show_enough_of_its_title_bar_to_be_reached(int x, int y, bool reachable) =>
        Assert.Equal(reachable, WindowPlacement.ScreenFor(new PixelPoint(x, y), Window, [Laptop]) != null);

    [Fact]
    public void Sizes_are_measured_in_the_scale_of_each_screen()
    {
        // A screen of 2880 x 1800 pixels that shows everything twice as large.
        var retina = new WorkArea(new PixelRect(0, 50, 2880, 1750), 2.0);

        Assert.NotNull(WindowPlacement.ScreenFor(new PixelPoint(2640, 60), Window, [retina])); // 240 pixels are 120 units...
        Assert.Null(WindowPlacement.ScreenFor(new PixelPoint(2660, 60), Window, [retina])); // ...and 220 pixels only 110
        Assert.True(WindowPlacement.Fits(new Size(1440, 875), retina));
        Assert.False(WindowPlacement.Fits(new Size(1441, 875), retina));
        Assert.False(WindowPlacement.Fits(new Size(1440, 876), retina));
    }

    [Fact]
    public void Window_place_is_remembered_when_it_closes_and_used_when_it_opens()
    {
        fixture.Run(LanguageId.EnglishUk, () =>
        {
            AppSettings.StartMaximized = false;
            AppSettings.WindowSize = new PointI(1000, 700);
            AppSettings.WindowPosition = null;
            var window = new MainWindow { DataContext = new MainWindowViewModel() };
            Assert.Equal(WindowStartupLocation.CenterScreen, window.WindowStartupLocation);
            window.Show();
            window.Position = new PixelPoint(140, 90);
            window.Close();

            Assert.Equal(140, AppSettings.WindowPosition?.X);
            Assert.Equal(90, AppSettings.WindowPosition?.Y);

            var again = new MainWindow { DataContext = new MainWindowViewModel() };
            Assert.Equal(WindowStartupLocation.Manual, again.WindowStartupLocation);
            again.Show();
            Assert.Equal(new PixelPoint(140, 90), again.Position);
            again.Close();
        });
    }

    [Fact]
    public void Window_opens_in_the_middle_when_its_place_is_on_no_screen()
    {
        fixture.Run(LanguageId.EnglishUk, () =>
        {
            AppSettings.StartMaximized = false;
            AppSettings.WindowSize = new PointI(1000, 700);
            AppSettings.WindowPosition = new PointI(-30000, -30000);
            var window = new MainWindow { DataContext = new MainWindowViewModel() };

            Assert.Equal(WindowStartupLocation.CenterScreen, window.WindowStartupLocation);
            window.Show();
            Assert.NotEqual(new PixelPoint(-30000, -30000), window.Position);
            Assert.Equal(1000, window.Width);
            window.Close();
        });
    }

    [Fact]
    public void Menu_bar_has_a_menu_for_each_tab_with_its_buttons()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var model = new MainWindowViewModel();
            NativeMenu bar = MenuBar.Build(model);

            List<NativeMenuItem> menus = [.. bar.Items.OfType<NativeMenuItem>()];
            Assert.Equal(["Archivo", "Acciones", "Configuración", "Herramientas", "Ayuda"], menus.Select(menu => menu.Header));

            List<NativeMenuItemBase> file = [.. menus[0].Menu!.Items];
            List<NativeMenuItem> fileItems = Commands(menus[0]);
            Assert.Equal(7, fileItems.Count);
            Assert.Equal(2, file.OfType<NativeMenuItemSeparator>().Count());
            Assert.Equal(new KeyGesture(Key.N, KeyModifiers.Meta), fileItems[0].Gesture);
            Assert.Equal(new KeyGesture(Key.O, KeyModifiers.Meta), fileItems[1].Gesture);
            Assert.Equal(new KeyGesture(Key.S, KeyModifiers.Meta), fileItems[2].Gesture);
            Assert.Equal(new KeyGesture(Key.S, KeyModifiers.Meta | KeyModifiers.Shift), fileItems[3].Gesture);
            Assert.Same(model.Tabs[0].Groups[0].Buttons[0].Command, fileItems[0].Command);

            // The buttons that make the PDF are in three tabs and in one menu.
            int inTabs = model.Tabs.Sum(tab => tab.Groups.Sum(group => group.Buttons.Count));
            int inMenus = menus.Sum(menu => Commands(menu).Count);
            Assert.Equal(inTabs - 4, inMenus);

            // No text is empty or broken in lines.
            Assert.All(menus.SelectMany(Commands), item =>
            {
                Assert.False(string.IsNullOrWhiteSpace(item.Header));
                Assert.DoesNotContain('\n', item.Header!);
            });
        });
    }

    [Fact]
    public void Menu_item_that_cannot_be_used_is_disabled_like_its_button()
    {
        fixture.Run(LanguageId.EnglishUk, () =>
        {
            var model = new MainWindowViewModel();
            NativeMenu bar = MenuBar.Build(model);
            NativeMenuItem actions = bar.Items.OfType<NativeMenuItem>().ElementAt(1);

            // Changing the fronts needs cards in the panel.
            var button = model.Tabs[1].Groups[1].Buttons[0];
            NativeMenuItem item = Commands(actions).Single(entry => ReferenceEquals(entry.Command, button.Command));
            Assert.False(button.Command.CanExecute(null));
            Assert.False(item.IsEnabled);
        });
    }

    [Fact]
    public void About_item_is_named_after_the_program_in_spanish_or_english()
    {
        fixture.Run(LanguageId.Spanish, () => Assert.Equal("Acerca de PNP Tool", App.AboutMenuText()));
        fixture.Run(LanguageId.EnglishUk, () => Assert.Equal("About PNP Tool", App.AboutMenuText()));
        fixture.Run(LanguageId.Dutch, () => Assert.Equal("About PNP Tool", App.AboutMenuText()));
    }

    [Fact]
    public void About_window_is_opened_once()
    {
        fixture.Run(LanguageId.EnglishUk, () =>
        {
            AppSettings.StartMaximized = false;
            var main = new MainWindow { DataContext = new MainWindowViewModel() };
            main.Show();

            _ = AboutWindow.ShowOnce(main);
            _ = AboutWindow.ShowOnce(main);
            List<AboutWindow> open = [.. main.OwnedWindows.OfType<AboutWindow>()];
            Assert.Single(open);

            open[0].Close();
            _ = AboutWindow.ShowOnce(main);
            AboutWindow reopened = Assert.Single(main.OwnedWindows.OfType<AboutWindow>());
            Assert.NotSame(open[0], reopened);
            reopened.Close();
            main.Close();
        });
    }
}
