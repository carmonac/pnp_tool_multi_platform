using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Online;

namespace PnpTool.App.ViewModels;

/// <summary>A card of the list of results, with the mark of how its download ended.</summary>
public sealed record CardSearchResult(CardDownload Card)
{
    public string Mark => Card.Status switch
    {
        CardDownloadStatus.Ok => "✓",
        CardDownloadStatus.EnglishInstead => "EN",
        _ => "✗",
    };

    public bool Failed => Card.Status == CardDownloadStatus.Failed;

    public bool English => Card.Status == CardDownloadStatus.EnglishInstead;

    public string Name => Card.Quantity > 1 ? Card.Quantity + " × " + Card.Name : Card.Name;
}

/// <summary>
/// The search of Magic cards in Scryfall: the images of a deck list or of a whole set are
/// downloaded into a folder and can be brought to the panel. Original: <c>Window_MTG</c>.
/// </summary>
public sealed partial class CardSearchViewModel : ViewModelBase
{
    private readonly HttpMessageHandler? _handler;

    public CardSearchViewModel()
        : this(null)
    {
    }

    /// <param name="handler">Answers the requests instead of the network, for tests.</param>
    public CardSearchViewModel(HttpMessageHandler? handler)
    {
        _handler = handler;
        Modes = [StringResources.Get("ComboBox_ModoDescargaMTG_1"), StringResources.Get("ComboBox_ModoDescargaMTG_2")];
        _folder = AppSettings.LastFolder;
    }

    /// <summary>Whether the cards come from a deck file, or are those of a set.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FromSet))]
    private bool _fromDeck = true;

    public bool FromSet => !FromDeck;

    /// <summary>A text list ("4 Lightning Bolt (M10)") or the JSON of a deck exported from Scryfall.</summary>
    [ObservableProperty]
    private string _deckFile = string.Empty;

    /// <summary>The code of the set, such as "m10".</summary>
    [ObservableProperty]
    private string _setCode = string.Empty;

    /// <summary>Where the folder with the images is made.</summary>
    [ObservableProperty]
    private string _folder;

    /// <summary>Every printing of a set, or one card a name.</summary>
    public IReadOnlyList<string> Modes { get; }

    [ObservableProperty]
    private int _selectedMode;

    /// <summary>The language of the cards, as a code of the program.</summary>
    [ObservableProperty]
    private string _languageCode = AppSettings.LanguageCode;

    /// <summary>Whether the images go to the panel as cards when the window is closed.</summary>
    [ObservableProperty]
    private bool _import;

    public ObservableCollection<CardSearchResult> Results { get; } = [];

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private double _progressMaximum = 1.0;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>The images downloaded, with their copies, when they are wanted in the panel.</summary>
    public IReadOnlyList<CardDownload> ToImport =>
        Import ? [.. Results.Select(result => result.Card).Where(card => card.File.Length != 0)] : [];

    /// <summary>Raised to tell the user something.</summary>
    public event Action<string>? Notice;

    /// <summary>What is missing to start, or null. Original: <c>CompruebaErrores</c>.</summary>
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(Folder))
        {
            return StringResources.Get("faltan_datos");
        }

        if (FromDeck)
        {
            return string.IsNullOrWhiteSpace(DeckFile) || !File.Exists(DeckFile) ? "No file is selected" : null;
        }

        return string.IsNullOrWhiteSpace(SetCode) ? "Set missing" : null;
    }

    /// <summary>Downloads the cards and lists how each ended. Original: <c>Boton_Descargar_Click</c>.</summary>
    public async Task DownloadAsync(CancellationToken cancellation = default)
    {
        Results.Clear();
        string? problem = Problem();
        if (problem != null)
        {
            Notice?.Invoke(problem);
            return;
        }

        using var client = new ScryfallClient(_handler)
        {
            Language = LanguageCode,
            EveryPrinting = SelectedMode == 0,
            ReportProgress = (done, total) =>
            {
                ProgressMaximum = Math.Max(1, total);
                Progress = done;
            },
        };
        if (_handler != null)
        {
            client.Pause = TimeSpan.Zero;
        }

        IsBusy = true;
        try
        {
            (AppError error, List<CardDownload> cards) = FromDeck
                ? await client.DownloadDeckAsync(DeckFile, Folder, cancellation)
                : await client.DownloadSetAsync(SetCode, Folder, cancellation);
            foreach (CardDownload card in cards)
            {
                Results.Add(new CardSearchResult(card));
            }

            Notice?.Invoke(error.HasError ? error.Message : StringResources.Get("proceso_completado"));
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or IOException or UnauthorizedAccessException)
        {
            // No network, a time-out or a folder that cannot be written.
            Notice?.Invoke(exception.Message);
        }
        finally
        {
            IsBusy = false;
            Progress = 0.0;
        }
    }
}
