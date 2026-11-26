using Avalonia.Controls;
using Avalonia.Interactivity;
using PnpTool.App.ViewModels;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.Views;

/// <summary>
/// The tool that sorts the images of a folder into fronts and backs; it closes when done.
/// Original: <c>WindowSepararFrontalesTraseras</c>.
/// </summary>
public partial class SplitFrontsBacksWindow : Window
{
    public SplitFrontsBacksWindow()
        : this(new SplitFrontsBacksViewModel())
    {
    }

    public SplitFrontsBacksWindow(SplitFrontsBacksViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = StringResources.Get("Form_Separar_Frontales_Traseras");
        MarkerLabel.Text = StringResources.Get("Form_Separar_Frontales_Traseras_Label_Criterio");
    }

    public SplitFrontsBacksViewModel Model { get; }

    /// <summary>Sorts the files and, when it goes well, tells so and closes.</summary>
    public async Task AcceptAsync(bool tell = true)
    {
        string? said = null;
        void Note(string message) => said = message;
        Model.Notice += Note;
        (int Fronts, int Backs)? result = Model.Split();
        Model.Notice -= Note;
        if (tell && said != null)
        {
            await new MessageWindow(said).ShowDialog(this);
        }

        if (result != null)
        {
            Close(true);
        }
    }

    private async void OnPickFolder(object? sender, RoutedEventArgs e)
    {
        string? folder = await DeckWindows.PickFolderAsync(this);
        if (folder != null)
        {
            Model.Folder = folder;
        }
    }

    private async void OnAccepted(object? sender, EventArgs e) => await AcceptAsync();

    private void OnCancelled(object? sender, EventArgs e) => Close(null);
}
