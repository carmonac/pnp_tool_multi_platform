using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PnpTool.App.ViewModels;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;

namespace PnpTool.App.Views;

/// <summary>
/// The search of Magic cards in Scryfall. The window stays open after a download, with the
/// list of what was found. Original: <c>Window_MTG</c>.
/// </summary>
public partial class CardSearchWindow : Window
{
    public CardSearchWindow()
        : this(new CardSearchViewModel())
    {
    }

    public CardSearchWindow(CardSearchViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = StringResources.Get("ShellMenuBarItem_Herramientas_Scryfall");
        ModeLabel.Text = StringResources.Get("Label_ModoDescargaMTG");
        FolderLabel.Text = StringResources.Get("Form_Dice7Pixels_LabelDestino");
        ImportBox.Content = StringResources.Get("Form_Extraer_CheckBox_importar");
        ToolTip.SetTip(DownloadButton, StringResources.Get("Window_Actualizar_Descargar"));
        LanguageBox.List = LanguageList.CardSearch;
        Opened += (_, _) => LanguageBox.Load();
        model.Notice += message => _ = new MessageWindow(message).ShowDialog(this);
    }

    public CardSearchViewModel Model { get; }

    private async void OnDownload(object? sender, RoutedEventArgs e)
    {
        Model.LanguageCode = LanguageBox.SelectedLanguage.Code;
        await Model.DownloadAsync();
    }

    private async void OnPickFolder(object? sender, RoutedEventArgs e)
    {
        string? folder = await DeckWindows.PickFolderAsync(this);
        if (folder != null)
        {
            Model.Folder = folder;
        }
    }

    private async void OnPickDeck(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(AppSettings.LastFolder),
            FileTypeFilter = [new FilePickerFileType("Deck") { Patterns = ["*.txt", "*.json"] }, FilePickerFileTypes.All],
        });
        string? path = files.Count == 0 ? null : files[0].TryGetLocalPath();
        if (path != null)
        {
            Model.DeckFile = path;
        }
    }
}
