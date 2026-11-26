using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Core;
using PnpTool.Core.CustomDecks;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Imaging;

namespace PnpTool.App.ViewModels;

/// <summary>
/// The creator of sports decks: a template image with a window, the picture of a player
/// behind it on each card, and the names of the team and of the player written where the
/// user puts them. Original: <c>WindowCrearMazoPersonalizadoDeportes</c> with
/// <c>WindowCrearMazoPersonalizadoDeportesModelo</c>.
/// </summary>
public sealed partial class SportsDeckViewModel : ViewModelBase
{
    /// <summary>The text shown for the player while no card is chosen, as in the original.</summary>
    public const string SamplePlayer = "Lorem ipsum";

    /// <summary>The size, in the units of the window, of the card the texts are placed on.</summary>
    public const double CardWidth = 240.0;

    public const double CardHeight = 336.0;

    private bool _loading;

    public SportsDeckViewModel()
    {
        // Where the original put the two texts on its card of 240 by 336.
        Deck.PlayerText.Position = new PointD { X = 100.0 / CardWidth, Y = 50.0 / CardHeight };
        Deck.TeamText.Position = new PointD { X = 80.0 / CardWidth, Y = 200.0 / CardHeight };
    }

    public SportsDeck Deck { get; private set; } = new();

    /// <summary>The folder with the templates shipped with the program, offered first when one is looked for.</summary>
    public static string TemplatesFolder => Path.Combine(AppPaths.CustomDecksFolder, "imagenes", "deportes");

    [ObservableProperty]
    private string _teamName = string.Empty;

    [ObservableProperty]
    private bool _teamVisible = true;

    [ObservableProperty]
    private string _templatePath = string.Empty;

    [ObservableProperty]
    private string _backPath = string.Empty;

    /// <summary>The names of the players, a card each.</summary>
    public ObservableCollection<string> Players { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayerName))]
    [NotifyPropertyChangedFor(nameof(PlayerPicturePath))]
    private int _selectedIndex = -1;

    [ObservableProperty]
    private double _progress;

    /// <summary>The name written on the card on show.</summary>
    public string PlayerName => SelectedIndex >= 0 && SelectedIndex < Players.Count ? Players[SelectedIndex] : SamplePlayer;

    /// <summary>The picture behind the template on the card on show.</summary>
    public string PlayerPicturePath =>
        SelectedIndex >= 0 && SelectedIndex < Deck.Cards.Length ? Deck.Cards[SelectedIndex].BackgroundImagePath : string.Empty;

    /// <summary>Raised to tell the user something.</summary>
    public event Action<string>? Notice;

    /// <summary>Raised when fonts or the places of the texts change other than by dragging.</summary>
    public event EventHandler? TextsChanged;

    /// <summary>Makes the PDF of the project of a deck and saves the project in the folder; set by the main window.</summary>
    public Func<Project, string, Task>? MakePdf { get; set; }

    partial void OnTeamNameChanged(string value) => Deck.TeamText.Text = value;

    partial void OnTeamVisibleChanged(bool value) => Deck.TeamTextVisible = value;

    partial void OnTemplatePathChanged(string value)
    {
        if (!_loading)
        {
            Deck.TemplateImagePath = value;
        }
    }

    partial void OnBackPathChanged(string value)
    {
        if (!_loading)
        {
            Deck.BackImagePath = value;
        }
    }

    /// <summary>Adds a player, a card of the deck. Original: <c>AnadirJugador</c>.</summary>
    public void AddPlayer(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        Deck.Add(new CustomCard { NameSpanish = name, Copies = 1 });
        Players.Add(name);
        SelectedIndex = Players.Count - 1;
    }

    /// <summary>Original: <c>Quitar</c>.</summary>
    public void RemoveSelected()
    {
        int index = SelectedIndex;
        if (index < 0 || index >= Deck.Cards.Length)
        {
            return;
        }

        Deck.Cards = [.. Deck.Cards.Where((_, position) => position != index)];
        Players.RemoveAt(index);
        SelectedIndex = Players.Count == 0 ? -1 : Math.Min(index, Players.Count - 1);
        OnPropertyChanged(nameof(PlayerName));
        OnPropertyChanged(nameof(PlayerPicturePath));
    }

    /// <summary>Gives the player chosen a picture. Original: <c>MeteImagenEnCarta</c>.</summary>
    public void SetPlayerPicture(string path)
    {
        if (SelectedIndex >= 0 && SelectedIndex < Deck.Cards.Length)
        {
            Deck.Cards[SelectedIndex].BackgroundImagePath = path;
            OnPropertyChanged(nameof(PlayerPicturePath));
        }
    }

    /// <summary>Moves a text: its top-left corner as a share, from 0 to 1, of the card.</summary>
    public void PlaceText(bool team, double x, double y)
    {
        TextData text = team ? Deck.TeamText : Deck.PlayerText;
        text.Position = new PointD { X = Math.Clamp(x, 0.0, 1.0), Y = Math.Clamp(y, 0.0, 1.0) };
    }

    /// <summary>Changes the font of a text. Original: <c>Boton_ConfigurarTexto_Click</c>.</summary>
    public void SetFont(bool team, FontSpec font)
    {
        (team ? Deck.TeamText : Deck.PlayerText).Font = font;
        TextsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Opens a sports deck saved before. Original: <c>AbreProyecto</c>.</summary>
    public void Open(string path)
    {
        AppError error;
        SportsDeck deck;
        try
        {
            (error, deck) = ProjectFileStore.LoadSportsDeck(path, "MazoDeportivo");
        }
        catch (Exception exception) when (exception is System.Xml.XmlException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            error = new AppError { Kind = ErrorKind.Error, Message = Path.GetFileName(path) + ": " + exception.Message };
            deck = new SportsDeck();
        }

        if (error.HasError)
        {
            Notice?.Invoke(error.Message);
            return;
        }

        Deck = deck;
        _loading = true;
        TeamName = deck.TeamText.Text;
        TeamVisible = deck.TeamTextVisible;
        TemplatePath = deck.TemplateImagePath;
        BackPath = deck.BackImagePath;
        _loading = false;
        // Decks of older versions kept the places of the texts in points of the screen; they go back to a place on the card.
        foreach (TextData text in new[] { deck.TeamText, deck.PlayerText })
        {
            text.Position = new PointD { X = Math.Clamp(text.Position.X, 0.0, 1.0), Y = Math.Clamp(text.Position.Y, 0.0, 1.0) };
        }

        SelectedIndex = -1;
        Players.Clear();
        foreach (CustomCard card in deck.Cards)
        {
            Players.Add(card.NameSpanish);
        }

        SelectedIndex = Players.Count == 0 ? -1 : 0;
        OnPropertyChanged(nameof(PlayerName));
        OnPropertyChanged(nameof(PlayerPicturePath));
        TextsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Saves the deck; the images made for a PDF are not part of it. Original: <c>Guardar</c>.</summary>
    public void Save(string path)
    {
        foreach (CustomCard card in Deck.Cards)
        {
            card.MergedImagePath = string.Empty;
        }

        try
        {
            ProjectFileStore.SaveCustomDeck(path, Deck);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Notice?.Invoke(exception.Message);
        }
    }

    /// <summary>What stops the deck from being made, or null. Original: <c>TodoOk</c>.</summary>
    public string? Problem() =>
        string.IsNullOrWhiteSpace(Deck.BackImagePath) ? StringResources.Get("falta_imagen_trasera")
        : string.IsNullOrWhiteSpace(Deck.TemplateImagePath) ? StringResources.Get("faltan_datos")
        : Deck.Cards.Length == 0 ? StringResources.Get("Cartas") + "=0"
        : null;

    /// <summary>Makes the images of the cards in a folder, then the PDF, and leaves the project in the folder. Original: <c>Procesar</c>.</summary>
    public async Task CreateAsync(string folder)
    {
        string? problem = Problem();
        if (problem != null)
        {
            Notice?.Invoke(problem);
            return;
        }

        Progress = 0.0;
        try
        {
            (AppError error, Project project) = await Task.Run(() => CustomDeckProjects.BuildSports(Deck, folder, share => Progress = share));
            if (error.HasError)
            {
                Notice?.Invoke(error.Message);
                return;
            }

            if (MakePdf != null)
            {
                await MakePdf(project, folder);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ImageMagick.MagickException)
        {
            Notice?.Invoke(exception.Message);
        }
        finally
        {
            Progress = 0.0;
        }
    }
}
