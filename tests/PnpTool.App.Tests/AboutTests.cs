using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PnpTool.App.ViewModels;
using PnpTool.App.ViewModels.Ribbon;
using PnpTool.App.Views;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;

namespace PnpTool.App.Tests;

[Collection(UserInterface.Name)]
public sealed class AboutTests(HeadlessFixture fixture)
{
    private sealed class RecordingServices(List<string> opened) : IPlatformServices
    {
        public void OpenUrl(string url) => opened.Add(url);

        public void OpenFile(string path) => opened.Add(path);

        public void OpenFolder(string path) => opened.Add(path);

        public void RevealFile(string path) => opened.Add(path);
    }

    private static List<string> Texts(AboutWindow window) =>
        [.. window.FindControl<StackPanel>("PageContent")!.GetLogicalDescendantsText()];

    [Fact]
    public void Pages_are_in_spanish_or_english_and_never_name_the_printing_company()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var about = new AboutViewModel();
            Assert.Equal(["about", "terms", "team", "translators", "patrons"], about.Pages.Select(page => page.Key));
            Assert.Equal(["PNP Tool", "Términos y condiciones", "PNP Tool Team", "Traductores", "Mecenas"], about.Pages.Select(page => page.Title));
            Assert.Contains("para macOS y Linux", about.Pages[0].Sections[0].Heading);
            Assert.Contains(AppInfo.Version, about.Pages[0].Sections[0].Heading);
            Assert.All(about.Pages[3].Sections, section => Assert.True(File.Exists(section.FlagPath), section.Heading));

            IEnumerable<string> everything = about.Pages.SelectMany(page =>
                page.Sections.SelectMany(section => section.Lines.Append(section.Heading ?? string.Empty)).Append(page.Title));
            Assert.DoesNotContain(everything, text => text.Contains("soriano", StringComparison.OrdinalIgnoreCase));
        });

        fixture.Run(LanguageId.EnglishUk, () =>
        {
            var about = new AboutViewModel();
            Assert.Equal(["PNP Tool", "Terms and conditions", "PNP Tool Team", "Translators", "Patrons"], about.Pages.Select(page => page.Title));
            Assert.Equal("Thank you all!", about.Pages[4].Closing);
        });
    }

    [Fact]
    public void Window_shows_the_page_chosen_and_its_links_open_in_the_browser()
    {
        var opened = new List<string>();
        IPlatformServices original = Platform.Services;
        try
        {
            Platform.Services = new RecordingServices(opened);
            fixture.Run(LanguageId.Spanish, () =>
            {
                var window = new AboutWindow();
                window.Show();
                Dispatcher.UIThread.RunJobs();

                Assert.Contains(Texts(window), text => text.Contains("Jupklass"));
                Grid source = window.FindControl<StackPanel>("PageContent")!.Children.OfType<Grid>().Single();
                Assert.Equal("GitHubMark", Assert.IsType<Avalonia.Controls.Shapes.Path>(source.Children[0]).Name);
                string sourceText = Assert.IsType<TextBlock>(source.Children[1]).Text!;
                Assert.Equal("Código fuente: " + AppLinks.SourceCode, sourceText);
                window.Model.OpenLink(AboutViewModel.LinkIn(sourceText)!);
                Screenshots.Capture(window, "ventana-acerca-de");

                window.FindControl<ListBox>("PageList")!.SelectedIndex = 3;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("translators", window.Model.Selected.Key);
                Assert.Contains("Traductores", Texts(window));
                Assert.Contains("Desgardel / Griffin Dark / Mister Digdug", Texts(window));
                Assert.DoesNotContain(Texts(window), text => text.Contains("Jupklass"));
                Screenshots.Capture(window, "ventana-acerca-de-traductores");

                window.Model.Selected = window.Model.Pages[1];
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(1, window.FindControl<ListBox>("PageList")!.SelectedIndex);
                TextBlock link = window.FindControl<StackPanel>("PageContent")!.Children.OfType<TextBlock>().Single(text => text.Classes.Contains("link"));
                Assert.EndsWith(AppLinks.AdobeIccLicence, link.Text);
                window.Model.OpenLink(AboutViewModel.LinkIn(link.Text!)!);

                window.FindControl<Button>("DonateButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.FindControl<Button>("TelegramButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.FindControl<Button>("CloseButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.False(window.IsVisible);
            });
        }
        finally
        {
            Platform.Services = original;
        }

        Assert.Equal([AppLinks.SourceCode, AppLinks.AdobeIccLicence, AppLinks.Donate, AppLinks.Telegram], opened);
    }

    [Fact]
    public void Help_tab_opens_the_channel_and_the_credits()
    {
        var opened = new List<string>();
        IPlatformServices original = Platform.Services;
        try
        {
            Platform.Services = new RecordingServices(opened);
            fixture.Run(LanguageId.Spanish, () =>
            {
                var model = new MainWindowViewModel();
                AboutViewModel? shown = null;
                model.ShowAbout = about =>
                {
                    shown = about;
                    return Task.CompletedTask;
                };

                IReadOnlyList<RibbonButtonViewModel> buttons = model.Tabs[4].Groups[1].Buttons;
                Assert.Equal(["ShellMenuBarItem_Ayuda_Telegram", MainWindowViewModel.AboutButtonKey], buttons.Select(button => button.Key));
                Assert.Equal(AppInfo.Name, buttons[1].Label);
                Assert.NotNull(buttons[0].Icon);
                Assert.NotNull(buttons[1].Icon);

                buttons[0].Command.Execute(null);
                buttons[1].Command.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.NotNull(shown);
            });
        }
        finally
        {
            Platform.Services = original;
        }

        Assert.Equal([AppLinks.Telegram], opened);
    }
}

internal static class AboutTestExtensions
{
    /// <summary>The text of every text block inside a panel, rows of several included.</summary>
    public static IEnumerable<string> GetLogicalDescendantsText(this Panel panel)
    {
        foreach (Control child in panel.Children)
        {
            if (child is TextBlock text)
            {
                yield return text.Text ?? string.Empty;
            }
            else if (child is Panel inner)
            {
                foreach (string found in inner.GetLogicalDescendantsText())
                {
                    yield return found;
                }
            }
        }
    }
}
