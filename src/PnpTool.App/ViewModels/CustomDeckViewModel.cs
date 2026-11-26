using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Core;
using PnpTool.Core.CustomDecks;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Projects;
using PnpTool.Imaging;

namespace PnpTool.App.ViewModels;

/// <summary>
/// The creator of custom decks: one of the decks shipped with the program (poker, Spanish,
/// "uno"), a background of the user's for each card and a back for all of them.
/// Original: <c>WindowCrearMazoPersonalizado</c> with <c>WindowCrearMazoPersonalizadoModelo</c>.
/// </summary>
public sealed partial class CustomDeckViewModel : ViewModelBase
{
    private bool _loading;

    public CustomDeckViewModel()
    {
        DeckNames = Directory.Exists(AppPaths.CustomDecksFolder)
            ? [.. Directory.GetFiles(AppPaths.CustomDecksFolder, "*" + AppSettings.CustomDeckExtension)
                .Select(file => Path.GetFileNameWithoutExtension(file)!)
                .Order(StringComparer.OrdinalIgnoreCase)]
            : [];
    }

    /// <summary>The deck being made.</summary>
    public CustomDeck Deck { get; private set; } = new();

    /// <summary>The decks shipped with the program. Original: <c>DevuelveListadoMazos</c>.</summary>
    public IReadOnlyList<string> DeckNames { get; }

    [ObservableProperty]
    private string? _selectedDeckName;

    /// <summary>The names of the cards and, last, the back of the deck.</summary>
    public ObservableCollection<string> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PicturePath))]
    [NotifyPropertyChangedFor(nameof(BackgroundPath))]
    private int _selectedIndex = -1;

    [ObservableProperty]
    private double _progress;

    /// <summary>Whether a deck has been chosen or opened.</summary>
    public bool HasDeck => Items.Count != 0;

    /// <summary>Whether the back is what is chosen in the list.</summary>
    public bool BackSelected => HasDeck && SelectedIndex == Deck.Cards.Length;

    /// <summary>The image drawn on top: the picture of the card chosen, or the back of the deck.</summary>
    public string PicturePath =>
        SelectedIndex < 0 || !HasDeck ? string.Empty
        : BackSelected ? Deck.BackImagePath
        : PathHelper.FindExisting(AppPaths.CustomDecksFolder.TrimEnd(Path.DirectorySeparatorChar) + PathHelper.FromFile(Deck.Cards[SelectedIndex].ImagePath));

    /// <summary>The image drawn under the picture of the card chosen.</summary>
    public string BackgroundPath =>
        SelectedIndex < 0 || !HasDeck || BackSelected ? string.Empty : Deck.Cards[SelectedIndex].BackgroundImagePath;

    /// <summary>The size of the cards of the deck, in millimetres, from its style. Original: <c>CambiaTamanoborde</c>.</summary>
    public (double Width, double Height) CardSize { get; private set; } = (63.5, 88.9);

    /// <summary>Raised to tell the user something.</summary>
    public event Action<string>? Notice;

    /// <summary>
    /// Makes the PDF of the project of a deck and saves the project in the folder; set by the
    /// main window. Original: <c>BotonCrearFicheroSalida</c> and <c>GuardarXML_V2</c>.
    /// </summary>
    public Func<Project, string, Task>? MakePdf { get; set; }

    partial void OnSelectedDeckNameChanged(string? value)
    {
        if (!_loading && !string.IsNullOrEmpty(value))
        {
            Load(Path.Combine(AppPaths.CustomDecksFolder, value + AppSettings.CustomDeckExtension));
        }
    }

    /// <summary>Opens a deck saved before, with its backgrounds and back. Original: <c>Boton_AbrirProyecto_Click</c>.</summary>
    public void Open(string path)
    {
        _loading = true;
        SelectedDeckName = null;
        _loading = false;
        Load(path);
    }

    /// <summary>Original: <c>AbreProyecto</c>.</summary>
    private void Load(string path)
    {
        var deck = new CustomDeck();
        AppError error;
        try
        {
            error = ProjectFileStore.LoadCustomDeck(path, ref deck);
        }
        catch (Exception exception) when (exception is System.Xml.XmlException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            error = new AppError { Kind = ErrorKind.Error, Message = Path.GetFileName(path) + ": " + exception.Message };
        }

        if (error.HasError)
        {
            Notice?.Invoke(error.Message);
            return;
        }

        Deck = deck;
        var settings = new ProjectSettings();
        string style = Path.Combine(AppPaths.DefaultStylesFolder, deck.Style);
        if (File.Exists(style) && !ProjectFileStore.LoadStyle(style, ref settings).Error.HasError)
        {
            CardSize = (settings.Grid.CardSize.X.Millimeter, settings.Grid.CardSize.Y.Millimeter);
        }

        bool spanish = AppSettings.LanguageCode == "es";
        SelectedIndex = -1;
        Items.Clear();
        foreach (CustomCard card in deck.Cards)
        {
            Items.Add(spanish || string.IsNullOrEmpty(card.NameEnglish) ? card.NameSpanish : card.NameEnglish);
        }

        Items.Add(StringResources.Get("Form_Setup_Caras_GroupBox_CaraTrasera"));
        OnPropertyChanged(nameof(HasDeck));
        OnPropertyChanged(nameof(CardSize));
        SelectedIndex = 0;
    }

    /// <summary>
    /// Gives an image to what is chosen in the list: the background of a card, or the back of
    /// the deck. Original: <c>ListadoCartas_MouseDoubleClick</c> and <c>ImagenCarta_Drop</c>.
    /// </summary>
    public void SetImage(string path)
    {
        if (SelectedIndex < 0 || !HasDeck)
        {
            return;
        }

        if (BackSelected)
        {
            Deck.BackImagePath = path;
        }
        else
        {
            Deck.Cards[SelectedIndex].BackgroundImagePath = path;
        }

        OnPropertyChanged(nameof(PicturePath));
        OnPropertyChanged(nameof(BackgroundPath));
    }

    /// <summary>Saves the deck with its backgrounds and back. Original: <c>Boton_Guardar_Click</c>.</summary>
    public void Save(string path)
    {
        try
        {
            ProjectFileStore.SaveCustomDeck(path, Deck);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Notice?.Invoke(exception.Message);
        }
    }

    /// <summary>What stops the deck from being made, or null. Original: the checks of <c>Procesar</c>.</summary>
    public string? Problem() =>
        !HasDeck ? StringResources.Get("faltan_datos")
        : string.IsNullOrEmpty(Deck.BackImagePath) ? StringResources.Get("falta_imagen_trasera")
        : null;

    /// <summary>
    /// Makes the images of the cards in a folder, then the PDF, and leaves the project in the
    /// folder. Original: <c>Procesar</c>.
    /// </summary>
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
            (AppError error, Project project) = await Task.Run(() => CustomDeckProjects.Build(Deck, folder, share => Progress = share));
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
