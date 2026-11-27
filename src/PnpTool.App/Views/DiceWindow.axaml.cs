using Avalonia.Controls;
using Avalonia.Interactivity;
using PnpTool.App.ViewModels;
using PnpTool.Core.Infrastructure;
using PnpTool.Pdf.Tabletop;

namespace PnpTool.App.Views;

/// <summary>
/// Asks where to write the templates of "seven pixels" dice. It closes with the folder and
/// beginning of the names of the files when accepted and with nothing when cancelled.
/// Original: <c>Window_Dice7Pixels</c>, which also wrote the templates and showed their
/// progress; here the main window does, with the progress window of every long task.
/// </summary>
public partial class DiceWindow : Window
{
    public DiceWindow()
        : this(new DiceViewModel(new SevenPixelDice([], SevenPixelDie.D6, AppSettings.ColorWhite)))
    {
    }

    public DiceWindow(DiceViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = StringResources.Get("Form_Dice7Pixels");
        FolderLabel.Text = StringResources.Get("Form_Dice7Pixels_LabelDestino");
        FileNameLabel.Text = StringResources.Get("Form_Dice7Pixels_LabelSalida");
    }

    public DiceViewModel Model { get; }

    /// <summary>Shows a message; the test of the window replaces it.</summary>
    public Func<string, Task>? ShowMessage { get; set; }

    /// <summary>Asks for a folder; the test of the window replaces it. Gives null when cancelled.</summary>
    public Func<Task<string?>>? PickFolder { get; set; }

    /// <summary>Accepts what is set, as the accept button does. Original: <c>Boton_Aceptar_Click</c>.</summary>
    public async Task AcceptAsync()
    {
        string? problem = Model.Problem();
        if (problem != null)
        {
            await (ShowMessage != null ? ShowMessage(problem) : new MessageWindow(problem).ShowDialog(this));
            return;
        }

        Close(Model.Result());
    }

    /// <summary>Chooses the folder, as its button does.</summary>
    public async Task PickFolderAsync()
    {
        string? path = PickFolder != null ? await PickFolder() : await DeckWindows.PickFolderAsync(this);
        if (!string.IsNullOrEmpty(path))
        {
            Model.Folder = path;
        }
    }

    private async void OnAccepted(object? sender, EventArgs e) => await AcceptAsync();

    private void OnCancelled(object? sender, EventArgs e) => Close(null);

    private async void OnPickFolder(object? sender, RoutedEventArgs e) => await PickFolderAsync();
}
