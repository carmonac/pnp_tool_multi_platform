using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using PnpTool.App.Services;
using PnpTool.App.ViewModels;
using PnpTool.Controls;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.App.Views;

/// <summary>
/// The tool that cuts images into pieces, with the image chosen and the cuts drawn over it.
/// Original: <c>Window_CortarImagenes</c>.
/// </summary>
public partial class CutImagesWindow : Window
{
    private string? _shown;
    private Bitmap? _picture;
    private (int Width, int Height) _shownSize;

    public CutImagesWindow()
        : this(new CutImagesViewModel())
    {
    }

    public CutImagesWindow(CutImagesViewModel model)
    {
        InitializeComponent();
        DataContext = model;
        Model = model;
        Title = StringResources.Get("Form_CortarImagenes");
        ToolTip.SetTip(AddButton, StringResources.Get("Form_CortarImagenes_Boton_AnadirImagen"));
        ToolTip.SetTip(FolderButton, StringResources.Get("Form_CortarImagenes_Boton_SeleccionarCarpeta"));
        ToolTip.SetTip(RemoveButton, StringResources.Get("Form_CortarImagenes_Boton_QuitarListbox"));
        ToolTip.SetTip(CutButton, StringResources.Get("Form_CortarImagenes_Boton_Cortar"));
        ToolTip.SetTip(ResetButton, StringResources.Get("Form_CortarImagenes_Boton_Reset"));
        ToolTip.SetTip(ColumnsBox, StringResources.Get("Columnas"));
        ToolTip.SetTip(RowsBox, StringResources.Get("Filas"));
        ToolTip.SetTip(SpacingXBox, StringResources.Get("Form_Setup_LabelSeparacionX"));
        ToolTip.SetTip(SpacingYBox, StringResources.Get("Form_Setup_LabelSeparacionY"));
        SmallBox.Content = CutImagesViewModel.SmallPiecesText;
        ToolTip.SetTip(SmallBox, StringResources.Get("Form_Extraer_CheckBox_BorrarImagenesXTamano") + "(" + CutImagesViewModel.SmallPiecesText + ")");
        LineColorButton.Load(model.LineColor);
        LineColorButton.ColorChanged += _ => model.LineColor = LineColorButton.Color;
        model.Notice += message => _ = new MessageWindow(message).ShowDialog(this);
        model.PreviewChanged += (_, _) => Draw();
        ImagesBox.AddHandler(DragDrop.DropEvent, OnDrop);
        Draw();
    }

    public CutImagesViewModel Model { get; }

    /// <summary>Asks for the folder for the pieces; the test of the window replaces it.</summary>
    public Func<Task<string?>>? AskFolder { get; set; }

    /// <summary>Asks for a folder and cuts the images into it. Original: <c>Boton_Cortar_Click</c>.</summary>
    public async Task CutAsync()
    {
        string? folder = AskFolder != null ? await AskFolder() : await DeckWindows.PickFolderAsync(this);
        if (!string.IsNullOrEmpty(folder))
        {
            await Model.CutAsync(folder);
        }
    }

    private void Draw()
    {
        string? path = Model.SelectedImage;
        if (path != _shown)
        {
            _shown = path;
            _picture = DeckWindows.LoadPicture(path);
            _shownSize = (0, 0);
            if (_picture != null && path != null)
            {
                var info = new ImageMagick.MagickImageInfo(path);
                _shownSize = ((int)info.Width, (int)info.Height);
            }
        }

        (int width, int height) = _shownSize;
        Rect[] pieces = [.. Model.Cutter.Pieces(width, height).Select(piece => new Rect(piece.X, piece.Y, Math.Max(0, piece.Width), Math.Max(0, piece.Height)))];
        var kept = new Rect(
            Model.BorderLeft,
            Model.BorderTop,
            Math.Max(0, width - Model.BorderLeft - Model.BorderRight),
            Math.Max(0, height - Model.BorderTop - Model.BorderBottom));
        Preview.Show(_picture, width, height, pieces, kept, Model.LineColor.ToAvalonia(), Math.Clamp(Model.LineThickness, 1, 10));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Delete && ImagesBox.IsKeyboardFocusWithin)
        {
            Model.RemoveSelected();
            e.Handled = true;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e) =>
        Model.Add((e.DataTransfer.TryGetFiles() ?? []).Select(file => file.TryGetLocalPath()).OfType<string>());

    private async void OnAdd(object? sender, RoutedEventArgs e) => Model.Add(await ImageFiles.PickAsync(this, multiple: true));

    private async void OnAddFolder(object? sender, RoutedEventArgs e)
    {
        string? folder = await DeckWindows.PickFolderAsync(this);
        if (folder != null)
        {
            Model.Add([folder]);
        }
    }

    private void OnRemove(object? sender, RoutedEventArgs e) => Model.RemoveSelected();

    private void OnClear(object? sender, RoutedEventArgs e) => Model.Clear();

    private void OnReset(object? sender, RoutedEventArgs e) => Model.ResetBorders();

    private async void OnCut(object? sender, RoutedEventArgs e) => await CutAsync();
}
