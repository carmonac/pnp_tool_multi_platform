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
                Assert.Equal(["ShellMenuBarItem_Ayuda_Telegram", MainWindowViewModel.AboutButtonKey, "ShellMenuBarItem_Configuracion_Herramienta"], buttons.Select(button => button.Key));
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
    /// <summary>Answers instead of the network with a list of releases.</summary>
    private sealed class FakeReleases(string body, List<string> requests) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requests.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    private const string ReleasePage = "https://github.com/carmonac/pnp_tool_multi_platform/releases/tag/v99.0.0";

    private const string OneRelease = "[{\"tag_name\":\"v99.0.0\",\"html_url\":\"" + ReleasePage + "\",\"prerelease\":false,\"draft\":false}]";

    [Fact]
    public void Update_notifications_are_chosen_and_kept_in_the_about_window()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            AppSettings.UpdateNotifications = UpdateChannel.Minor;
            var window = new AboutWindow();
            window.Show();
            var box = window.FindControl<ComboBox>("UpdateChannelBox")!;
            Assert.Equal(["Ninguna", "Mayor", "Menor", "Test"], window.Model.UpdateChannels.Select(option => option.Text));
            Assert.Equal(UpdateChannel.Minor, ((UpdateChannelOption)box.SelectedItem!).Channel);
            Assert.Equal("Avisar actualización", window.Model.UpdateLabel);
            Assert.Equal("Buscar", window.FindControl<Button>("CheckUpdatesButton")!.Content);

            box.SelectedIndex = 0;

            Assert.Equal(UpdateChannel.None, AppSettings.UpdateNotifications);
            AppSettings.UpdateNotifications = UpdateChannel.Tester;
            Assert.False(AppSettings.Load().HasError);
            Assert.Equal(UpdateChannel.None, AppSettings.UpdateNotifications);
            Screenshots.Capture(window, "ventana-acerca-de-actualizaciones");
            window.Close();
            AppSettings.UpdateNotifications = UpdateChannel.Minor;
            AppSettings.Save();
        });
    }

    [Fact]
    public void Search_button_tells_of_a_newer_version_and_opens_its_page()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var opened = new List<string>();
            var requests = new List<string>();
            IPlatformServices before = Platform.Services;
            Platform.Services = new RecordingServices(opened);
            try
            {
                var window = new AboutWindow();
                window.Model.NewUpdateChecker = () => new PnpTool.Core.Online.UpdateChecker(new FakeReleases(OneRelease, requests));
                window.Show();
                Assert.False(window.FindControl<Button>("OpenUpdateButton")!.IsVisible);

                HeadlessFixture.Wait(window.Model.CheckForUpdatesAsync());
                Dispatcher.UIThread.RunJobs();

                Assert.Single(requests);
                Assert.Equal(AppInfo.Version + " → 99.0.0", window.FindControl<TextBlock>("UpdateStatusText")!.Text);
                Assert.True(window.FindControl<Button>("OpenUpdateButton")!.IsVisible);
                window.FindControl<Button>("OpenUpdateButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal([ReleasePage], opened);

                // Nothing newer: the text of the original.
                window.Model.NewUpdateChecker = () => new PnpTool.Core.Online.UpdateChecker(new FakeReleases("[]", requests));
                HeadlessFixture.Wait(window.Model.CheckForUpdatesAsync());
                Assert.Equal("No hay actualizaciones disponibles", window.Model.UpdateStatus);
                Assert.False(window.Model.HasUpdate);
                window.Close();
            }
            finally
            {
                Platform.Services = before;
            }
        });
    }

    [Fact]
    public void At_startup_a_newer_version_is_offered_and_the_setting_none_keeps_quiet()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var opened = new List<string>();
            var requests = new List<string>();
            var questions = new List<string>();
            IPlatformServices before = Platform.Services;
            Platform.Services = new RecordingServices(opened);
            try
            {
                bool answer = false;
                var model = new MainWindowViewModel
                {
                    NewUpdateChecker = () => new PnpTool.Core.Online.UpdateChecker(new FakeReleases(OneRelease, requests)),
                    Ask = (question, _) =>
                    {
                        questions.Add(question);
                        return Task.FromResult<bool?>(answer);
                    },
                };

                // Turned off: the network is not even asked.
                AppSettings.UpdateNotifications = UpdateChannel.None;
                HeadlessFixture.Wait(model.CheckForUpdatesAsync());
                Assert.Empty(requests);
                Assert.Empty(questions);

                // Declined: nothing opens.
                AppSettings.UpdateNotifications = UpdateChannel.Minor;
                HeadlessFixture.Wait(model.CheckForUpdatesAsync());
                Assert.Equal(["Nueva versión disponible. ¿Actualizar?" + Environment.NewLine + AppInfo.Version + " → 99.0.0"], questions);
                Assert.Empty(opened);

                // Accepted: the page of the release opens in the browser; nothing is downloaded.
                answer = true;
                HeadlessFixture.Wait(model.CheckForUpdatesAsync());
                Assert.Equal([ReleasePage], opened);

                // Up to date or no connection: no question.
                questions.Clear();
                model.NewUpdateChecker = () => new PnpTool.Core.Online.UpdateChecker(new FakeReleases("[]", requests));
                HeadlessFixture.Wait(model.CheckForUpdatesAsync());
                model.NewUpdateChecker = () => new PnpTool.Core.Online.UpdateChecker(new FakeReleases("<html>", requests));
                HeadlessFixture.Wait(model.CheckForUpdatesAsync());
                Assert.Empty(questions);
            }
            finally
            {
                Platform.Services = before;
                AppSettings.UpdateNotifications = UpdateChannel.Minor;
            }
        });
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
