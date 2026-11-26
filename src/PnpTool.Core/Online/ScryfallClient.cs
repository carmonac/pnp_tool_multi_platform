using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PnpTool.Core.Infrastructure;

namespace PnpTool.Core.Online;

/// <summary>How the download of a card ended.</summary>
public enum CardDownloadStatus
{
    Ok,

    /// <summary>The card was not found in the language asked for; its English printing was taken.</summary>
    EnglishInstead,

    Failed,
}

/// <summary>A card asked for, with the image written for it, if any.</summary>
/// <param name="Name">The name shown for the card.</param>
/// <param name="File">The image, or an empty text when none was written.</param>
/// <param name="Quantity">How many copies the list asked for.</param>
public sealed record CardDownload(CardDownloadStatus Status, string Name, string File, int Quantity);

/// <summary>A line of a deck list: "4 Lightning Bolt (M10) 146".</summary>
public sealed record DeckListEntry(int Quantity, string Name, string Set, int CollectorNumber);

/// <summary>
/// Downloads images of Magic cards from Scryfall: the cards of a deck list, or a whole set.
/// Original: <c>Scryfall</c>.
/// </summary>
public sealed partial class ScryfallClient : IDisposable
{
    private const string Api = "https://api.scryfall.com/cards/";
    private readonly HttpClient _http;

    /// <param name="handler">Answers the requests instead of the network, for tests.</param>
    public ScryfallClient(HttpMessageHandler? handler = null)
    {
        _http = handler == null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(60);
        // Scryfall asks for a client name and an Accept header; nothing about the user or the machine is sent.
        string[] version = AppInfo.Version.Split('.');
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("PNPTool/" + version[0] + "." + (version.Length > 1 ? version[1] : "0"));
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json;q=0.9,*/*;q=0.8");
    }

    /// <summary>The language of the cards, as a code of the program ("es", "zh"…).</summary>
    public string Language { get; set; } = "en";

    /// <summary>Whether every printing of a set is downloaded, or one card a name.</summary>
    public bool EveryPrinting { get; set; } = true;

    /// <summary>The pause between requests, as Scryfall asks of its clients.</summary>
    public TimeSpan Pause { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Told how many cards are done, of how many.</summary>
    public Action<int, int>? ReportProgress { get; set; }

    public void Dispose() => _http.Dispose();

    /// <summary>Scryfall's code of a language of the program: the two letters, and "zhs" for Chinese.</summary>
    public static string LanguageCode(string code)
    {
        string two = code.Length >= 2 ? code[..2].ToLowerInvariant() : "en";
        return two == "zh" ? "zhs" : two;
    }

    /// <summary>
    /// Reads a line of a deck list: a quantity, a name and, optionally, a set in brackets and a
    /// collector number. Empty lines, comments (#) and lines without a quantity give null.
    /// Original: <c>ParseLine</c>, which turned a line it could not read into a search for
    /// the card "e" of the set "e".
    /// </summary>
    public static DeckListEntry? ParseDeckLine(string line)
    {
        line = line.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
        {
            return null;
        }

        Match match = DeckLine().Match(line);
        if (!match.Success)
        {
            return null;
        }

        int quantity = int.TryParse(match.Groups["qty"].Value, out int typed) ? typed : 1;
        int number = int.TryParse(match.Groups["num"].Value, out int collector) ? collector : 0;
        return new DeckListEntry(quantity, match.Groups["name"].Value, match.Groups["set"].Value, number);
    }

    [GeneratedRegex(@"^(?<qty>\d+)x?\s+(?<name>.+?)(?:\s+\((?<set>[^)]*)\))?(?:\s+(?<num>\d+))?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex DeckLine();

    /// <summary>
    /// The cards of a deck file: a text list, or the JSON Scryfall exports for a deck, of
    /// which the main sections are taken. Original: <c>DescargaMazo</c> and <c>JsonScryFall_ParsePrimaryCards</c>.
    /// </summary>
    public static List<DeckListEntry> ReadDeck(string file)
    {
        if (string.Equals(Path.GetExtension(file), ".txt", StringComparison.OrdinalIgnoreCase))
        {
            return [.. File.ReadAllLines(file).Select(ParseDeckLine).OfType<DeckListEntry>()];
        }

        var entries = new List<DeckListEntry>();
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("sections", out JsonElement sections) || !sections.TryGetProperty("primary", out JsonElement primary)
            || primary.ValueKind != JsonValueKind.Array || !root.TryGetProperty("entries", out JsonElement all))
        {
            return entries;
        }

        foreach (JsonElement section in primary.EnumerateArray())
        {
            if (section.GetString() is not string name || !all.TryGetProperty(name, out JsonElement list) || list.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement entry in list.EnumerateArray())
            {
                bool found = entry.TryGetProperty("found", out JsonElement flag) && flag.ValueKind == JsonValueKind.True;
                if (found && entry.TryGetProperty("card_digest", out JsonElement digest) && digest.ValueKind == JsonValueKind.Object
                    && Text(digest, "name") is string card)
                {
                    int count = entry.TryGetProperty("count", out JsonElement number) && number.TryGetInt32(out int typed) ? typed : 1;
                    entries.Add(new DeckListEntry(count, card, Text(digest, "set") ?? string.Empty, 0));
                }
            }
        }

        return entries;
    }

    /// <summary>
    /// Downloads the cards of a deck file into <c>&lt;folder&gt;/Cartas_&lt;name of the file&gt;</c>.
    /// A card not found in the language asked for is taken in English.
    /// Original: <c>Descarga</c> for a deck.
    /// </summary>
    public async Task<(AppError Error, List<CardDownload> Cards)> DownloadDeckAsync(string deckFile, string folder, CancellationToken cancellation = default)
    {
        var cards = new List<CardDownload>();
        List<DeckListEntry> entries;
        try
        {
            entries = ReadDeck(deckFile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return (Problem("No valid cards on " + deckFile + ": " + exception.Message), cards);
        }

        if (entries.Count == 0)
        {
            return (Problem("No valid cards on " + deckFile), cards);
        }

        string target = Directory.CreateDirectory(Path.Combine(folder, "Cartas_" + Path.GetFileNameWithoutExtension(deckFile))).FullName;
        ReportProgress?.Invoke(0, entries.Count);
        try
        {
            for (int index = 0; index < entries.Count; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                cards.AddRange(await DownloadEntryAsync(entries[index], target, cancellation));
                ReportProgress?.Invoke(index + 1, entries.Count);
            }
        }
        catch (HttpRequestException exception)
        {
            return (Problem(exception.Message), cards);
        }

        return (new AppError(), cards);
    }

    /// <summary>
    /// Downloads every card of a set into <c>&lt;folder&gt;/Cartas_&lt;set&gt;</c>.
    /// Original: <c>DescargaSets</c>, which read the first page of the answer only (175 cards)
    /// and did not say where it had written the images.
    /// </summary>
    public async Task<(AppError Error, List<CardDownload> Cards)> DownloadSetAsync(string set, string folder, CancellationToken cancellation = default)
    {
        var cards = new List<CardDownload>();
        set = set.Trim();
        string target = Directory.CreateDirectory(Path.Combine(folder, "Cartas_" + CleanFileName(set))).FullName;
        string? url = Api + "search?q=" + Uri.EscapeDataString("set:" + set) + "+" + Uri.EscapeDataString("lang:" + LanguageCode(Language))
            + "&unique=" + (EveryPrinting ? "prints" : "cards");
        int done = 0;
        try
        {
            while (url != null)
            {
                cancellation.ThrowIfCancellationRequested();
                using HttpResponseMessage response = await _http.GetAsync(url, cancellation);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return (cards.Count == 0 ? Problem("Scryfall: " + set + " (" + LanguageCode(Language) + ") not found") : new AppError(), cards);
                }

                response.EnsureSuccessStatusCode();
                using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
                JsonElement root = document.RootElement;
                if (!root.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Array)
                {
                    return (Problem("Scryfall: " + set + ": no data"), cards);
                }

                int total = root.TryGetProperty("total_cards", out JsonElement count) && count.TryGetInt32(out int typed) ? typed : data.GetArrayLength();
                if (done == 0)
                {
                    ReportProgress?.Invoke(0, total);
                }

                foreach (JsonElement card in data.EnumerateArray())
                {
                    cancellation.ThrowIfCancellationRequested();
                    string number = Text(card, "collector_number") ?? "0";
                    string name = Text(card, "printed_name") ?? Text(card, "name") ?? "carta_" + done;
                    List<(string Name, string Url)> images = Images(card, name);
                    if (images.Count == 0)
                    {
                        cards.Add(new CardDownload(CardDownloadStatus.Failed, number + "-" + name, string.Empty, 1));
                    }

                    foreach ((string face, string image) in images)
                    {
                        string file = Path.Combine(target, CleanFileName(set + "-" + number + "-" + face) + ".png");
                        bool written = await SaveImageAsync(image, file, cancellation);
                        cards.Add(new CardDownload(written ? CardDownloadStatus.Ok : CardDownloadStatus.Failed, number + "-" + face, written ? file : string.Empty, 1));
                    }

                    done++;
                    ReportProgress?.Invoke(done, total);
                }

                bool more = root.TryGetProperty("has_more", out JsonElement hasMore) && hasMore.ValueKind == JsonValueKind.True;
                url = more ? Text(root, "next_page") : null;
                if (url != null)
                {
                    await Task.Delay(Pause, cancellation);
                }
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            return (Problem("Scryfall: " + exception.Message), cards);
        }

        return (new AppError(), cards);
    }

    /// <summary>Original: <c>AnadeScryfallCardAcartasConImagen</c>.</summary>
    private async Task<List<CardDownload>> DownloadEntryAsync(DeckListEntry entry, string folder, CancellationToken cancellation)
    {
        string asked = entry.Name + (entry.Set.Length != 0 ? " (" + entry.Set + ")" : string.Empty);
        (JsonDocument? found, bool english) = await FindAsync(entry, cancellation);
        using (found)
        {
            if (found == null)
            {
                return [new CardDownload(CardDownloadStatus.Failed, asked, string.Empty, entry.Quantity)];
            }

            JsonElement card = found.RootElement;
            string name = Text(card, "printed_name") ?? Text(card, "name") ?? entry.Name;
            var results = new List<CardDownload>();
            foreach ((string face, string url) in Images(card, name))
            {
                string shown = face + (entry.Set.Length != 0 ? " (" + entry.Set + ")" : string.Empty);
                string file = Path.Combine(folder, CleanFileName(face + (entry.Set.Length != 0 ? "_" + entry.Set : string.Empty) + "_x" + entry.Quantity) + ".png");
                bool written = await SaveImageAsync(url, file, cancellation);
                results.Add(new CardDownload(
                    !written ? CardDownloadStatus.Failed : english ? CardDownloadStatus.EnglishInstead : CardDownloadStatus.Ok,
                    shown,
                    written ? file : string.Empty,
                    entry.Quantity));
            }

            return results.Count != 0 ? results : [new CardDownload(CardDownloadStatus.Failed, asked, string.Empty, entry.Quantity)];
        }
    }

    /// <summary>
    /// The first card that answers to the name, in the language asked for or, failing that, in
    /// English. Original: <c>FindCard</c>.
    /// </summary>
    private async Task<(JsonDocument? Card, bool English)> FindAsync(DeckListEntry entry, CancellationToken cancellation)
    {
        string language = LanguageCode(Language);
        foreach (string tried in language == "en" ? new[] { "en" } : [language, "en"])
        {
            var url = new StringBuilder(Api).Append("search?q=").Append(Uri.EscapeDataString(entry.Name));
            if (entry.Set.Length != 0)
            {
                url.Append('+').Append(Uri.EscapeDataString("set:" + entry.Set));
            }

            url.Append('+').Append(Uri.EscapeDataString("lang:" + tried));
            using HttpResponseMessage response = await _http.GetAsync(url.ToString(), cancellation);
            await Task.Delay(Pause, cancellation);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            try
            {
                using JsonDocument list = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
                if (list.RootElement.TryGetProperty("data", out JsonElement data) && data.ValueKind == JsonValueKind.Array && data.GetArrayLength() != 0)
                {
                    return (JsonDocument.Parse(data[0].GetRawText()), tried != language);
                }
            }
            catch (JsonException)
            {
                // An answer that is not what was expected counts as not found.
            }
        }

        return (null, false);
    }

    /// <summary>
    /// The images of a card: its own, or one a face for the cards printed on both sides.
    /// Original: the two branches of <c>ProcesaCartasdelJson</c>; the deck download took the
    /// image of the card only and failed with a two-sided card.
    /// </summary>
    private static List<(string Name, string Url)> Images(JsonElement card, string name)
    {
        var images = new List<(string, string)>();
        if (card.TryGetProperty("image_uris", out JsonElement own) && own.ValueKind == JsonValueKind.Object)
        {
            if ((Text(own, "png") ?? Text(own, "large")) is string url)
            {
                images.Add((name, url));
            }
        }
        else if (card.TryGetProperty("card_faces", out JsonElement faces) && faces.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement face in faces.EnumerateArray())
            {
                if (face.TryGetProperty("image_uris", out JsonElement uris) && uris.ValueKind == JsonValueKind.Object
                    && (Text(uris, "png") ?? Text(uris, "large")) is string url)
                {
                    images.Add((Text(face, "printed_name") ?? Text(face, "name") ?? name, url));
                }
            }
        }

        return images;
    }

    private async Task<bool> SaveImageAsync(string url, string file, CancellationToken cancellation)
    {
        try
        {
            using HttpResponseMessage response = await _http.GetAsync(url, cancellation);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            await using (FileStream stream = File.Create(file))
            {
                await response.Content.CopyToAsync(stream, cancellation);
            }

            await Task.Delay(Pause, cancellation);
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            Log.Write("Image not downloaded: " + exception.Message);
            return false;
        }
    }

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    /// <summary>Original: <c>LimpiarNombreArchivo</c>.</summary>
    public static string CleanFileName(string name)
    {
        var clean = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            if (!"<>:\"/\\|?*“”’".Contains(c) && Array.IndexOf(Path.GetInvalidFileNameChars(), c) < 0)
            {
                clean.Append(c);
            }
        }

        return clean.ToString().Trim();
    }

    private static AppError Problem(string message) => new() { Kind = ErrorKind.Error, Message = message };
}
