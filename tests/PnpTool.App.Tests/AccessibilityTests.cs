using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PnpTool.App.Services;
using PnpTool.App.ViewModels;
using PnpTool.App.Views;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Primitives;

namespace PnpTool.App.Tests;

/// <summary>What assistive technology is told of the main window, and the icons on the dark theme.</summary>
[Collection(UserInterface.Name)]
public sealed class AccessibilityTests(HeadlessFixture fixture)
{
    private static MainWindow Open()
    {
        AppSettings.StartMaximized = false;
        AppSettings.WindowSize = new PointI(1280, 800);
        var window = new MainWindow { DataContext = new MainWindowViewModel() };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [Fact]
    public void Buttons_of_the_quick_access_bar_and_of_the_command_bar_have_names()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            MainWindow window = Open();

            Button[] quick = [.. window.FindControl<ItemsControl>("QuickAccessBar")!.GetVisualDescendants().OfType<Button>()];
            Assert.Equal(5, quick.Length);
            Assert.Equal(["Previsualizar", "Generar fichero salida", "Abrir", "Guardar"], quick.Take(4).Select(AutomationProperties.GetName));
            Assert.All(quick, button => Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(button))));

            Button[] ribbon = [.. window.FindControl<TabControl>("CommandBar")!.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("ribbon"))];
            Assert.NotEmpty(ribbon);
            Assert.All(ribbon, button => Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(button))));
            Button swatch = window.FindControl<TabControl>("CommandBar")!.GetVisualDescendants().OfType<Button>().First(button => button.Classes.Contains("swatch") && button.IsVisible);
            Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(swatch)));
            window.Close();
        });
    }

    [Fact]
    public void Pressing_a_tab_through_the_accessibility_interface_selects_it()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            MainWindow window = Open();
            var model = (MainWindowViewModel)window.DataContext!;
            TabItem[] tabs = [.. window.FindControl<TabControl>("CommandBar")!.GetVisualDescendants().OfType<TabItem>()];
            Assert.Equal(5, tabs.Length);
            Assert.All(tabs, tab => Assert.IsType<RibbonTabItem>(tab));
            Assert.Equal("Acciones", model.SelectedTab!.Header);

            AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(tabs[3]);
            Assert.Equal(AutomationControlType.TabItem, peer.GetAutomationControlType());
            Assert.IsAssignableFrom<ISelectionItemProvider>(peer);
            Assert.IsAssignableFrom<IInvokeProvider>(peer).Invoke();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Herramientas", model.SelectedTab!.Header);
            Assert.True(tabs[3].IsSelected);
            Assert.False(tabs[1].IsSelected);
            window.Close();
        });
    }

    [Fact]
    public void Picture_only_buttons_are_named_after_their_tooltip()
    {
        fixture.Run(LanguageId.EnglishUk, () =>
        {
            var icon = new Button { Content = new Image() };
            ToolTip.SetTip(icon, "Swap width and height");
            AccessibleNames.Name(icon);
            Assert.Equal("Swap width and height", AutomationProperties.GetName(icon));

            // A name already given stays, and so does a button that has its own text.
            var named = new Button { Content = new Image() };
            AutomationProperties.SetName(named, "Mine");
            ToolTip.SetTip(named, "Other");
            AccessibleNames.Name(named);
            Assert.Equal("Mine", AutomationProperties.GetName(named));

            var text = new Button { Content = "Search" };
            ToolTip.SetTip(text, "Looks for cards");
            AccessibleNames.Name(text);
            Assert.True(string.IsNullOrEmpty(AutomationProperties.GetName(text)));

            var toggle = new ToggleButton { Content = new Image() };
            ToolTip.SetTip(toggle, "Keep the proportion");
            AccessibleNames.Name(toggle);
            Assert.Equal("Keep the proportion", AutomationProperties.GetName(toggle));

            // In a window, buttons are named as they appear.
            AccessibleNames.Register();
            AccessibleNames.Register();
            var owner = new Window();
            owner.Show();
            var window = new FaceSetupWindow();
            _ = window.ShowDialog(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(window.FindControl<ToggleButton>("RatioButton")!)));
            Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(window.FindControl<PnpTool.Controls.OkCancelButtons>("Buttons")!.OkButton)));
            window.Close();
            owner.Close();
        });
    }

    [Fact]
    public void Dark_theme_draws_a_light_halo_round_the_icons()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            MainWindow window = Open();
            Image icon = window.FindControl<ItemsControl>("QuickAccessBar")!.GetVisualDescendants().OfType<Image>().First();

            window.RequestedThemeVariant = ThemeVariant.Light;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0.0, Assert.IsAssignableFrom<Avalonia.Media.IDropShadowEffect>(icon.Effect).Opacity);
            Screenshots.Capture(window, "ventana-tema-claro");

            window.RequestedThemeVariant = ThemeVariant.Dark;
            Dispatcher.UIThread.RunJobs();
            var halo = Assert.IsAssignableFrom<Avalonia.Media.IDropShadowEffect>(icon.Effect);
            Assert.True(halo.Opacity > 0.5);
            Assert.Equal(Avalonia.Media.Colors.White, halo.Color);
            Screenshots.Capture(window, "ventana-tema-oscuro");

            // The tabs with the darkest icons: new and save as, and the settings.
            var model = (MainWindowViewModel)window.DataContext!;
            model.SelectedTab = model.Tabs[0];
            Dispatcher.UIThread.RunJobs();
            Screenshots.Capture(window, "ventana-tema-oscuro-archivo");
            model.SelectedTab = model.Tabs[2];
            Dispatcher.UIThread.RunJobs();
            Screenshots.Capture(window, "ventana-tema-oscuro-configuracion");
            window.Close();
        });
    }
}
