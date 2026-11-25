using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PnpTool.App.ViewModels.Panel;
using PnpTool.Core.Cards;

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
        if (e.Source is Visual source && source.FindAncestorOfType<PnpTool.Controls.NumericBox>(includeSelf: true) != null)
        {
            return;
        }

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

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(CommandModifier) && e.Delta.Y != 0)
        {
            ZoomStepRequested?.Invoke(e.Delta.Y > 0 ? 1 : -1);
            e.Handled = true;
        }
    }
}
