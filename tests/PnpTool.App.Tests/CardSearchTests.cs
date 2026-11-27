using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Threading;
using ImageMagick;
using PnpTool.App.ViewModels;
using PnpTool.App.Views;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Online;

namespace PnpTool.App.Tests;

[Collection(UserInterface.Name)]
public sealed class CardSearchTests(HeadlessFixture fixture)
{
    /// <summary>A Scryfall that knows two cards and serves a real picture for each.</summary>
    private sealed class FakeServer : HttpMessageHandler
    {
        private readonly byte[] _picture;

        public FakeServer()
        {
            using var image = new MagickImage(new MagickColor("#3060C0"), 150, 210) { Format = MagickFormat.Png };
            _picture = image.ToByteArray();
        }

        public bool Offline { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Offline)
            {
                throw new HttpRequestException("No network");
            }

            string url = Uri.UnescapeDataString(request.RequestUri!.ToString());
            if (url.StartsWith("https://img.test/", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_picture) });
            }

            string? name = url.Contains("q=Lightning Bolt") ? "Lightning Bolt" : url.Contains("q=Shock") && url.Contains("lang:en") ? "Shock" : null;
            if (url.Contains("q=set:m10"))
            {
                return Json("{\"total_cards\":2,\"has_more\":false,\"data\":[" + Card("Lightning Bolt", "146") + "," + Card("Shock", "155") + "]}");
            }

            return name == null
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") })
                : Json("{\"total_cards\":1,\"has_more\":false,\"data\":[" + Card(name, "1") + "]}");
        }

        private static string Card(string name, string number) =>
            $"{{\"name\":\"{name}\",\"collector_number\":\"{number}\",\"image_uris\":{{\"png\":\"https://img.test/{number}.png\"}}}}";

        private static Task<HttpResponseMessage> Json(string json) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }

    private static string Deck(string folder)
    {
        string deck = Path.Combine(folder, "mazo.txt");
        File.WriteAllLines(deck, ["4 Lightning Bolt", "2 Shock", "1 Unknown Card"]);
        return deck;
    }

    [Fact]
    public void A_deck_is_downloaded_and_listed_with_how_each_card_ended()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            var search = new CardSearchViewModel(new FakeServer()) { Folder = string.Empty };
            var notices = new List<string>();
            search.Notice += notices.Add;

            HeadlessFixture.Wait(search.DownloadAsync());                 // no folder
            Assert.Equal([StringResources.Get("faltan_datos")], notices);
            search.Folder = folder;
            Assert.Equal("No file is selected", search.Problem());
            search.FromDeck = false;
            Assert.True(search.FromSet);
            Assert.Equal("Set missing", search.Problem());
            search.FromDeck = true;
            search.DeckFile = Deck(folder);
            Assert.Null(search.Problem());
            search.LanguageCode = "es";

            notices.Clear();
            HeadlessFixture.Wait(search.DownloadAsync());

            Assert.Equal(["✓", "EN", "✗"], search.Results.Select(result => result.Mark));
            Assert.Equal(["4 × Lightning Bolt", "2 × Shock", "Unknown Card"], search.Results.Select(result => result.Name));
            Assert.Equal([StringResources.Get("proceso_completado")], notices);
            Assert.False(search.IsBusy);
            Assert.Empty(search.ToImport);                                // not asked to
            search.Import = true;
            Assert.Equal([4, 2], search.ToImport.Select(card => card.Quantity));

            // A set, one card a name.
            search.FromDeck = false;
            search.SetCode = "m10";
            search.SelectedMode = 1;
            HeadlessFixture.Wait(search.DownloadAsync());
            Assert.Equal(["146-Lightning Bolt", "155-Shock"], search.Results.Select(result => result.Name));
            Assert.All(search.ToImport, card => Assert.True(File.Exists(card.File)));
        });
    }

    [Fact]
    public void Without_network_the_search_says_so_and_goes_on()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            var search = new CardSearchViewModel(new FakeServer { Offline = true }) { Folder = folder, DeckFile = Deck(folder) };
            var notices = new List<string>();
            search.Notice += notices.Add;

            HeadlessFixture.Wait(search.DownloadAsync());

            Assert.Contains("No network", Assert.Single(notices));
            Assert.Empty(search.Results);
            Assert.False(search.IsBusy);
        });
    }

    [Fact]
    public void Downloaded_cards_reach_the_panel_with_their_copies()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            var search = new CardSearchViewModel(new FakeServer()) { Folder = folder, DeckFile = Deck(folder), LanguageCode = "en", Import = true };
            HeadlessFixture.Wait(search.DownloadAsync());
            var model = new MainWindowViewModel();

            HeadlessFixture.Wait(model.ImportCardsAsync(search.ToImport));

            Assert.Equal(2, model.ImagePanel.PanelCount);
            Assert.Equal([4, 2], model.ImagePanel.Cards.Select(card => card.Copies));
            HeadlessFixture.Wait(model.ImportCardsAsync([]));
            Assert.Equal(2, model.ImagePanel.PanelCount);
        });
    }

    [Fact]
    public void Window_lists_the_results()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            var owner = new Window();
            owner.Show();
            var search = new CardSearchViewModel(new FakeServer()) { Folder = folder, DeckFile = Deck(folder), LanguageCode = "es" };
            var window = new CardSearchWindow(search);
            window.Show(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.True(window.FindControl<TextBox>("DeckBox")!.IsEffectivelyVisible);
            Assert.False(window.FindControl<TextBox>("SetBox")!.IsEffectivelyVisible);
            var said = new List<string>();
            search.Notice += said.Add;

            HeadlessFixture.Wait(search.DownloadAsync());
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(3, window.FindControl<ListBox>("ResultsBox")!.ItemCount);
            Screenshots.Capture(window, "ventana-buscar-cartas");
            search.FromDeck = false;
            Dispatcher.UIThread.RunJobs();
            Assert.True(window.FindControl<TextBox>("SetBox")!.IsEffectivelyVisible);
            window.Close();
            owner.Close();
        });
    }
}
