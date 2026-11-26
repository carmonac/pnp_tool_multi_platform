using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PnpTool.App.Services;
using PnpTool.App.ViewModels;
using PnpTool.Controls;
using PnpTool.Core.CustomDecks;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.Views;

/// <summary>
/// The creator of sports decks. The two texts are dragged to their place on the card; a
/// double click on a player, or an image dropped on the card, gives the player a picture. It
/// closes with the folder to make the deck in, or with nothing when it is just closed.
/// Original: <c>WindowCrearMazoPersonalizadoDeportes</c>.
/// </summary>
public partial class SportsDeckWindow : Window
{
    private readonly Action<string> _notice;
    private TextBlock? _dragged;
    private Point _dragStart;
    private Point _textStart;

    public SportsDeckWindow()
        : this(new SportsDeckViewModel())
    {
    }

    public SportsDeckWindow(SportsDeckViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = StringResources.Get("ShellMenuBarItem_Herramientas_CreaMazoPersonalizadoDeportes");
        TeamLabel.Text = "Nombre Equipo:";    // a fixed text in the original too
        TemplateLabel.Text = StringResources.Get("Form_Setup_Label_Subgrupo");
        BackLabel.Text = StringResources.Get("Form_Principal_Label_ImagenTrasera");
        ToolTip.SetTip(OpenButton, StringResources.Get("Form_Principal_Boton_Abrir_ToolTipDescription"));
        ToolTip.SetTip(SaveButton, StringResources.Get("Form_Principal_Boton_Guardar_ToolTipDescription"));
        ToolTip.SetTip(AddButton, StringResources.Get("Form_UnirPDF_Boton_Anadir"));
        ToolTip.SetTip(RemoveButton, StringResources.Get("Form_UnirPDF_Boton_quitar"));
        ToolTip.SetTip(CreateButton, StringResources.Get("Crear_pdf"));
        _notice = message => _ = new MessageWindow(message).ShowDialog(this);
        model.Notice += _notice;
        model.PropertyChanged += OnModelChanged;
        model.TextsChanged += OnTextsChanged;
        Closed += (_, _) =>
        {
            model.Notice -= _notice;
            model.PropertyChanged -= OnModelChanged;
            model.TextsChanged -= OnTextsChanged;
        };
        foreach (TextBlock text in new[] { TeamText, PlayerText })
        {
            text.PointerPressed += OnTextPressed;
            text.PointerMoved += OnTextMoved;
            text.PointerReleased += OnTextReleased;
        }

        CardCanvas.AddHandler(DragDrop.DropEvent, OnDrop);
        ShowTexts();
        ShowPictures();
    }

    public SportsDeckViewModel Model { get; }

    /// <summary>Asks for the folder to make the deck in; the test of the window replaces it.</summary>
    public Func<Task<string?>>? AskFolder { get; set; }

    /// <summary>Asks for the name of a new player; the test of the window replaces it.</summary>
    public Func<Task<string?>>? AskName { get; set; }

    /// <summary>Asks for a folder and closes with it, so that the deck is made there, when nothing is missing.</summary>
    public async Task CreateAsync()
    {
        string? problem = Model.Problem();
        if (problem != null)
        {
            await new MessageWindow(problem).ShowDialog(this);
            return;
        }

        string? folder = AskFolder != null ? await AskFolder() : await DeckWindows.PickFolderAsync(this);
        if (!string.IsNullOrEmpty(folder))
        {
            Close(folder);
        }
    }

    /// <summary>Asks for a name and adds the player. Original: <c>AnadirJugador</c>.</summary>
    public async Task AddPlayerAsync()
    {
        string? name = AskName != null
            ? await AskName()
            : await new TextInputWindow(StringResources.Get("Form_UnirPDF_Boton_Anadir")).ShowDialog<string?>(this);
        if (!string.IsNullOrEmpty(name))
        {
            Model.AddPlayer(name);
        }
    }

    private void OnTextsChanged(object? sender, EventArgs e) => ShowTexts();

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SportsDeckViewModel.TemplatePath) or nameof(SportsDeckViewModel.PlayerPicturePath))
        {
            ShowPictures();
        }
    }

    private void ShowPictures()
    {
        TemplateImage.Source = DeckWindows.LoadPicture(Model.TemplatePath);
        PlayerImage.Source = DeckWindows.LoadPicture(Model.PlayerPicturePath);
    }

    /// <summary>Puts the texts where the deck says, in its fonts.</summary>
    private void ShowTexts()
    {
        Place(TeamText, Model.Deck.TeamText);
        Place(PlayerText, Model.Deck.PlayerText);
    }

    private static void Place(TextBlock text, TextData data)
    {
        data.Font.ApplyTo(text);
        Canvas.SetLeft(text, data.Position.X * SportsDeckViewModel.CardWidth);
        Canvas.SetTop(text, data.Position.Y * SportsDeckViewModel.CardHeight);
    }

    private void OnTextPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is TextBlock text && e.GetCurrentPoint(text).Properties.IsLeftButtonPressed)
        {
            _dragged = text;
            _dragStart = e.GetPosition(CardCanvas);
            _textStart = new Point(Canvas.GetLeft(text), Canvas.GetTop(text));
            e.Pointer.Capture(text);
            e.Handled = true;
        }
    }

    private void OnTextMoved(object? sender, PointerEventArgs e)
    {
        if (_dragged == null || !ReferenceEquals(sender, _dragged))
        {
            return;
        }

        Point now = e.GetPosition(CardCanvas);
        double left = Math.Clamp(_textStart.X + now.X - _dragStart.X, 0.0, Math.Max(0.0, CardCanvas.Width - _dragged.Bounds.Width));
        double top = Math.Clamp(_textStart.Y + now.Y - _dragStart.Y, 0.0, Math.Max(0.0, CardCanvas.Height - _dragged.Bounds.Height));
        Canvas.SetLeft(_dragged, left);
        Canvas.SetTop(_dragged, top);
        Model.PlaceText(ReferenceEquals(_dragged, TeamText), left / SportsDeckViewModel.CardWidth, top / SportsDeckViewModel.CardHeight);
    }

    private void OnTextReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragged != null)
        {
            e.Pointer.Capture(null);
            _dragged = null;
            e.Handled = true;
        }
    }

    private async Task ChooseFontAsync(bool team)
    {
        TextData data = team ? Model.Deck.TeamText : Model.Deck.PlayerText;
        FontSpec? font = await new FontDialog(team ? Model.TeamName : Model.PlayerName, data.Font).ShowDialog<FontSpec?>(this);
        if (font != null)
        {
            Model.SetFont(team, font);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Delete && PlayersBox.IsKeyboardFocusWithin)
        {
            Model.RemoveSelected();
            e.Handled = true;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        string[] files = [.. (e.DataTransfer.TryGetFiles() ?? []).Select(file => file.TryGetLocalPath()).OfType<string>()];
        if (files.Length == 1 && File.Exists(files[0]))
        {
            Model.SetPlayerPicture(files[0]);
        }
    }

    private async void OnPickPicture(object? sender, RoutedEventArgs e)
    {
        if (Model.SelectedIndex < 0)
        {
            return;
        }

        IReadOnlyList<string> files = await ImageFiles.PickAsync(this, multiple: false);
        if (files.Count != 0)
        {
            Model.SetPlayerPicture(files[0]);
        }
    }

    private async void OnPickTemplate(object? sender, RoutedEventArgs e)
    {
        string? path = await DeckWindows.PickImageAsync(this, SportsDeckViewModel.TemplatesFolder);
        if (path != null)
        {
            Model.TemplatePath = path;
        }
    }

    private async void OnPickBack(object? sender, RoutedEventArgs e)
    {
        string? path = await DeckWindows.PickImageAsync(this);
        if (path != null)
        {
            Model.BackPath = path;
        }
    }

    private async void OnOpen(object? sender, RoutedEventArgs e)
    {
        string? path = await DeckWindows.PickDeckToOpenAsync(this);
        if (path != null)
        {
            Model.Open(path);
        }
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        string? path = await DeckWindows.PickDeckToSaveAsync(this, Model.Deck.Name);
        if (path != null)
        {
            Model.Save(path);
        }
    }

    private async void OnAdd(object? sender, RoutedEventArgs e) => await AddPlayerAsync();

    private void OnRemove(object? sender, RoutedEventArgs e) => Model.RemoveSelected();

    private async void OnTeamFont(object? sender, RoutedEventArgs e) => await ChooseFontAsync(team: true);

    private async void OnPlayerFont(object? sender, RoutedEventArgs e) => await ChooseFontAsync(team: false);

    private async void OnCreate(object? sender, RoutedEventArgs e) => await CreateAsync();
}
