using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PnpTool.App.Services;
using PnpTool.App.ViewModels;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Projects;

namespace PnpTool.App.Views;

/// <summary>
/// The tool that makes the cover of a book. A click on one of the three parts, or an image
/// dropped on it, gives it its image. Original: <c>WindowPortadaLibro</c>.
/// </summary>
public partial class BookCoverWindow : Window
{
    public BookCoverWindow()
        : this(new BookCoverViewModel())
    {
    }

    public BookCoverWindow(BookCoverViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = StringResources.Get("ShellMenuBarItem_Herramientas_HacerPortadas");
        PaperLabel.Text = StringResources.Get("Form_Setup_GroupBox_Papel");
        CoverLabel.Text = StringResources.Get("Form_SetupLibro_GroupBox_Cubierta");
        PagesLabel.Text = StringResources.Get("Form_PortadaLibro_NumeroPaginas");
        SpineLabel.Text = StringResources.Get("Form_Setup_libro_Label_Lomo");
        BleedLabel.Text = StringResources.Get("Form_Setup_LabelSangrado");
        NoteBlock.Text = StringResources.Get("Form_PortadaLibro_Nota");
        ToolTip.SetTip(ThicknessBox, StringResources.Get("grosor"));
        ToolTip.SetTip(CreateButton, StringResources.Get("Form_PortadaLibro_HacerPortada"));
        model.Notice += message => _ = new MessageWindow(message).ShowDialog(this);
        model.PropertyChanged += OnModelChanged;
        Closed += (_, _) => model.PropertyChanged -= OnModelChanged;
        LeftButton.AddHandler(DragDrop.DropEvent, (_, e) => Drop(BookCoverPart.Left, e));
        SpineButton.AddHandler(DragDrop.DropEvent, (_, e) => Drop(BookCoverPart.Spine, e));
        RightButton.AddHandler(DragDrop.DropEvent, (_, e) => Drop(BookCoverPart.Right, e));
        ShowPictures();
    }

    public BookCoverViewModel Model { get; }

    /// <summary>Asks where to save the PDF; the test of the window replaces it.</summary>
    public Func<Task<string?>>? AskFile { get; set; }

    /// <summary>Checks the settings, asks where to save and makes the cover. Original: <c>CrearPDFDirecto</c>.</summary>
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

        string? file = AskFile != null ? await AskFile() : await PdfToolWindows.PickPdfToSaveAsync(this, BookCoverViewModel.SuggestedName);
        return !string.IsNullOrEmpty(file) && await Model.CreateAsync(file);
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BookCoverViewModel.LeftImage) or nameof(BookCoverViewModel.SpineImage) or nameof(BookCoverViewModel.RightImage))
        {
            ShowPictures();
        }
    }

    private void ShowPictures()
    {
        LeftPicture.Source = DeckWindows.LoadPicture(Model.LeftImage);
        SpinePicture.Source = DeckWindows.LoadPicture(Model.SpineImage);
        RightPicture.Source = DeckWindows.LoadPicture(Model.RightImage);
    }

    private void Drop(BookCoverPart part, DragEventArgs e)
    {
        string? image = PdfToolWindows.Dropped(e).FirstOrDefault(PnpTool.Imaging.ImageFormats.IsImage);
        if (image != null)
        {
            Model.SetImage(part, image);
        }
    }

    private async Task PickAsync(BookCoverPart part)
    {
        IReadOnlyList<string> files = await ImageFiles.PickAsync(this, multiple: false);
        if (files.Count != 0)
        {
            Model.SetImage(part, files[0]);
        }
    }

    private async void OnPickLeft(object? sender, RoutedEventArgs e) => await PickAsync(BookCoverPart.Left);

    private async void OnPickSpine(object? sender, RoutedEventArgs e) => await PickAsync(BookCoverPart.Spine);

    private async void OnPickRight(object? sender, RoutedEventArgs e) => await PickAsync(BookCoverPart.Right);

    private async void OnCreate(object? sender, RoutedEventArgs e) => await CreateAsync();
}
