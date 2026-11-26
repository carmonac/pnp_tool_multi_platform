using System.Net;
using System.Text;
using PnpTool.Core.Online;

namespace PnpTool.Core.Tests.Online;

/// <summary>Answers the requests of the client from a table, and notes them.</summary>
public sealed class FakeScryfall : HttpMessageHandler
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    public List<string> Requests { get; } = [];

    /// <summary>Text answers by a piece of the address; the first that fits is taken.</summary>
    public List<(string Contains, string Json)> Answers { get; } = [];

    /// <summary>Whether every request fails as without network.</summary>
    public bool Offline { get; set; }

    public static string Card(string name, string number = "1", string? printed = null, bool twoFaces = false)
    {
        string image(string id) => $"\"image_uris\":{{\"png\":\"https://img.test/{id}.png\",\"large\":\"https://img.test/{id}.jpg\"}}";
        string printedName = printed == null ? string.Empty : $"\"printed_name\":\"{printed}\",";
        return twoFaces
            ? $"{{\"name\":\"{name}\",\"collector_number\":\"{number}\",\"card_faces\":[{{\"name\":\"{name} A\",{image(number + "a")}}},{{\"name\":\"{name} B\",{image(number + "b")}}}]}}"
            : $"{{\"name\":\"{name}\",{printedName}\"collector_number\":\"{number}\",{image(number)}}}";
    }

    public static string List(string cards, int total, string? nextPage = null) =>
        $"{{\"object\":\"list\",\"total_cards\":{total},\"has_more\":{(nextPage == null ? "false" : "true")}"
        + (nextPage == null ? string.Empty : $",\"next_page\":\"{nextPage}\"") + $",\"data\":[{cards}]}}";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string url = Uri.UnescapeDataString(request.RequestUri!.ToString());
        Requests.Add(url);
        if (Offline)
        {
            throw new HttpRequestException("No network");
        }

        if (url.StartsWith("https://img.test/", StringComparison.Ordinal))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Png) });
        }

        foreach ((string contains, string json) in Answers)
        {
            if (url.Contains(contains, StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
            }
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"object\":\"error\",\"status\":404}") });
    }
}

public sealed class ScryfallClientTests : IDisposable
{
    private readonly string _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "pnptool-scryfall-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static ScryfallClient Client(FakeScryfall fake, string language = "es") => new(fake) { Language = language, Pause = TimeSpan.Zero };

    [Theory]
    [InlineData("4 Lightning Bolt", 4, "Lightning Bolt", "", 0)]
    [InlineData("4x Lightning Bolt (M10) 146", 4, "Lightning Bolt", "M10", 146)]
    [InlineData("1 Fire // Ice (MH2)", 1, "Fire // Ice", "MH2", 0)]
    [InlineData("  2   Llanowar Elves  ", 2, "Llanowar Elves", "", 0)]
    public void Deck_lines_give_quantity_name_set_and_number(string line, int quantity, string name, string set, int number)
    {
        Assert.Equal(new DeckListEntry(quantity, name, set, number), ScryfallClient.ParseDeckLine(line));
    }

    [Theory]
    [InlineData("")]
    [InlineData("# sideboard")]
    [InlineData("Sideboard")]
    [InlineData("Lightning Bolt")]
    public void Lines_that_are_not_cards_are_skipped(string line)
    {
        Assert.Null(ScryfallClient.ParseDeckLine(line));
    }

    [Fact]
    public void Language_codes_are_those_of_scryfall()
    {
        Assert.Equal("es", ScryfallClient.LanguageCode("es"));
        Assert.Equal("en", ScryfallClient.LanguageCode("en-GB"));
        Assert.Equal("zhs", ScryfallClient.LanguageCode("zh"));
        Assert.Equal("en", ScryfallClient.LanguageCode(""));
    }

    [Fact]
    public async Task A_deck_list_downloads_each_card_falling_back_to_english()
    {
        string deck = Path.Combine(_folder, "mazo.txt");
        File.WriteAllLines(deck, ["# main", "4 Lightning Bolt (M10)", "Sideboard", "1 Rare Thing", "2 Fire // Ice", "3 No Such Card"]);
        var fake = new FakeScryfall();
        fake.Answers.Add(("q=Lightning Bolt+set:M10+lang:es", FakeScryfall.List(FakeScryfall.Card("Lightning Bolt", "146", printed: "Rayo"), 1)));
        fake.Answers.Add(("q=Rare Thing+lang:en", FakeScryfall.List(FakeScryfall.Card("Rare Thing", "7"), 1)));
        fake.Answers.Add(("q=Fire // Ice+lang:es", FakeScryfall.List(FakeScryfall.Card("Fire // Ice", "9", twoFaces: true), 1)));
        var progress = new List<(int, int)>();
        using ScryfallClient client = Client(fake);
        client.ReportProgress = (done, total) => progress.Add((done, total));

        (AppError error, List<CardDownload> cards) = await client.DownloadDeckAsync(deck, _folder, TestContext.Current.CancellationToken);

        Assert.False(error.HasError, error.Message);
        string target = Path.Combine(_folder, "Cartas_mazo");
        Assert.Equal(
            [
                new CardDownload(CardDownloadStatus.Ok, "Rayo (M10)", Path.Combine(target, "Rayo_M10_x4.png"), 4),
                new CardDownload(CardDownloadStatus.EnglishInstead, "Rare Thing", Path.Combine(target, "Rare Thing_x1.png"), 1),
                new CardDownload(CardDownloadStatus.Ok, "Fire // Ice A", Path.Combine(target, "Fire  Ice A_x2.png"), 2),
                new CardDownload(CardDownloadStatus.Ok, "Fire // Ice B", Path.Combine(target, "Fire  Ice B_x2.png"), 2),
                new CardDownload(CardDownloadStatus.Failed, "No Such Card", string.Empty, 3),
            ],
            cards);
        Assert.All(cards.Where(card => card.File.Length != 0), card => Assert.True(File.Exists(card.File), card.File));
        Assert.Equal((0, 4), progress[0]);
        Assert.Equal((4, 4), progress[^1]);
        Assert.Contains(fake.Requests, url => url.Contains("q=Rare Thing+lang:es"));    // asked in Spanish first
    }

    [Fact]
    public async Task A_deck_exported_from_scryfall_is_read_from_its_main_sections()
    {
        string deck = Path.Combine(_folder, "deck.json");
        File.WriteAllText(deck, """
            {"name":"Test","sections":{"primary":["mainboard"],"secondary":["sideboard"]},
             "entries":{"mainboard":[{"count":3,"found":true,"card_digest":{"name":"Lightning Bolt","set":"m10"}},
                                     {"count":1,"found":false,"card_digest":null}],
                        "sideboard":[{"count":2,"found":true,"card_digest":{"name":"Shock","set":"m10"}}]}}
            """);

        Assert.Equal([new DeckListEntry(3, "Lightning Bolt", "m10", 0)], ScryfallClient.ReadDeck(deck));

        var fake = new FakeScryfall();
        fake.Answers.Add(("q=Lightning Bolt+set:m10+lang:en", FakeScryfall.List(FakeScryfall.Card("Lightning Bolt", "146"), 1)));
        using ScryfallClient client = Client(fake, "en");
        (AppError error, List<CardDownload> cards) = await client.DownloadDeckAsync(deck, _folder, TestContext.Current.CancellationToken);
        Assert.False(error.HasError);
        Assert.Equal(CardDownloadStatus.Ok, Assert.Single(cards).Status);
        Assert.Single(fake.Requests, url => url.Contains("search?"));               // English is asked once
    }

    [Fact]
    public async Task A_set_is_downloaded_through_all_its_pages()
    {
        var fake = new FakeScryfall();
        fake.Answers.Add(("page=2", FakeScryfall.List(FakeScryfall.Card("Third: the \\\"last\\\"", "3"), 3)));
        fake.Answers.Add(("q=set:m10+lang:zhs&unique=cards", FakeScryfall.List(
            FakeScryfall.Card("First", "1", printed: "Primera") + "," + FakeScryfall.Card("Second", "2", twoFaces: true),
            3,
            nextPage: "https://api.scryfall.com/cards/search?page=2&q=set%3Am10")));
        using ScryfallClient client = Client(fake, "zh");
        client.EveryPrinting = false;
        var progress = new List<(int, int)>();
        client.ReportProgress = (done, total) => progress.Add((done, total));

        (AppError error, List<CardDownload> cards) = await client.DownloadSetAsync(" m10 ", _folder, TestContext.Current.CancellationToken);

        Assert.False(error.HasError, error.Message);
        string target = Path.Combine(_folder, "Cartas_m10");
        Assert.Equal(["1-Primera", "2-Second A", "2-Second B", "3-Third: the \"last\""], cards.Select(card => card.Name));
        Assert.Equal(Path.Combine(target, "m10-3-Third the last.png"), cards[3].File);  // without what a file name cannot have
        Assert.All(cards, card => Assert.True(File.Exists(card.File), card.File));
        Assert.Equal((3, 3), progress[^1]);
    }

    [Fact]
    public async Task Problems_come_back_as_messages()
    {
        var fake = new FakeScryfall();
        using ScryfallClient client = Client(fake);
        CancellationToken token = TestContext.Current.CancellationToken;

        (AppError unknown, List<CardDownload> none) = await client.DownloadSetAsync("zzz", _folder, token);
        Assert.True(unknown.HasError);
        Assert.Empty(none);

        string empty = Path.Combine(_folder, "vacio.txt");
        File.WriteAllText(empty, "just words");
        Assert.True((await client.DownloadDeckAsync(empty, _folder, token)).Error.HasError);
        Assert.True((await client.DownloadDeckAsync(Path.Combine(_folder, "no.txt"), _folder, token)).Error.HasError);
        string broken = Path.Combine(_folder, "roto.json");
        File.WriteAllText(broken, "{ not json");
        Assert.True((await client.DownloadDeckAsync(broken, _folder, token)).Error.HasError);

        fake.Offline = true;
        string deck = Path.Combine(_folder, "mazo.txt");
        File.WriteAllText(deck, "1 Lightning Bolt");
        (AppError offline, _) = await client.DownloadDeckAsync(deck, _folder, token);
        Assert.True(offline.HasError);
        Assert.Contains("No network", offline.Message);
        Assert.True((await client.DownloadSetAsync("m10", _folder, token)).Error.HasError);
    }
}
