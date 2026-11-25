using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Text;
using PnpTool.Imaging;

namespace PnpTool.App.ViewModels.Panel;

/// <summary>A face selected in the panel: which face of which card.</summary>
public readonly record struct SelectedFace(CardPanelViewModel Card, FaceType Face);

/// <summary>
/// The image panel: the cards of the project in order, what is selected in it and its
/// counts. Original: <c>PanelImagenes</c>, without the parts that come with later tasks
/// (drag and drop, context menus, undo, stacked layouts).
/// </summary>
public sealed partial class ImagePanelViewModel : ViewModelBase
{
    private readonly List<SelectedFace> _selectedFaces = [];
    private int _lastClicked = -1;
    private bool _batch;

    /// <summary>The cards, in the order they will be printed. Original: the children of <c>Panel_Imagenes</c>.</summary>
    public ObservableCollection<CardPanelViewModel> Cards { get; } = [];

    /// <summary>Raised when cards are added, removed or changed. Original: <c>ProyectoCambia</c>.</summary>
    public event EventHandler? ProjectChanged;

    /// <summary>Raised when the counts may have changed. Original: <c>NotificaCambioCartasCopias</c>.</summary>
    public event EventHandler? CountsChanged;

    /// <summary>Size of the cards on screen in percent, from 75 to 300. Original: <c>UI.ValorResize</c>.</summary>
    [ObservableProperty]
    private double _zoom = 100;

    /// <summary>Whether each card shows its back beside its front. Original: <c>PanelDoble</c>.</summary>
    [ObservableProperty]
    private bool _isDouble;

    /// <summary>The colour of new blank cards. Original: <c>ConfiguracionPrograma.ColorBlancoSeleccionado</c>.</summary>
    public ArgbColor BlankColor { get; set; } = ArgbColor.White;

    /// <summary>Longest side in pixels of the pictures in the panel, from the program's settings.</summary>
    public int ThumbnailSize => DisplayImages.ThumbnailSize(AppSettings.PanelResolution);

    /// <summary>Original: <c>ElementosTotales</c>.</summary>
    public int PanelCount => Cards.Count;

    /// <summary>Cards to print, copies counted. Original: <c>CartasTotales</c>.</summary>
    public int CardCount => Cards.Sum(card => card.Copies);

    /// <summary>Original: <c>ContadorPaneleSeleccionados</c>.</summary>
    public int SelectedPanelCount => Cards.Count(card => card.IsSelected);

    /// <summary>Cards to print among the selected ones. Original: <c>ContadorCartasSeleccionados</c>.</summary>
    public int SelectedCardCount => Cards.Where(card => card.IsSelected).Sum(card => card.Copies);

    /// <summary>The selected faces in the order they were selected. Original: <c>CarasSeleccionadas</c>.</summary>
    public IReadOnlyList<SelectedFace> SelectedFaces => _selectedFaces;

    /// <summary>Two faces are selected, so they can be swapped. Original: <c>ActivaIntercambia</c>.</summary>
    public bool CanSwapFaces => _selectedFaces.Count == 2;

    /// <summary>
    /// The text of the status line: panels and cards and, when some panel is selected, how
    /// many panels and cards are. Original: <c>ActualizaContadorCartasCopias</c>.
    /// </summary>
    public string CountsText
    {
        get
        {
            string text = StringResources.Get("Paneles") + ": " + PanelCount + " / " + StringResources.Get("Cartas") + ": " + CardCount;
            int selected = SelectedPanelCount;
            return selected == 0
                ? text
                : text + "   " + StringResources.Get("Elementos_Seleccionados") + ": " + selected + " / " + SelectedCardCount;
        }
    }

    /// <summary>The pictures being made; finished when every card shows its own.</summary>
    public Task ImagesLoaded => Task.WhenAll(Cards.Select(card => card.ImagesLoaded));

    // ---- Adding and removing ----

    /// <summary>
    /// Splits image files into fronts and backs. They are put in natural order ("2" before
    /// "10"); with backs shown they are then taken in pairs, front and back, and an odd
    /// number of files is an error. Original: <c>Subir_imagenes_WPF_SepararRutas</c>.
    /// </summary>
    public static (string[] Fronts, string[] Backs, AppError Error) SplitFiles(IEnumerable<string> files, bool isDouble)
    {
        string[] sorted = [.. files];
        Array.Sort(sorted, new NumericComparer());
        if (!isDouble)
        {
            return (sorted, [], new AppError());
        }

        if (sorted.Length % 2 != 0)
        {
            return ([], [], new AppError { Kind = ErrorKind.Error, Message = StringResources.Get("imagenes_par") });
        }

        string[] fronts = [.. sorted.Where((_, index) => index % 2 == 0)];
        string[] backs = [.. sorted.Where((_, index) => index % 2 == 1)];
        Array.Sort(fronts);
        Array.Sort(backs);
        return (fronts, backs, new AppError());
    }

    /// <summary>
    /// Reads image files, away from the user-interface thread, and adds a card for each (or
    /// for each pair, with backs shown) at the position, or at the end when it is -1. The
    /// number of copies comes from the file name ("dragon x3.png"). Gives the number of cards
    /// added and, for files that are not there, a warning naming them.
    /// Original: <c>Subir_imagenes_WPF</c> in the panel and in <c>FormPrincipalModeloLocal</c>.
    /// </summary>
    public async Task<(int Added, AppError Error)> AddImagesAsync(
        IEnumerable<string> files, int position = -1, IProgress<double>? progress = null)
    {
        (string[] fronts, string[] backs, AppError error) = SplitFiles(files, IsDouble);
        if (error.HasError || fronts.Length == 0)
        {
            return (0, error);
        }

        bool isDouble = IsDouble;
        Card[] cards = await Task.Run(() =>
        {
            var read = new Card[fronts.Length];
            for (int i = 0; i < fronts.Length; i++)
            {
                read[i] = new Card();
                if (isDouble)
                {
                    read[i].LoadFromFiles(fronts[i], ImageKind.Normal, backs[i], ImageKind.Normal);
                }
                else
                {
                    read[i].LoadFromFiles(fronts[i], ImageKind.Normal, string.Empty, ImageKind.Empty);
                }

                progress?.Report((i + 1) * 100.0 / fronts.Length);
            }

            return read;
        });

        AddCards(cards, position);
        string[] missing = [.. fronts.Concat(backs).Where(file => !File.Exists(file))];
        if (missing.Length != 0)
        {
            error = new AppError { Kind = ErrorKind.Warning, Message = string.Join(Environment.NewLine, missing) };
        }

        return (cards.Length, error);
    }

    /// <summary>
    /// Adds cards at the position, or at the end when it is -1 or past the end.
    /// Original: <c>CrearPanelImagen_WPF</c> and <c>Post_CreaPanelImagen_WPF</c>.
    /// </summary>
    public void AddCards(IEnumerable<Card> cards, int position = -1)
    {
        _batch = true;
        int at = position < 0 || position > Cards.Count ? Cards.Count : position;
        int first = at;
        foreach (Card card in cards)
        {
            var panel = new CardPanelViewModel(this, card);
            Cards.Insert(at++, panel);
            panel.RefreshImages();
        }

        _batch = false;
        Renumber(first);
        Changed();
    }

    /// <summary>Adds a card in the blank colour, on both faces. Original: <c>Subir_Imagenes_Blanca</c> and <c>Init_Blanca</c>.</summary>
    public void AddBlankCard(int position = -1)
    {
        string color = BlankColor.ToString();
        AddCards([NewCard(new CardFace(color, FaceType.Front, ImageKind.Blank), new CardFace(color, FaceType.Back, ImageKind.Blank))], position);
    }

    /// <summary>Adds a card without images, which leaves its place on the sheet free. Original: <c>Subir_Imagen_Vacia</c> and <c>Init_Vacia</c>.</summary>
    public void AddEmptyCard(int position = -1) =>
        AddCards([NewCard(new CardFace { FaceType = FaceType.Front, ImageKind = ImageKind.Empty }, new CardFace { FaceType = FaceType.Back, ImageKind = ImageKind.Empty })], position);

    /// <summary>
    /// Removes the selected cards, with the temporary files made for them. Gives how many
    /// were removed. Original: <c>BorrarPaneles</c> and <c>EliminaPanel</c>.
    /// </summary>
    public int DeleteSelected()
    {
        List<CardPanelViewModel> selected = [.. Cards.Where(card => card.IsSelected)];
        if (selected.Count == 0)
        {
            return 0;
        }

        _batch = true;
        foreach (CardPanelViewModel card in selected)
        {
            Remove(card);
        }

        _batch = false;
        Renumber(0);
        Changed();
        return selected.Count;
    }

    /// <summary>Removes every card. Original: <c>BorrarTodo</c> and <c>NuevoProyecto</c>.</summary>
    public void Clear()
    {
        foreach (CardPanelViewModel card in Cards)
        {
            card.Release();
        }

        Cards.Clear();
        _selectedFaces.Clear();
        _lastClicked = -1;
        Changed();
    }

    /// <summary>
    /// Removes one card. Original: <c>PanelEntrada_ContextMenuEliminar_Click</c>.
    /// </summary>
    public void DeleteAt(int index)
    {
        if (!IsCard(index))
        {
            return;
        }

        _batch = true;
        Remove(Cards[index]);
        _batch = false;
        Renumber(0);
        Changed();
    }

    /// <summary>
    /// Adds a copy of a card just before it. Original: <c>PanelEntrada_ContextMenuClonar_Click</c>.
    /// </summary>
    public void Clone(int index)
    {
        if (IsCard(index))
        {
            AddCards([Cards[index].Card], index);
        }
    }

    /// <summary>
    /// Moves a card so that it ends at the given position, counted from 0; the cards in
    /// between shift by one. A position past the end means the end. Original:
    /// <c>MuevePanelA</c>, used by "move to" and by dropping a card on another.
    /// </summary>
    public void Move(int from, int to)
    {
        if (!IsCard(from))
        {
            return;
        }

        to = Math.Clamp(to, 0, Cards.Count - 1);
        if (from == to)
        {
            return;
        }

        Cards.Move(from, to);
        Renumber(Math.Min(from, to));
        _lastClicked = -1;
        Changed();
    }

    /// <summary>
    /// Gives a number of copies to a card and to every selected card.
    /// Original: <c>PanelEntrada_ContextMenuCambiarCopias_Click</c>.
    /// </summary>
    public void SetCopies(int index, int copies)
    {
        _batch = true;
        for (int i = 0; i < Cards.Count; i++)
        {
            if (i == index || Cards[i].IsSelected)
            {
                Cards[i].Copies = copies;
            }
        }

        _batch = false;
        Changed();
    }

    // ---- Changing faces ----

    /// <summary>
    /// The faces an action on a face applies to: that face alone when it is not selected,
    /// every selected face when it is. Original: the choice between acting on the panel and
    /// calling <c>ReportaCambioSeleccionCara</c> in <c>PanelCarta_WPF</c>.
    /// </summary>
    public IReadOnlyList<SelectedFace> FacesFor(int index, FaceType face)
    {
        if (!IsCard(index))
        {
            return [];
        }

        if (!Cards[index].IsFaceSelected(face))
        {
            return [new SelectedFace(Cards[index], face)];
        }

        return
        [
            .. Cards.SelectMany(card => new[] { new SelectedFace(card, FaceType.Front), new SelectedFace(card, FaceType.Back) })
                .Where(entry => entry.Card.IsFaceSelected(entry.Face) && (IsDouble || entry.Face == FaceType.Front)),
        ];
    }

    /// <summary>
    /// Turns or mirrors a face, or every selected face when that one is selected. Original:
    /// <c>RotaryTransformarImagenDesdeMenu</c> and <c>HayCambioSeleccionCara_RotacionBucle</c>.
    /// </summary>
    public void RotateFace(int index, FaceType face, RotationProperties change)
    {
        foreach ((CardPanelViewModel card, FaceType type) in FacesFor(index, face))
        {
            card.Face(type).ApplyRotation(change);
            card.LayoutChanged();
            card.RefreshImages();
        }

        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Puts another image on a face, or on every selected face when that one is selected.
    /// The number of copies of the card is kept. Original: <c>CambiarImagen</c> and
    /// <c>HayCambioSeleccionCara_NoRotacionBucle</c>.
    /// </summary>
    /// <remarks>
    /// The original copied the new image as a front face even onto a back; here the face
    /// stays the face it was.
    /// </remarks>
    public void ChangeFaceImage(int index, FaceType face, string path)
    {
        var read = new Card();
        read.LoadFromFiles(path, ImageKind.Normal, string.Empty, ImageKind.Empty);
        ReplaceFaces(FacesFor(index, face), read.Front);
    }

    /// <summary>
    /// Makes a face blank, in the blank colour, or every selected face when that one is
    /// selected. Original: <c>CambiarImagenBlanco</c>.
    /// </summary>
    public void ChangeFaceToBlank(int index, FaceType face) =>
        ReplaceFaces(FacesFor(index, face), new CardFace(BlankColor.ToString(), FaceType.Front, ImageKind.Blank));

    /// <summary>
    /// Puts one image on the given face of the cards in a range of positions.
    /// Original: <c>AnadirImagenEnBloque_Rango</c>.
    /// </summary>
    /// <param name="source">The image, as read from its file.</param>
    /// <param name="face">Front or back.</param>
    /// <param name="from">First position, from 0.</param>
    /// <param name="to">Position after the last.</param>
    public void ChangeImages(CardFace source, FaceType face, int from, int to) =>
        ReplaceFaces([.. Cards.Skip(Math.Max(from, 0)).Take(Math.Max(to - Math.Max(from, 0), 0)).Select(card => new SelectedFace(card, face))], source);

    /// <summary>
    /// Puts one image on the given face of every card that has that face selected.
    /// Original: <c>AnadirImagenEnBloque_Seleccionadas</c>.
    /// </summary>
    public void ChangeSelectedImages(CardFace source, FaceType face) =>
        ReplaceFaces([.. Cards.Where(card => card.IsFaceSelected(face)).Select(card => new SelectedFace(card, face))], source);

    /// <summary>
    /// Sets the colour painted behind a face. Original: <c>CambiaColor</c> of <c>PanelCarta_WPF</c>.
    /// </summary>
    public void SetFaceBackground(int index, FaceType face, ArgbColor color)
    {
        if (!IsCard(index))
        {
            return;
        }

        Cards[index].Face(face).BackgroundColor = color.ToString();
        Cards[index].LayoutChanged();
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Whether the menus offer a background colour for fronts and for backs; the project's
    /// settings decide. Original: <c>HabilitarCambioColorEnCaras</c>.
    /// </summary>
    public (bool Front, bool Back) BackgroundColorOffered { get; set; }

    // ---- Dropped files ----

    /// <summary>Raised with the path when a single project file is dropped on the panel.</summary>
    public event Action<string>? ProjectFileDropped;

    /// <summary>
    /// The image files among what was dropped: files that are images, and the images inside
    /// dropped folders. Original: <c>FormPrincipalModelo.ProcesaRutasDragDrop</c>.
    /// </summary>
    public static string[] ImagesAmong(IEnumerable<string> paths)
    {
        var images = new List<string>();
        foreach (string path in paths.Order(new NumericComparer()))
        {
            if (Directory.Exists(path))
            {
                images.AddRange(Directory.GetFiles(path).Where(ImageFormats.IsImage).Order(StringComparer.Ordinal));
            }
            else if (ImageFormats.IsImage(path))
            {
                images.Add(path);
            }
        }

        return [.. images];
    }

    /// <summary>
    /// Takes files dropped from the file manager. A single project file asks for that project
    /// to be opened; otherwise the images are added, before the card they were dropped on or
    /// at the end. Gives what <see cref="AddImagesAsync"/> gives.
    /// Original: <c>PanelImagenes_DropDirecto</c>.
    /// </summary>
    public async Task<(int Added, AppError Error)> DropFilesAsync(IReadOnlyList<string> paths, int onCard = -1)
    {
        if (paths.Count == 1 && Path.GetExtension(paths[0]) == ".pnp")
        {
            ProjectFileDropped?.Invoke(paths[0]);
            return (0, new AppError());
        }

        string[] images = await Task.Run(() => ImagesAmong(paths));
        if (images.Length == 0)
        {
            return (0, new AppError());
        }

        (int added, AppError error) = await AddImagesAsync(images, IsCard(onCard) ? onCard : -1);
        _lastClicked = -1;
        return (added, error);
    }

    // ---- Selection ----

    /// <summary>
    /// A click on a card. Without keys it selects the card alone, or unselects it if it was
    /// selected, leaving the others as they are. With the command key (Ctrl, or Cmd on macOS)
    /// it adds the card to the selection or takes it away. With Shift it also selects the
    /// cards between the last card clicked without Shift and this one.
    /// Original: <c>PanelCarta_MouseDown(int, bool)</c> and <c>BotonSeleccionarCarta</c>.
    /// </summary>
    /// <remarks>
    /// Kept from the original: with Shift the card clicked changes its own state like with
    /// any other click, and the card the range starts from is not touched.
    /// </remarks>
    public void ClickCard(int index, bool shift, bool command)
    {
        if (index < 0 || index >= Cards.Count)
        {
            return;
        }

        _batch = true;
        bool wasSelected = Cards[index].IsSelected;
        if (!shift)
        {
            _lastClicked = index;
            if (!command && !wasSelected)
            {
                ClearCardSelection();
            }
        }
        else
        {
            int anchor = _lastClicked == -1 ? 0 : _lastClicked;
            for (int i = Math.Min(anchor, index) + 1; i < Math.Max(anchor, index); i++)
            {
                Cards[i].IsSelected = true;
            }
        }

        Cards[index].IsSelected = !wasSelected;
        _batch = false;
        NotifyCounts();
    }

    /// <summary>
    /// A click on the picture of a face selects that face, or unselects it. Faces are
    /// selected apart from cards. Original: <c>SeleccionarImagen</c> and
    /// <c>PanelEntrada_Boton_PanelCarta_Click</c>.
    /// </summary>
    public void ClickFace(int index, FaceType face)
    {
        if (index < 0 || index >= Cards.Count || Cards[index].Face(face).ImageKind == ImageKind.Empty)
        {
            return;
        }

        Cards[index].SetFaceSelected(face, !Cards[index].IsFaceSelected(face));
    }

    /// <summary>Unselects the cards, leaving the faces. Original: <c>BorrarSeleccion_Paneles</c>.</summary>
    public void ClearCardSelection()
    {
        bool outer = !_batch;
        _batch = true;
        foreach (CardPanelViewModel card in Cards)
        {
            card.IsSelected = false;
        }

        if (outer)
        {
            _batch = false;
            NotifyCounts();
        }
    }

    /// <summary>Unselects cards and faces. Original: <c>BorrarSeleccion</c>.</summary>
    public void ClearSelection()
    {
        _batch = true;
        foreach (CardPanelViewModel card in Cards)
        {
            card.IsSelected = false;
            card.IsFrontSelected = false;
            card.IsBackSelected = false;
        }

        _batch = false;
        NotifyCounts();
    }

    /// <summary>
    /// Selects cards, faces or both in a range of positions, after unselecting everything.
    /// Original: <c>SeleccionarTodo</c> and <c>MarcarSeleccion</c>.
    /// </summary>
    /// <param name="from">First position, from 0.</param>
    /// <param name="to">Position after the last.</param>
    /// <param name="cards">Whether to select the cards.</param>
    /// <param name="faces">Which faces to select: front, back, both or none.</param>
    public void Select(int from, int to, bool cards, FaceType faces)
    {
        ClearSelection();
        _batch = true;
        for (int i = Math.Max(from, 0); i < Math.Min(to, Cards.Count); i++)
        {
            if (cards)
            {
                Cards[i].IsSelected = true;
            }

            if (faces is FaceType.Front or FaceType.Both)
            {
                Cards[i].IsFrontSelected = true;
            }

            if (IsDouble && faces is FaceType.Back or FaceType.Both)
            {
                Cards[i].IsBackSelected = true;
            }
        }

        _batch = false;
        NotifyCounts();
    }

    /// <summary>
    /// Exchanges the images of the two selected faces; each stays selected. Does nothing
    /// unless exactly two faces are selected. Original: <c>IntercambiarImagenes</c>.
    /// </summary>
    public bool SwapFaces()
    {
        if (!CanSwapFaces)
        {
            return false;
        }

        (CardPanelViewModel firstCard, FaceType firstType) = _selectedFaces[0];
        (CardPanelViewModel secondCard, FaceType secondType) = _selectedFaces[1];
        CardFace first = firstCard.Face(firstType);
        CardFace second = secondCard.Face(secondType);
        var kept = new CardFace();
        kept.CopyFrom(first);
        first.CopyFrom(second);
        first.FaceType = firstType;
        second.CopyFrom(kept);
        second.FaceType = secondType;
        firstCard.LayoutChanged();
        secondCard.LayoutChanged();
        firstCard.RefreshImages();
        if (!ReferenceEquals(firstCard, secondCard))
        {
            secondCard.RefreshImages();
        }

        ProjectChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Makes every picture again. Original: <c>RefrescaTodosLosPaneles</c>.</summary>
    public void RefreshImages()
    {
        foreach (CardPanelViewModel card in Cards)
        {
            card.RefreshImages();
        }
    }

    /// <summary>
    /// The cards as a deck, in order, each with its copies. Original:
    /// <c>GenerarMazoDesdePaneles</c> without stacking.
    /// </summary>
    public Deck ToDeck()
    {
        var deck = new Deck();
        foreach (CardPanelViewModel card in Cards)
        {
            deck.Add(card.Card);
        }

        return deck;
    }

    /// <summary>Megabytes on disk of the images shown. Original: <c>CalculaTamanoTotalEnDisco</c>.</summary>
    public double SizeOnDiskMb() =>
        Cards.Sum(card => card.Card.Front.SizeOnDiskMb + (IsDouble ? card.Card.Back.SizeOnDiskMb : 0.0));

    // ---- Called by the cards ----

    internal void SelectionChanged() => NotifyCounts();

    internal void CopiesChanged()
    {
        NotifyCounts();
        if (!_batch)
        {
            ProjectChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal void FaceSelectionChanged(CardPanelViewModel card, FaceType face, bool selected)
    {
        var entry = new SelectedFace(card, face);
        _selectedFaces.Remove(entry);
        if (selected)
        {
            _selectedFaces.Add(entry);
        }

        OnPropertyChanged(nameof(CanSwapFaces));
        NotifyCounts();
    }

    partial void OnZoomChanged(double value)
    {
        foreach (CardPanelViewModel card in Cards)
        {
            card.LayoutChanged();
        }
    }

    partial void OnIsDoubleChanged(bool value)
    {
        foreach (CardPanelViewModel card in Cards)
        {
            if (!value)
            {
                card.IsBackSelected = false;
            }

            card.LayoutChanged();
            card.RefreshImages();
        }
    }

    private bool IsCard(int index) => index >= 0 && index < Cards.Count;

    /// <summary>Gives the faces the image of another, each keeping its side and whether it is selected.</summary>
    private void ReplaceFaces(IReadOnlyList<SelectedFace> faces, CardFace source)
    {
        foreach ((CardPanelViewModel card, FaceType type) in faces)
        {
            CardFace face = card.Face(type);
            bool selected = face.Selected;
            face.CopyFrom(source);
            face.FaceType = type;
            face.Selected = selected;
            card.LayoutChanged();
            card.RefreshImages();
        }

        if (faces.Count != 0)
        {
            ProjectChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static Card NewCard(CardFace front, CardFace back) => new() { Front = front, Back = back, Copies = 1 };

    private void Remove(CardPanelViewModel card)
    {
        card.IsSelected = false;
        card.IsFrontSelected = false;
        card.IsBackSelected = false;
        card.Release();
        card.Card.DeleteTempFiles();
        Cards.Remove(card);
    }

    /// <summary>Original: <c>ActualizaLabelCartas</c>.</summary>
    private void Renumber(int from)
    {
        for (int i = from; i < Cards.Count; i++)
        {
            Cards[i].Index = i;
        }

        _lastClicked = _lastClicked < Cards.Count ? _lastClicked : -1;
    }

    private void Changed()
    {
        NotifyCounts();
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyCounts()
    {
        if (_batch)
        {
            return;
        }

        OnPropertyChanged(nameof(CountsText));
        OnPropertyChanged(nameof(PanelCount));
        OnPropertyChanged(nameof(CardCount));
        OnPropertyChanged(nameof(SelectedPanelCount));
        OnPropertyChanged(nameof(SelectedCardCount));
        CountsChanged?.Invoke(this, EventArgs.Empty);
    }
}
