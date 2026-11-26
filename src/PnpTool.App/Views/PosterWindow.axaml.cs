using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using PnpTool.App.Services;
using PnpTool.App.ViewModels;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.Views;

/// <summary>
/// The tool that splits a picture over several sheets to make a poster. A click on the
/// picture of the sheets, or an image dropped on it, chooses the image. It takes the place
/// of the program PosteRazor that the Windows version starts.
/// </summary>
public partial class PosterWindow : Window
{
    private Bitmap? _picture;

    public PosterWindow()
        : this(new PosterViewModel())
    {
    }

    public PosterWindow(PosterViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = "Poster";
        PaperLabel.Text = StringResources.Get("Form_Setup_GroupBox_Papel");
        LandscapeBox.Content = StringResources.Get("Form_Setup_Label_PapelHorizontal");
        MarginLabel.Text = StringResources.Get("Form_Setup_libro_Label_MarcasPerforacionMargen");
        SizeLabel.Text = StringResources.Get("Tamano");
        model.Notice += message => _ = new MessageWindow(message).ShowDialog(this);
        model.PropertyChanged += OnModelChanged;
        model.PlanChanged += ShowPlan;
        Closed += (_, _) =>
        {
            model.PropertyChanged -= OnModelChanged;
            model.PlanChanged -= ShowPlan;
        };
        ImageButton.AddHandler(DragDrop.DropEvent, OnDrop);
        LoadPicture();
    }

    public PosterViewModel Model { get; }

    /// <summary>Asks where to save the PDF; the test of the window replaces it.</summary>
    public Func<Task<string?>>? AskFile { get; set; }

    /// <summary>Checks the settings, asks where to save and makes the poster.</summary>
    public async Task<bool> CreateAsync()
    {
        string? problem = Model.Problem();
        if (problem != null)
        {
            if (AskFile == null)
            {
                await new MessageWindow(problem).ShowDialog(this);
            }

            return false;
        }

        string? file = AskFile != null ? await AskFile() : await PdfToolWindows.PickPdfToSaveAsync(this, PosterViewModel.SuggestedName);
        return !string.IsNullOrEmpty(file) && await Model.CreateAsync(file);
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PosterViewModel.ImagePath))
        {
            LoadPicture();
        }
    }

    private void LoadPicture()
    {
        _picture = DeckWindows.LoadPicture(Model.ImagePath);
        ShowPlan();
    }

    private void ShowPlan()
    {
        Preview.Show(_picture, Model.Plan);
        ImageHint.IsVisible = Model.Plan == null;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        string? image = PdfToolWindows.Dropped(e).FirstOrDefault(PnpTool.Imaging.ImageFormats.IsImage);
        if (image != null)
        {
            Model.ImagePath = image;
        }
    }

    private async void OnPickImage(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<string> files = await ImageFiles.PickAsync(this, multiple: false);
        if (files.Count != 0)
        {
            Model.ImagePath = files[0];
        }
    }

    private async void OnCreate(object? sender, RoutedEventArgs e) => await CreateAsync();
}
