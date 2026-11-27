using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Controls;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.App.ViewModels;

/// <summary>What the face window gives back: the settings of the face and, for backs, where they are moved and turned.</summary>
public sealed record FaceSetupResult(FaceSettings Face, UnitPoint BackOffset, double BackRotation);

/// <summary>
/// The settings of the fronts or of the backs: size of the image on the card, effect,
/// background, outer frame and cut lines, and for backs how far they are moved and turned.
/// Original: <c>WindowConfigurarCara</c> with its controls <c>Control_TamanoCarta</c>,
/// <c>Control_ConfiguracionCaraTrasera</c>, <c>Control_Efecto</c>, <c>Control_ColorFondo</c>,
/// <c>ControlMarcoExterior</c> and <c>Control_LineasDeCorte</c>.
/// </summary>
/// <remarks>
/// Lengths are typed and shown in the unit the program is set to, with a point as decimal
/// separator; the thickness of the cut lines is in points.
/// </remarks>
public sealed partial class FaceSetupViewModel : ViewModelBase
{
    private readonly FaceSettings _face = new();
    private readonly SettingsGroup _group;
    private readonly UnitPoint _cardSize;
    private readonly UnitPoint _spacing;
    private readonly Unit _bleed;
    private readonly bool _sizeLocked;
    private readonly bool _wasOpen;
    private double _ratio = 1.0;
    private bool _loading = true;
    private bool _padlockUsed;
    private string _frameColorBefore;

    /// <param name="settings">The settings of the project; they are not changed.</param>
    /// <param name="side">Fronts or backs.</param>
    public FaceSetupViewModel(ProjectSettings settings, FaceType side)
    {
        IsBack = side == FaceType.Back;
        _group = settings.Group.Group;
        _face.CopyFrom(IsBack ? settings.Back : settings.Front);
        _cardSize = new UnitPoint(settings.Grid.CardSize);
        _spacing = new UnitPoint(settings.Grid.Spacing);
        _bleed = settings.Grid.Bleed;
        _sizeLocked = settings.LockCardSize;
        _wasOpen = _face.Locked;

        bool templates = _group == SettingsGroup.AdvancedStyles;
        bool fewer = _group is SettingsGroup.Tabletop or SettingsGroup.Soriano;
        SizeOffered = !templates;
        SizeEnabled = !_sizeLocked;
        BackPlaceOffered = IsBack && !fewer;
        EffectOffered = !(fewer && settings.Group.ProjectType == ProjectType.Papermini);
        BackgroundOffered = !templates && !fewer;
        FrameOffered = !fewer;
        CutLinesOffered = !fewer && !(IsBack && LayoutCalculator.IsGutterfold(settings.PrintLayout));

        // ---- Size ----
        _widthText = _face.Size.X.ToDisplayString();
        _heightText = _face.Size.Y.ToDisplayString();

        // ---- Effect. Original: Control_Efecto.CargaComboBox ----
        EffectOptions = _group == SettingsGroup.Soriano
            ? [.. ImageEffectOption.All(withMirror: true).Where(option => option.Value == ImageEffectType.Mirror)]
            : ImageEffectOption.All(withMirror: _group != SettingsGroup.Tickets);
        _selectedEffect = EffectOptions.FirstOrDefault(option => option.Value == _face.Effect) ?? EffectOptions[0];
        _effectSizeText = _face.RoundedCornerSize.ToString();

        // ---- Background ----
        BackgroundOptions =
        [
            .. Enum.GetValues<CardBackgroundType>().Select(type => new CardBackgroundOption(StringResources.ForEnum("ComboBox_EfectosMarcoExterior_", type), type)),
        ];
        _paintBackground = _face.PaintBackground;
        _selectedBackground = BackgroundOptions.First(option => option.Value == _face.BackgroundEffect);
        BackgroundColor = _face.BackgroundFillColor;

        // ---- Outer frame. Advanced templates have no mirror frame ----
        FrameOptions =
        [
            .. Enum.GetValues<OuterFrameEffect>()
                .Where(effect => !templates || effect == OuterFrameEffect.SolidColor)
                .Select(effect => new OuterFrameEffectOption(StringResources.ForEnum("ComboBox_EfectosMarcoExterior_", effect), effect)),
        ];
        if (templates)
        {
            _face.OuterFrameEffect = OuterFrameEffect.SolidColor;
        }

        _showFrame = _face.ShowOuterFrame;
        _selectedFrame = FrameOptions.First(option => option.Value == _face.OuterFrameEffect);
        _frameSizeText = _face.OuterFrameSize.ToDisplayString();
        FrameColor = _face.OuterFrameColor;
        _frameColorBefore = _face.OuterFrameColor;

        // ---- Cut lines. Templates draw crosses or nothing, and always with some thickness ----
        if (templates && _face.CutLine.Thickness.Point <= 0.0)
        {
            _face.CutLine.Thickness = Unit.FromPoint(0.4);
        }

        CutLineOptions = templates ? CutLineTypeOption.Simple() : CutLineTypeOption.All();
        _selectedCutLine = CutLineOptions.FirstOrDefault(option => option.Value == _face.CutLine.LineType) ?? CutLineOptions[0];
        _cutLinesToEdge = _face.CutLine.CrossLinesToEdge;
        CutLineColor = _face.CutLine.Color;
        _cutLineThicknessText = _face.CutLine.Thickness.ToDisplayString(UnitType.Point, withSuffix: false, newLine: false);
        _cutLineGapText = _face.CutLine.Gap.ToString();
        _toEdgeAllowed = !templates;

        // ---- Backs ----
        _offsetXText = settings.BackOffset.X.ToDisplayString();
        _offsetYText = settings.BackOffset.Y.ToDisplayString();
        _rotationText = DecimalTextBox.Format(settings.BackRotation);

        _loading = false;
        EffectChosen();
        CutLineChosen();
    }

    /// <summary>Whether these are the settings of the backs.</summary>
    public bool IsBack { get; }

    public string Title => StringResources.Get(IsBack ? "Boton_Configurar_Cara_Trasera" : "Boton_Configurar_Cara_Frontal");

    /// <summary>Tells the window of something the user has to read.</summary>
    public event Action<string>? Notice;

    // ---- Size. Original: Control_TamanoCarta ----

    /// <summary>Advanced templates give each image its size.</summary>
    public bool SizeOffered { get; }

    /// <summary>With the size of the faces tied to the card's, it cannot be typed.</summary>
    public bool SizeEnabled { get; }

    [ObservableProperty]
    private string _widthText;

    [ObservableProperty]
    private string _heightText;

    /// <summary>Whether typing one side changes the other in proportion. Original: <c>CandadoAbierto</c>, the other way round.</summary>
    [ObservableProperty]
    private bool _ratioLocked;

    partial void OnRatioLockedChanged(bool value)
    {
        _padlockUsed = true;
        if (value)
        {
            _ratio = (double)_face.Size.Y == 0.0 ? 1.0 : (double)_face.Size.X / (double)_face.Size.Y;
        }
    }

    partial void OnWidthTextChanged(string value)
    {
        if (_loading)
        {
            return;
        }

        _face.Size.X = Parse(value);
        if (RatioLocked)
        {
            _face.Size.Y = Unit.FromPoint((double)_face.Size.X / _ratio);
            _loading = true;
            HeightText = _face.Size.Y.ToDisplayString();
            _loading = false;
        }
    }

    partial void OnHeightTextChanged(string value)
    {
        if (_loading)
        {
            return;
        }

        _face.Size.Y = Parse(value);
        if (RatioLocked)
        {
            _face.Size.X = Unit.FromPoint((double)_face.Size.Y * _ratio);
            _loading = true;
            WidthText = _face.Size.X.ToDisplayString();
            _loading = false;
        }
    }

    /// <summary>Exchanges width and height. Original: <c>Boton_Swap_Click</c>.</summary>
    public void SwapSize() => SetSize(new UnitPoint { X = _face.Size.Y, Y = _face.Size.X });

    private void SetSize(UnitPoint size)
    {
        _face.Size = new UnitPoint(size);
        if (RatioLocked)
        {
            _ratio = (double)size.Y == 0.0 ? 1.0 : (double)size.X / (double)size.Y;
        }

        _loading = true;
        WidthText = size.X.ToDisplayString();
        HeightText = size.Y.ToDisplayString();
        _loading = false;
    }

    // ---- Effect. Original: Control_Efecto ----

    public bool EffectOffered { get; }

    public IReadOnlyList<ImageEffectOption> EffectOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectHasSize))]
    private ImageEffectOption _selectedEffect;

    /// <summary>The radius of rounded corners, or the size of a token.</summary>
    [ObservableProperty]
    private string _effectSizeText;

    /// <summary>Original: <c>ComboboxItemEfectosImagenes.EfectoTieneTamano</c>.</summary>
    public bool EffectHasSize => SelectedEffect.Value
        is ImageEffectType.RoundedCorners or ImageEffectType.HexagonToken or ImageEffectType.CircularToken or ImageEffectType.PentagonToken;

    partial void OnSelectedEffectChanged(ImageEffectOption value)
    {
        if (!_loading && value != null)
        {
            EffectChosen();
        }
    }

    /// <summary>
    /// With the size of the faces tied to the card's, a mirrored image leaves room for the
    /// bleed it fills and any other takes the whole card. Original: <c>HayCambioEfecto</c>.
    /// </summary>
    private void EffectChosen()
    {
        if (!_sizeLocked)
        {
            return;
        }

        double bleed = SelectedEffect.Value == ImageEffectType.Mirror ? 2.0 * (double)_bleed : 0.0;
        SetSize(new UnitPoint { X = (double)_cardSize.X - bleed, Y = (double)_cardSize.Y - bleed });
    }

    // ---- Background. Original: Control_ColorFondo ----

    public bool BackgroundOffered { get; }

    public IReadOnlyList<CardBackgroundOption> BackgroundOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundColorOffered))]
    private bool _paintBackground;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundColorOffered))]
    private CardBackgroundOption _selectedBackground;

    /// <summary>The colour behind every card, as "A_R_G_B"; the window sets it from its colour button.</summary>
    public string BackgroundColor { get; set; }

    /// <summary>One colour for all is chosen here; a colour for each card, on the cards.</summary>
    public bool BackgroundColorOffered => PaintBackground && SelectedBackground.Value == CardBackgroundType.SolidColor;

    // ---- Outer frame. Original: ControlMarcoExterior ----

    public bool FrameOffered { get; }

    public IReadOnlyList<OuterFrameEffectOption> FrameOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FrameColorOffered))]
    private bool _showFrame;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FrameColorOffered))]
    private OuterFrameEffectOption _selectedFrame;

    [ObservableProperty]
    private string _frameSizeText;

    /// <summary>The colour of the frame, as "A_R_G_B", or nothing for a mirror frame; the window sets it from its colour button.</summary>
    public string FrameColor { get; set; }

    public bool FrameColorOffered => ShowFrame && SelectedFrame.Value == OuterFrameEffect.SolidColor;

    /// <summary>A frame that is turned on with no size gets 3 mm. Original: <c>CheckBox_PintarMarco_Checked</c>.</summary>
    partial void OnShowFrameChanged(bool value)
    {
        if (value && Parse(FrameSizeText).Millimeter <= 0.0)
        {
            FrameSizeText = Unit.FromMillimeter(3.0).ToDisplayString();
        }
    }

    /// <summary>
    /// A mirror frame has no colour, and cannot be had with space between the cards: asking
    /// for it there says so and turns the frame off.
    /// Original: <c>ComboBox_EfectoMarcoExterior_SelectionChanged</c> and <c>SeleccionEfectoCambia</c>.
    /// </summary>
    partial void OnSelectedFrameChanged(OuterFrameEffectOption? oldValue, OuterFrameEffectOption newValue)
    {
        if (_loading || newValue == null)
        {
            return;
        }

        if (newValue.Value == OuterFrameEffect.Mirror)
        {
            if (_spacing.X.Millimeter > 0.0 || _spacing.Y.Millimeter > 0.0)
            {
                Notice?.Invoke(StringResources.Get("Error_Efecto_Marco_Con_Separacion"));
                ShowFrame = false;
                _loading = true;
                SelectedFrame = FrameOptions[0];
                _loading = false;
                return;
            }

            _frameColorBefore = FrameColor;
            FrameColor = string.Empty;
        }
        else if (oldValue?.Value == OuterFrameEffect.Mirror)
        {
            FrameColor = _frameColorBefore;
        }
    }

    // ---- Cut lines. Original: Control_LineasDeCorte ----

    public bool CutLinesOffered { get; }

    public IReadOnlyList<CutLineTypeOption> CutLineOptions { get; }

    private readonly bool _toEdgeAllowed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CutLineDrawn))]
    [NotifyPropertyChangedFor(nameof(CutLineGapOffered))]
    [NotifyPropertyChangedFor(nameof(CutLinesToEdgeOffered))]
    [NotifyPropertyChangedFor(nameof(CutLineGapLabel))]
    private CutLineTypeOption _selectedCutLine;

    [ObservableProperty]
    private bool _cutLinesToEdge;

    /// <summary>In points.</summary>
    [ObservableProperty]
    private string _cutLineThicknessText;

    /// <summary>The gap between line and card, or the size of the crosses.</summary>
    [ObservableProperty]
    private string _cutLineGapText;

    /// <summary>The colour of the lines, as "A_R_G_B"; the window sets it from its colour button.</summary>
    public string CutLineColor { get; set; }

    /// <summary>Colour and thickness, for every kind but none.</summary>
    public bool CutLineDrawn => SelectedCutLine.Value != CutLineType.None;

    public bool CutLineGapOffered => SelectedCutLine.Value is CutLineType.Normal or CutLineType.Mixed or CutLineType.Cross;

    public bool CutLinesToEdgeOffered => SelectedCutLine.Value == CutLineType.Cross && _toEdgeAllowed;

    /// <summary>Original: the two texts of <c>Label_Separar</c>.</summary>
    public string CutLineGapLabel => SelectedCutLine.Value == CutLineType.Cross
        ? StringResources.Get("Tamano") + " (" + LayoutViewModel.UnitName + ")"
        : StringResources.Get("Form_Setup_Caras_CaraFrontal_LabelLineaCorteSeparar");

    partial void OnSelectedCutLineChanged(CutLineTypeOption value)
    {
        if (!_loading && value != null)
        {
            CutLineChosen();
        }
    }

    /// <summary>Crosses without size get 2. Original: <c>CambiaVisibilidad_Cruz</c>.</summary>
    private void CutLineChosen()
    {
        if (SelectedCutLine.Value == CutLineType.Cross && IntegerTextBox.Parse(CutLineGapText) <= 0)
        {
            CutLineGapText = "2";
        }
    }

    // ---- Backs. Original: Control_ConfiguracionCaraTrasera ----

    /// <summary>Whether the backs can be moved and turned here.</summary>
    public bool BackPlaceOffered { get; }

    [ObservableProperty]
    private string _offsetXText;

    [ObservableProperty]
    private string _offsetYText;

    /// <summary>In degrees.</summary>
    [ObservableProperty]
    private string _rotationText;

    // ---- Accepting ----

    /// <summary>
    /// What stops the settings from being accepted, or null: an image larger than the card.
    /// Original: the checks of <c>Boton_Ok_Click</c>.
    /// </summary>
    public string? Problem()
    {
        var problems = new List<string>();
        if (_face.Size.X.IsGreaterInMillimeters(_cardSize.X))
        {
            problems.Add(StringResources.Get(IsBack ? "Carta_Escala_Ancho_Trasera_Error" : "Carta_Escala_Ancho_Frontal_Error"));
        }

        if (_face.Size.Y.IsGreaterInMillimeters(_cardSize.Y))
        {
            problems.Add(StringResources.Get(IsBack ? "Carta_Escala_Alto_Trasera_Error" : "Carta_Escala_Alto_Frontal_Error"));
        }

        return problems.Count == 0 ? null : string.Join(Environment.NewLine, problems);
    }

    /// <summary>The settings as they stand in the window. Original: the end of <c>Boton_Ok_Click</c>.</summary>
    public FaceSetupResult Result()
    {
        var face = new FaceSettings();
        face.CopyFrom(_face);

        // The original always wrote "padlock open" here, whatever the file said, and never
        // read it back. It is kept as it was unless the padlock was used.
        face.Locked = _padlockUsed ? !RatioLocked : _wasOpen;
        face.Effect = SelectedEffect.Value;
        face.RoundedCornerSize = IntegerTextBox.Parse(EffectSizeText);
        face.PaintBackground = PaintBackground;
        face.BackgroundEffect = SelectedBackground.Value;
        face.BackgroundFillColor = BackgroundColor;
        face.ShowOuterFrame = ShowFrame;
        face.OuterFrameEffect = SelectedFrame.Value;
        face.OuterFrameColor = FrameColor;
        face.OuterFrameSize = Parse(FrameSizeText);
        face.CutLine.LineType = SelectedCutLine.Value;
        face.CutLine.CrossLinesToEdge = CutLinesToEdge;
        face.CutLine.Color = CutLineColor;
        face.CutLine.Thickness = string.IsNullOrEmpty(CutLineThicknessText) ? Unit.Zero : Unit.FromDisplayString(CutLineThicknessText, UnitType.Point);
        face.CutLine.Gap = IntegerTextBox.Parse(CutLineGapText);
        string rotation = RotationText is null or "" or "-" ? "0" : RotationText;
        return new FaceSetupResult(
            face,
            new UnitPoint { X = Parse(OffsetXText), Y = Parse(OffsetYText) },
            DecimalTextBox.Parse(rotation));
    }

    private static Unit Parse(string? text) => string.IsNullOrEmpty(text) || text == "-" ? Unit.Zero : Unit.FromDisplayString(text);
}
