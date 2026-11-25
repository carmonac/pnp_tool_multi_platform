using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PnpTool.App.Services;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.Views;

/// <summary>An image to put on a face of several cards.</summary>
/// <param name="Image">The image, as read from its file.</param>
/// <param name="Face">Front or back.</param>
/// <param name="OnSelected">True for the cards that have that face selected; false for a range.</param>
/// <param name="From">First position of the range, from 0.</param>
/// <param name="To">Position after the last of the range.</param>
public sealed record ImageChange(CardFace Image, FaceType Face, bool OnSelected, int From, int To);

/// <summary>
/// Asks for an image and for the cards whose front (or back) it replaces: the ones with that
/// face selected, or a range of positions. It closes with the answer, or with nothing when
/// cancelled. Original: <c>Window_Anadir_Muchas_Imagenes_v2</c> and
/// <c>FormPrincipalModeloLocal.Boton_AnadirImagenEnBloque_Click</c>.
/// </summary>
public partial class ChangeImagesWindow : Window
{
    private readonly int _cardCount;
    private readonly FaceType _face;
    private CardFace? _image;

    public ChangeImagesWindow()
        : this(1, FaceType.Front, 0)
    {
    }

    /// <param name="cardCount">How many cards the panel has.</param>
    /// <param name="face">The face to change: front or back.</param>
    /// <param name="selectedFaces">How many faces are selected in the panel.</param>
    public ChangeImagesWindow(int cardCount, FaceType face, int selectedFaces)
    {
        InitializeComponent();
        _cardCount = cardCount;
        _face = face;
        Title = StringResources.Get(face == FaceType.Front ? "Boton_Configurar_Cara_Frontal" : "Boton_Configurar_Cara_Trasera");
        Heading.Text = StringResources.Get("Cambiar_Cara");
        ToolTip.SetTip(PictureArea, StringResources.Get("Form_AnadirMuchasImagenes_PictureBox_ImagenACargar"));
        FromLabel.Text = StringResources.Get("Form_AnadirMuchasImagenes_Label_Desde");
        ToLabel.Text = StringResources.Get("Form_AnadirMuchasImagenes_Label_Hasta");
        FromBox.Text = "1";
        ToBox.Text = cardCount.ToString();

        // With nothing selected only a range can be chosen.
        SelectedOption.IsEnabled = selectedFaces != 0;
        SelectedOption.IsChecked = selectedFaces != 0;
        RangeOption.IsChecked = selectedFaces == 0;
        RangeFields.IsVisible = selectedFaces == 0;
        RangeOption.IsCheckedChanged += (_, _) => RangeFields.IsVisible = RangeOption.IsChecked == true;
        PictureArea.AddHandler(DragDrop.DropEvent, OnPictureDropped);
    }

    /// <summary>
    /// Takes an image file. A file that cannot be read as an image is refused.
    /// Original: <c>Form_AnadirMuchasImagenes_PictureBox_ImagenACargar_Click</c>.
    /// </summary>
    public async Task<bool> LoadImageAsync(string path)
    {
        CardFace face = await Task.Run(() => ImageInspector.Current.GetImageProperties(path));
        if (face.IsCorrupt || !File.Exists(path))
        {
            return false;
        }

        _image = face;
        Picture.Source = await FaceImages.LoadAsync(face, 512);
        ErrorText.Text = string.Empty;
        return true;
    }

    /// <summary>
    /// What is wrong with what the window shows, or an error of kind none.
    /// Original: <c>DatosValidos</c>.
    /// </summary>
    public AppError Validate()
    {
        var error = new AppError();
        string from = StringResources.Get("Form_AnadirMuchasImagenes_Label_Desde");
        string to = StringResources.Get("Form_AnadirMuchasImagenes_Label_Hasta");
        if (RangeOption.IsChecked == true)
        {
            if (FromBox.Value < 1)
            {
                error.Kind = ErrorKind.Error;
                error.Message = from + ">0";
            }

            if (ToBox.Value < FromBox.Value)
            {
                error.Kind = ErrorKind.Error;
                error.Message = to + "<" + from;
            }

            if (ToBox.Value > _cardCount)
            {
                error.Kind = ErrorKind.Error;
                error.Message = to + ">" + _cardCount;
            }
        }

        if (_image == null)
        {
            error.Kind = ErrorKind.MissingData;
        }

        return error;
    }

    /// <summary>Accepts what the window shows, if it is valid. Original: <c>Boton_insertar_Click</c>.</summary>
    public void Accept()
    {
        AppError error = Validate();
        if (error.HasError)
        {
            ErrorText.Text = error.Kind == ErrorKind.MissingData ? StringResources.Get("faltan_datos") : error.Message;
            return;
        }

        Close(new ImageChange(_image!, _face, SelectedOption.IsChecked == true, FromBox.Value - 1, ToBox.Value));
    }

    private async void OnPictureClicked(object? sender, PointerPressedEventArgs e)
    {
        IReadOnlyList<string> files = await ImageFiles.PickAsync(this, multiple: false);
        if (files.Count == 1)
        {
            await LoadImageAsync(files[0]);
        }
    }

    private async void OnPictureDropped(object? sender, DragEventArgs e)
    {
        string[] files = [.. (e.DataTransfer.TryGetFiles() ?? []).Select(file => file.TryGetLocalPath()).OfType<string>()];
        if (files.Length == 1)
        {
            await LoadImageAsync(files[0]);
        }
        else
        {
            ErrorText.Text = "only one file";
        }
    }

    private void OnAccepted(object? sender, EventArgs e) => Accept();

    private void OnCancelled(object? sender, EventArgs e) => Close((ImageChange?)null);
}
