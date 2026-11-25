using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.App.Services;
using PnpTool.Core.Cards;
using PnpTool.Core.Primitives;

namespace PnpTool.App.ViewModels.Panel;

/// <summary>
/// One card in the image panel: its number and sizes, the pictures of its faces, what is
/// selected and how many copies are wanted. Original: <c>PanelCarta_WPF</c>.
/// </summary>
public sealed partial class CardPanelViewModel : ViewModelBase
{
    private readonly ImagePanelViewModel _owner;
    private CancellationTokenSource _loading = new();

    /// <param name="owner">The panel the card is in.</param>
    /// <param name="card">The card; the panel keeps its own copy.</param>
    public CardPanelViewModel(ImagePanelViewModel owner, Card card)
    {
        _owner = owner;
        Card = new Card();
        Card.CopyFrom(card);
        Card.Selected = false;
        Card.Front.Selected = false;
        Card.Back.Selected = false;
        _copies = Card.Copies;
    }

    /// <summary>Original: <c>CartaLocal</c>.</summary>
    public Card Card { get; }

    /// <summary>Position in the panel, from 0. Original: <c>IndiceCarta</c>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private int _index;

    /// <summary>Whether the whole card is selected. Original: <c>CartaLocal.Seleccionada</c> and <c>CheckPanel</c>.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Original: <c>CaraFrontal.Seleccionada</c> and <c>CheckFrontal</c>.</summary>
    [ObservableProperty]
    private bool _isFrontSelected;

    /// <summary>Original: <c>CaraTrasera.Seleccionada</c> and <c>CheckTrasera</c>.</summary>
    [ObservableProperty]
    private bool _isBackSelected;

    /// <summary>Original: <c>NumeroCopias</c>.</summary>
    [ObservableProperty]
    private int _copies;

    [ObservableProperty]
    private Bitmap? _frontImage;

    [ObservableProperty]
    private Bitmap? _backImage;

    /// <summary>Whether the card shows its back beside its front. Original: <c>PanelDoble</c>.</summary>
    public bool IsDouble => _owner.IsDouble;

    /// <summary>Width on screen at the panel's zoom. Original: <c>Resize</c>.</summary>
    public double Width => Math.Round((IsDouble ? 190 : 95) * _owner.Zoom / 100.0);

    /// <summary>Height on screen at the panel's zoom.</summary>
    public double Height => Math.Round(155 * _owner.Zoom / 100.0);

    /// <summary>
    /// "#3 63.5-88.9", the number of the card and the size of its front, followed by
    /// " @ " and the size of the back when backs are shown. Original: <c>EstableTextoTitulo</c>.
    /// </summary>
    public string Title
    {
        get
        {
            string title = "#" + (Index + 1) + " " + SizeText(Card.Front);
            return IsDouble ? title + " @ " + SizeText(Card.Back) : title;
        }
    }

    /// <summary>
    /// The colour chosen as background of the front, or null when none (or white) was chosen.
    /// It frames the picture. Original: <c>CambiaColoresBotonSeleccionado</c>.
    /// </summary>
    public ArgbColor? FrontBackground => Background(Card.Front);

    public ArgbColor? BackBackground => Background(Card.Back);

    /// <summary>The pictures being made; finished when both faces show theirs.</summary>
    public Task ImagesLoaded { get; private set; } = Task.CompletedTask;

    public CardFace Face(FaceType face) => face == FaceType.Back ? Card.Back : Card.Front;

    public bool IsFaceSelected(FaceType face) => face == FaceType.Back ? IsBackSelected : IsFrontSelected;

    public void SetFaceSelected(FaceType face, bool selected)
    {
        if (face == FaceType.Back)
        {
            IsBackSelected = selected;
        }
        else
        {
            IsFrontSelected = selected;
        }
    }

    /// <summary>
    /// Makes the pictures of the faces again, in the background. Original:
    /// <c>RefrescaImagenesPanel</c> and <c>ActualizaControlImagen</c>.
    /// </summary>
    public void RefreshImages()
    {
        _loading.Cancel();
        _loading = new CancellationTokenSource();
        CancellationToken cancellation = _loading.Token;
        int size = _owner.ThumbnailSize;
        ImagesLoaded = Task.WhenAll(
            LoadAsync(FaceType.Front, size, cancellation),
            IsDouble ? LoadAsync(FaceType.Back, size, cancellation) : Task.CompletedTask);
    }

    /// <summary>Gives the card back what it held at another moment, leaving what is selected as it is.</summary>
    public void RestoreCard(Card state)
    {
        Card.CopyFrom(state);
        Card.Selected = IsSelected;
        Card.Front.Selected = IsFrontSelected;
        Card.Back.Selected = IsBackSelected;
        Copies = state.Copies;
    }

    /// <summary>Tells the view that sizes and title depend on something that changed in the panel.</summary>
    public void LayoutChanged()
    {
        OnPropertyChanged(nameof(IsDouble));
        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Height));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(FrontBackground));
        OnPropertyChanged(nameof(BackBackground));
    }

    /// <summary>Stops the pictures being made and lets go of the ones shown. Original: <c>QuitaImagenes</c>.</summary>
    public void Release()
    {
        _loading.Cancel();
        FrontImage = null;
        BackImage = null;
    }

    private async Task LoadAsync(FaceType face, int size, CancellationToken cancellation)
    {
        Bitmap? picture = await FaceImages.LoadAsync(Face(face), size, cancellation);
        if (cancellation.IsCancellationRequested)
        {
            return;
        }

        if (face == FaceType.Back)
        {
            BackImage = picture;
        }
        else
        {
            FrontImage = picture;
        }
    }

    partial void OnIsSelectedChanged(bool value)
    {
        Card.Selected = value;
        _owner.SelectionChanged();
    }

    partial void OnIsFrontSelectedChanged(bool value)
    {
        Card.Front.Selected = value;
        _owner.FaceSelectionChanged(this, FaceType.Front, value);
    }

    partial void OnIsBackSelectedChanged(bool value)
    {
        Card.Back.Selected = value;
        _owner.FaceSelectionChanged(this, FaceType.Back, value);
    }

    partial void OnCopiesChanged(int value)
    {
        Card.Copies = value;
        _owner.CopiesChanged();
    }

    private static ArgbColor? Background(CardFace face)
    {
        if (string.IsNullOrEmpty(face.BackgroundColor))
        {
            return null;
        }

        ArgbColor color = ArgbColor.Parse(face.BackgroundColor);
        return color == ArgbColor.White ? null : color;
    }

    private static string SizeText(CardFace face) => face.Size.X.ToDisplayString() + "-" + face.Size.Y.ToDisplayString();
}
