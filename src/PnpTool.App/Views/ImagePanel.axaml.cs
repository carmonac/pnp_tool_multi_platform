using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PnpTool.App.ViewModels.Panel;
using PnpTool.App.Services;
using PnpTool.Controls;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.App.Views;

/// <summary>One row of cards in the image panel.</summary>
public sealed record CardRow(IReadOnlyList<CardPanelViewModel> Cards);

/// <summary>
/// The image panel: the cards of the project, wrapped in rows. It turns clicks into
/// selections and shows a larger view of the face the pointer rests on.
/// Original: <c>PanelImagenes</c>.
/// </summary>
/// <remarks>
/// The original put every card in a wrap panel, so each card of the project had its controls
/// whether in view or not. Here the cards are cut into rows that fit the width, and only the
/// rows in view have controls, so a large project costs little more than a small one.
/// </remarks>
public partial class ImagePanel : UserControl
{
    /// <summary>How long the pointer must rest on a face before its larger view appears.</summary>
    public static readonly TimeSpan ZoomDelay = TimeSpan.FromMilliseconds(500);

    private ImagePanelViewModel? _panel;
    private bool _rowsPending;
    private int _columns;
    private CardPanel? _hoverCard;
    private FaceType _hoverFace;
    private CancellationTokenSource _hover = new();

    public ImagePanel()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnPressed, handledEventsToo: false);
        AddHandler(PointerMovedEvent, OnMoved, handledEventsToo: true);
        AddHandler(PointerExitedEvent, (_, _) => HideZoom(), handledEventsToo: true);
        AddHandler(PointerWheelChangedEvent, OnWheel, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Scroller.SizeChanged += (_, _) => RowsChanged(onlyIfColumnsChange: true);
        AddHandler(ContextRequestedEvent, OnContextRequested);
        AddHandler(PointerReleasedEvent, (_, _) => _press = null, handledEventsToo: true);
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    /// <summary>How many cards fit in a row at the current width and zoom.</summary>
    public int Columns => _columns;

    /// <summary>
    /// Raised when the wheel is turned with the command key held: 1 to enlarge the cards, -1
    /// to reduce them. Original: the wheel handling of <c>Window_Principal_v2</c>.
    /// </summary>
    public event Action<int>? ZoomStepRequested;

    private ImagePanelViewModel? Panel => DataContext as ImagePanelViewModel;

    /// <summary>The card and face under a control, if any.</summary>
    public static (CardPanel? Card, FaceType? Face) Locate(object? source)
    {
        FaceType? face = null;
        for (Visual? visual = source as Visual; visual != null; visual = visual.GetVisualParent())
        {
            if (visual is Border { Tag: string tag } && visual.Classes.Contains("face"))
            {
                face = tag == "Back" ? FaceType.Back : FaceType.Front;
            }

            if (visual is CardPanel card)
            {
                return (card, face);
            }
        }

        return (null, null);
    }

    /// <summary>Ctrl, or Cmd on macOS.</summary>
    private KeyModifiers CommandModifier =>
        Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

    /// <summary>
    /// A press on a picture selects that face; anywhere else on a card it selects the card;
    /// outside the cards it unselects them. Original: <c>BotonSeleccionarCarta</c>,
    /// <c>BotonSeleccionarImagen</c> and <c>Panel_Imagenes_MouseDown</c>.
    /// </summary>
    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Panel == null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        HideZoom();
        (CardPanel? card, FaceType? face) = Locate(e.Source);
        if (card?.Card == null)
        {
            Panel.ClearCardSelection();
            return;
        }

        // The copies box is for typing, not for selecting.
        if (e.Source is Visual source && source.FindAncestorOfType<NumericBox>(includeSelf: true) != null)
        {
            return;
        }

        _press = (e, e.GetPosition(this), card.Card);

        if (face != null)
        {
            Panel.ClickFace(card.Card.Index, face.Value);
        }
        else
        {
            Panel.ClickCard(
                card.Card.Index,
                shift: e.KeyModifiers.HasFlag(KeyModifiers.Shift),
                command: e.KeyModifiers.HasFlag(CommandModifier));
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_panel != null)
        {
            _panel.Cards.CollectionChanged -= OnCardsChanged;
            _panel.PropertyChanged -= OnPanelChanged;
        }

        _panel = Panel;
        if (_panel != null)
        {
            _panel.Cards.CollectionChanged += OnCardsChanged;
            _panel.PropertyChanged += OnPanelChanged;
        }

        RowsChanged(onlyIfColumnsChange: false);
    }

    private void OnCardsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        RowsChanged(onlyIfColumnsChange: false);

    private void OnPanelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImagePanelViewModel.Zoom) or nameof(ImagePanelViewModel.IsDouble))
        {
            RowsChanged(onlyIfColumnsChange: true);
        }
    }

    /// <summary>
    /// Asks for the rows to be cut again. Many changes in a row (adding five hundred cards,
    /// say) cut them once, when the user interface is next idle.
    /// </summary>
    private void RowsChanged(bool onlyIfColumnsChange)
    {
        if (onlyIfColumnsChange && ColumnsThatFit() == _columns)
        {
            return;
        }

        if (!_rowsPending)
        {
            _rowsPending = true;
            Dispatcher.UIThread.Post(BuildRows, DispatcherPriority.Loaded);
        }
    }

    private int ColumnsThatFit()
    {
        if (_panel == null || _panel.Cards.Count == 0)
        {
            return 1;
        }

        // Each card has a margin of 4 on every side.
        double cardWidth = _panel.Cards[0].Width + 8;
        double available = Scroller.Bounds.Width - 20;   // room for the scroll bar
        return Math.Max(1, (int)(available / cardWidth));
    }

    private void BuildRows()
    {
        _rowsPending = false;
        HideZoom();
        _columns = ColumnsThatFit();
        Vector offset = Scroller.Offset;
        RowList.ItemsSource = _panel == null
            ? null
            : _panel.Cards.Chunk(_columns).Select(cards => new CardRow(cards)).ToArray();
        Scroller.Offset = offset;
    }

    /// <summary>Original: <c>PanelesImagenes_MouseMove</c> and <c>Timer_PanelZoom_Tick</c>.</summary>
    private async void OnMoved(object? sender, PointerEventArgs e)
    {
        MaybeStartDrag(e);
        (CardPanel? card, FaceType? face) = Locate(e.Source);
        if (card?.Card == null || face == null || card.Card.Face(face.Value).ImageKind == ImageKind.Empty)
        {
            HideZoom();
            return;
        }

        if (ReferenceEquals(card, _hoverCard) && face == _hoverFace)
        {
            return;
        }

        HideZoom();
        _hoverCard = card;
        _hoverFace = face.Value;
        CancellationToken resting = _hover.Token;
        try
        {
            await Task.Delay(ZoomDelay, resting);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        ShowZoom();
    }

    /// <summary>Original: <c>MuestraZoom</c>.</summary>
    private void ShowZoom()
    {
        if (_hoverCard?.Card == null)
        {
            return;
        }

        ZoomPopup.PlacementTarget = _hoverCard;
        ZoomPopup.IsOpen = true;
        _ = ZoomPanel.ShowAsync(_hoverCard.Card.Face(_hoverFace));
    }

    /// <summary>Original: <c>PanelesImagenes_MouseLeave</c>.</summary>
    private void HideZoom()
    {
        _hover.Cancel();
        _hover = new CancellationTokenSource();
        _hoverCard = null;
        if (ZoomPopup.IsOpen)
        {
            ZoomPopup.IsOpen = false;
            ZoomPanel.Clear();
        }
    }

    // ---- Context menus ----

    /// <summary>
    /// The menu for a place in the panel: for the picture of a face (unless the face has no
    /// image), for the rest of a card, or for the panel outside the cards. Original:
    /// <c>Control_MenuContextual_PanelImagen</c>, <c>Control_MenuContextual_PanelCarta</c> and
    /// <c>Control_MenuContextual_PanelPrincipal</c>.
    /// </summary>
    public ContextMenu BuildMenu(CardPanelViewModel? card, FaceType? face)
    {
        var menu = new ContextMenu();
        if (Panel is not { } panel)
        {
            return menu;
        }

        if (card == null)
        {
            menu.Items.Add(Item("Añadir_imagen_final", () => AddImagesAt(-1)));
            menu.Items.Add(Item("Añadir_imagen_blanca_final", () => panel.AddBlankCard()));
            menu.Items.Add(Item("Añadir_imagen_vacia_final", () => panel.AddEmptyCard()));
            return menu;
        }

        if (face is { } side && card.Face(side).ImageKind != ImageKind.Empty)
        {
            bool colorOffered = side == FaceType.Front ? panel.BackgroundColorOffered.Front : panel.BackgroundColorOffered.Back;
            menu.Items.Add(Item("Cambiar_Cara", () => ChangeImage(card, side)));
            menu.Items.Add(Item("imagen_blanco", () => panel.ChangeFaceToBlank(card.Index, side)));
            menu.Items.Add(Item("ContextMenuStrip_BotonRotar_Izquierda", () => panel.RotateFace(card.Index, side, new RotationProperties(-90, false, false))));
            menu.Items.Add(Item("ContextMenuStrip_BotonRotar_Derecha", () => panel.RotateFace(card.Index, side, new RotationProperties(90, false, false))));
            menu.Items.Add(Item("ContextMenuStrip_BotonRotar_Vertical", () => panel.RotateFace(card.Index, side, new RotationProperties(0, false, true))));
            menu.Items.Add(Item("ContextMenuStrip_BotonRotar_Horizontal", () => panel.RotateFace(card.Index, side, new RotationProperties(0, true, false))));
            if (colorOffered)
            {
                menu.Items.Add(Item("Color", () => ChooseBackground(card, side)));
            }

            menu.Items.Add(Item("MenuContextualPanelImagen_AbrirCon", () => Platform.Services.OpenFile(card.Face(side).OriginalPath)));
            menu.Items.Add(Item("MenuContextualPanelImagen_Explorador", () => Platform.Services.RevealFile(card.Face(side).OriginalPath)));
            return menu;
        }

        menu.Items.Add(Item("Añadir_imagen_aquí", () => AddImagesAt(card.Index)));
        menu.Items.Add(Item("Añadir_imagen_blanca_aquí", () => panel.AddBlankCard(card.Index)));
        menu.Items.Add(Item("Añadir_imagen_vacia_aquí", () => panel.AddEmptyCard(card.Index)));
        menu.Items.Add(Item("Clonar", () => panel.Clone(card.Index)));
        menu.Items.Add(Item("Mover_A", () => MoveTo(card)));
        menu.Items.Add(Item("Quitar_Carta", () => panel.DeleteAt(card.Index)));
        menu.Items.Add(Item("MenuContextualPanelImagen_CambiarCopias", () => ChangeCopies(card)));
        return menu;
    }

    private static MenuItem Item(string key, Action action)
    {
        var item = new MenuItem { Header = StringResources.Get(key), Tag = key };
        item.Click += (_, _) => action();
        return item;
    }

    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        HideZoom();
        (CardPanel? card, FaceType? face) = Locate(e.Source);
        ContextMenu menu = BuildMenu(card?.Card, face);
        if (menu.Items.Count != 0)
        {
            menu.Open(this);
            e.Handled = true;
        }
    }

    private Window? Owner => TopLevel.GetTopLevel(this) as Window;

    /// <summary>Original: <c>AnadirImagenNormal</c> from the menus.</summary>
    private async void AddImagesAt(int position)
    {
        if (Panel == null || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        if (Panel.RequestImages != null)
        {
            await Panel.RequestImages(position);
            return;
        }

        IReadOnlyList<string> files = await ImageFiles.PickAsync(top, multiple: true);
        if (files.Count != 0)
        {
            Report(await Panel.AddImagesAsync(files, position));
        }
    }

    /// <summary>Original: <c>CambiarImagen</c> from the menu.</summary>
    private async void ChangeImage(CardPanelViewModel card, FaceType face)
    {
        if (Panel == null || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        IReadOnlyList<string> files = await ImageFiles.PickAsync(top, multiple: false);
        if (files.Count != 0)
        {
            Panel.ChangeFaceImage(card.Index, face, files[0]);
        }
    }

    /// <summary>Original: <c>CambiaColor</c>.</summary>
    private async void ChooseBackground(CardPanelViewModel card, FaceType face)
    {
        if (Panel == null || Owner == null)
        {
            return;
        }

        ArgbColor? color = await new ColorPickerDialog(ArgbColor.White).ShowDialog<ArgbColor?>(Owner);
        if (color != null)
        {
            Panel.SetFaceBackground(card.Index, face, color.Value);
        }
    }

    /// <summary>Original: <c>PanelEntrada_ContextMenuMoverA_Click</c>.</summary>
    private async void MoveTo(CardPanelViewModel card)
    {
        if (Panel == null || Owner == null)
        {
            return;
        }

        int? position = await new MoveToWindow(Panel.PanelCount, copies: false).ShowDialog<int?>(Owner);
        if (position != null)
        {
            Panel.Move(card.Index, position.Value - 1);
        }
    }

    /// <summary>Original: <c>PanelEntrada_ContextMenuCambiarCopias_Click</c>.</summary>
    private async void ChangeCopies(CardPanelViewModel card)
    {
        if (Panel == null || Owner == null)
        {
            return;
        }

        int? copies = await new MoveToWindow(card.Copies, copies: true).ShowDialog<int?>(Owner);
        if (copies != null)
        {
            Panel.SetCopies(card.Index, copies.Value);
        }
    }

    /// <summary>Raised with the message when adding images found a problem the user should know of.</summary>
    public event Action<string>? ProblemFound;

    private void Report((int Added, AppError Error) result)
    {
        if (result.Error.HasError || result.Error.HasWarning)
        {
            string title = result.Error.HasWarning ? StringResources.Get("Imagen_no_encontrada") + Environment.NewLine : string.Empty;
            ProblemFound?.Invoke(title + result.Error.Message);
        }
    }

    // ---- Drag and drop ----

    /// <summary>The data a dragged card carries: its position in the panel.</summary>
    public static readonly DataFormat<string> CardFormat = DataFormat.CreateStringApplicationFormat("pnptool.card");

    /// <summary>How far the pointer must move with the button down before a card is dragged.</summary>
    private const double DragThreshold = 6;

    private (PointerPressedEventArgs Args, Point Point, CardPanelViewModel Card)? _press;

    /// <summary>
    /// Starts dragging the card that was pressed once the pointer has moved a little. The
    /// original started on the press itself and showed a copy of the card under the pointer.
    /// </summary>
    private async void MaybeStartDrag(PointerEventArgs e)
    {
        if (_press is not { } press || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Point now = e.GetPosition(this);
        if (Math.Abs(now.X - press.Point.X) < DragThreshold && Math.Abs(now.Y - press.Point.Y) < DragThreshold)
        {
            return;
        }

        _press = null;
        HideZoom();
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(CardFormat, press.Card.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        await DragDrop.DoDragDropAsync(press.Args, data, DragDropEffects.Move);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(CardFormat)
            ? DragDropEffects.Move
            : e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// A card dropped on another takes its place, and one dropped outside the cards goes to
    /// the end. Files dropped from the file manager are added, before the card they fall on
    /// or at the end. Original: <c>PanelImagenes_Drop</c>, <c>DropGenerico</c> and
    /// <c>PanelImagenes_DropDirecto</c>.
    /// </summary>
    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (Panel is not { } panel)
        {
            return;
        }

        e.Handled = true;
        int target = Locate(e.Source).Card?.Card?.Index ?? -1;
        if (e.DataTransfer.TryGetValue(CardFormat) is { } dragged && int.TryParse(dragged, out int from))
        {
            panel.Move(from, target < 0 ? panel.PanelCount - 1 : target);
            return;
        }

        string[] files = [.. (e.DataTransfer.TryGetFiles() ?? []).Select(file => file.TryGetLocalPath()).OfType<string>()];
        if (files.Length != 0)
        {
            Report(await panel.DropFilesAsync(files, target));
        }
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(CommandModifier) && e.Delta.Y != 0)
        {
            ZoomStepRequested?.Invoke(e.Delta.Y > 0 ? 1 : -1);
            e.Handled = true;
        }
    }
}
