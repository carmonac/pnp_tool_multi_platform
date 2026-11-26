using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PnpTool.App.Services;
using PnpTool.App.ViewModels;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.Views;

/// <summary>
/// The creator of custom decks. A double click on a card of the list, or an image dropped on
/// the card, gives it a background; the last entry of the list is the back of the deck. It
/// closes with the folder to make the deck in, or with nothing when it is just closed.
/// Original: <c>WindowCrearMazoPersonalizado</c>.
/// </summary>
public partial class CustomDeckWindow : Window
{
    private readonly Action<string> _notice;

    public CustomDeckWindow()
        : this(new CustomDeckViewModel())
    {
    }

    public CustomDeckWindow(CustomDeckViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = StringResources.Get("ShellMenuBarItem_Herramientas_CreaMazoPersonalizado");
        ToolTip.SetTip(OpenButton, StringResources.Get("Form_Principal_Boton_Abrir_ToolTipDescription"));
        ToolTip.SetTip(SaveButton, StringResources.Get("Form_Principal_Boton_Guardar_ToolTipDescription"));
        ToolTip.SetTip(CreateButton, StringResources.Get("Crear_pdf"));
        _notice = message => _ = new MessageWindow(message).ShowDialog(this);
        model.Notice += _notice;
        model.PropertyChanged += OnModelChanged;
        Closed += (_, _) =>
        {
            model.Notice -= _notice;
            model.PropertyChanged -= OnModelChanged;
        };
        CardBorder.AddHandler(DragDrop.DropEvent, OnDrop);
        ShowCard();
    }

    public CustomDeckViewModel Model { get; }

    /// <summary>Asks for the folder to make the deck in; the test of the window replaces it.</summary>
    public Func<Task<string?>>? AskFolder { get; set; }

    /// <summary>
    /// Asks for a folder and closes with it, so that the deck is made there, when nothing is
    /// missing. Original: the start of <c>Procesar</c>.
    /// </summary>
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

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CustomDeckViewModel.PicturePath) or nameof(CustomDeckViewModel.BackgroundPath) or nameof(CustomDeckViewModel.CardSize))
        {
            ShowCard();
        }
    }

    private void ShowCard()
    {
        (double width, double height) = Model.CardSize;
        CardBorder.Width = Math.Round(width / 25.4 * 96.0);
        CardBorder.Height = Math.Round(height / 25.4 * 96.0);
        PictureImage.Source = DeckWindows.LoadPicture(Model.PicturePath);
        BackgroundImage.Source = DeckWindows.LoadPicture(Model.BackgroundPath);
    }

    private async void OnPickImage(object? sender, RoutedEventArgs e)
    {
        if (Model.SelectedIndex < 0)
        {
            return;
        }

        IReadOnlyList<string> files = await ImageFiles.PickAsync(this, multiple: false);
        if (files.Count != 0)
        {
            Model.SetImage(files[0]);
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        string[] files = [.. (e.DataTransfer.TryGetFiles() ?? []).Select(file => file.TryGetLocalPath()).OfType<string>()];
        if (files.Length == 1 && File.Exists(files[0]))
        {
            Model.SetImage(files[0]);
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

    private async void OnCreate(object? sender, RoutedEventArgs e) => await CreateAsync();
}
