using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Paper;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using Grid = PnpTool.Core.Projects.Grid;

namespace PnpTool.App.ViewModels;

/// <summary>What the layout window gives back when accepted.</summary>
public sealed record LayoutResult(PrintLayout Layout, bool DoublePanel, Grid Grid, bool LockCardSize);

/// <summary>
/// The layout of the sheets: paper, sides and backs, size of the cards, space between them,
/// bleed, and rows and columns. Original: <c>WindowSetupImpresion</c> with its controls
/// <c>Control_TamanosPapel</c>, <c>Control_DobleCara</c>, <c>Control_TamanoCarta</c>,
/// <c>Control_Tabla</c> and <c>Control_FolioPreview</c>.
/// </summary>
/// <remarks>Lengths are typed and shown in the unit the program is set to, with a point as decimal separator.</remarks>
public sealed partial class LayoutViewModel : ViewModelBase
{
    private readonly PrintLayout _layout = new();
    private readonly Grid _grid = new();
    private readonly SettingsGroup _group;
    private readonly ProjectType _projectType;
    private double _ratio = 1.0;
    private bool _loading = true;

    /// <param name="settings">The settings of the project; they are not changed.</param>
    /// <param name="statistics">The sizes of the images of the project, offered as card sizes.</param>
    public LayoutViewModel(ProjectSettings settings, DeckStatistics? statistics = null)
    {
        _group = settings.Group.Group;
        _projectType = settings.Group.ProjectType;
        _layout.CopyFrom(settings.PrintLayout);
        _grid.CopyFrom(settings.Grid);
        Statistics = statistics ?? new DeckStatistics();
        _lockCardSize = settings.LockCardSize;

        PaperOptions = PaperTypeOption.ForSheets(covers: false);
        _selectedPaper = PaperOptions.FirstOrDefault(option => option.Value == _layout.Paper.Type) ?? PaperOptions[0];
        _paperWidthText = _layout.Paper.Size.X.ToDisplayString();
        _paperHeightText = _layout.Paper.Size.Y.ToDisplayString();
        _landscape = _layout.Paper.Landscape;

        // Advanced templates have no gutterfold. Original: RetiraGutter.
        string oneSide = StringResources.Get("Form_Setup_RadioButton_ImpresionUnaCara");
        string bothSides = StringResources.Get("Form_Setup_RadioButton_ImpresionDobleCara");
        SideOptions = _group == SettingsGroup.AdvancedStyles ? [oneSide, bothSides] : [oneSide, bothSides, "Gutterfold"];
        string fold = StringResources.Get("Window_SetupImpresion_Gutterfold_DoblarPor") + " ";
        FoldOptions = [fold + StringResources.Get("Frontal"), fold + StringResources.Get("Trasera")];
        bool gutterfold = LayoutCalculator.IsGutterfold(_layout);
        _selectedSide = _layout.DoubleSided ? 1 : gutterfold && SideOptions.Count > 2 ? 2 : 0;
        _selectedFold = _layout.FoldBottom ? 0 : 1;
        _backOptions = BackImageOption.For(_layout.DoubleSided, gutterfold, _projectType, _group);
        _selectedBack = BackOptions.FirstOrDefault(option => option.Value == _layout.BackImage) ?? BackOptions[0];
        DoublePanel = settings.DoublePanel;

        _cardWidthText = _grid.CardSize.X.ToDisplayString();
        _cardHeightText = _grid.CardSize.Y.ToDisplayString();
        _spacingXText = _grid.Spacing.X.ToDisplayString();
        _spacingYText = _grid.Spacing.Y.ToDisplayString();
        _bleedText = _grid.Bleed.ToDisplayString();
        _autoCalculate = _layout.CalculateAutomatically;
        _columnsText = _layout.Columns.ToString();
        _rowsText = _layout.Rows.ToString();
        _loading = false;
        Recalculate();
    }

    public DeckStatistics Statistics { get; }

    public string StatisticsText => Statistics.ToString();

    /// <summary>The unit lengths are typed in, for tooltips. Original: <c>TerminoSistemaMetrico</c>.</summary>
    public static string UnitName => MeasurementSettings.Current == UnitType.Inch ? "inch" : "mm";

    // ---- Paper. Original: Control_TamanosPapel ----

    public IReadOnlyList<PaperTypeOption> PaperOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomPaper))]
    private PaperTypeOption _selectedPaper;

    [ObservableProperty]
    private string _paperWidthText;

    [ObservableProperty]
    private string _paperHeightText;

    /// <summary>Whether the sheet is used on its side.</summary>
    [ObservableProperty]
    private bool _landscape;

    /// <summary>Only a custom size can be typed.</summary>
    public bool IsCustomPaper => SelectedPaper.Value == PaperType.Custom;

    /// <summary>Whether paper and table can be changed: not for the "tabletop" group.</summary>
    public bool PaperAndTableOffered => _group != SettingsGroup.Tabletop;

    partial void OnSelectedPaperChanged(PaperTypeOption value)
    {
        _layout.Paper.Type = value.Value;
        if (value.Value != PaperType.Custom)
        {
            UnitPoint size = Paper.GetSize(value.Value);
            _loading = true;
            PaperWidthText = size.X.ToDisplayString();
            PaperHeightText = size.Y.ToDisplayString();
            _loading = false;
            _layout.Paper.Size = Unit2(PaperWidthText, PaperHeightText);
        }

        Recalculate();
    }

    partial void OnPaperWidthTextChanged(string value) => PaperTyped();

    partial void OnPaperHeightTextChanged(string value) => PaperTyped();

    private void PaperTyped()
    {
        if (!_loading)
        {
            _layout.Paper.Size = Unit2(PaperWidthText, PaperHeightText);
            Recalculate();
        }
    }

    partial void OnLandscapeChanged(bool value)
    {
        _layout.Paper.Landscape = value;
        Recalculate();
    }

    // ---- Sides and backs. Original: Control_DobleCara ----

    /// <summary>One side, both sides, or gutterfold.</summary>
    public IReadOnlyList<string> SideOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGutterfold))]
    private int _selectedSide;

    [ObservableProperty]
    private IReadOnlyList<BackImageOption> _backOptions;

    [ObservableProperty]
    private BackImageOption? _selectedBack;

    /// <summary>Fold by the fronts or by the backs.</summary>
    public IReadOnlyList<string> FoldOptions { get; }

    [ObservableProperty]
    private int _selectedFold;

    public bool IsGutterfold => SelectedSide == 2;

    /// <summary>Dice and books have no choice of backs.</summary>
    public bool BacksOffered => _group is not (SettingsGroup.Dice or SettingsGroup.Dice7Pixel or SettingsGroup.Books);

    /// <summary>Whether cards show two faces: when every card has its own back. Original: <c>PanelDoble</c>.</summary>
    public bool DoublePanel { get; private set; }

    partial void OnSelectedSideChanged(int value)
    {
        if (_loading)
        {
            return;
        }

        BackImageMode before = SelectedBack?.Value ?? BackImageMode.NoBack;
        _layout.DoubleSided = value == 1;
        _loading = true;
        BackOptions = BackImageOption.For(_layout.DoubleSided, value == 2, _projectType, _group);
        if (value == 2)
        {
            SelectedFold = 0;
        }

        // With backs, a back per card stays a back per card and anything else becomes the same back for all.
        SelectedBack = value > 0 && before == BackImageMode.DifferentBack && BackOptions.Count > 1 ? BackOptions[1] : BackOptions[0];
        _loading = false;
        BackChosen();
    }

    partial void OnSelectedBackChanged(BackImageOption? value)
    {
        if (!_loading && value != null)
        {
            BackChosen();
        }
    }

    private void BackChosen()
    {
        _layout.BackImage = SelectedBack?.Value ?? BackImageMode.NoBack;
        DoublePanel = _layout.BackImage == BackImageMode.DifferentBack;
        Recalculate();
    }

    // ---- Card size. Original: Control_TamanoCarta ----

    [ObservableProperty]
    private string _cardWidthText;

    [ObservableProperty]
    private string _cardHeightText;

    /// <summary>Whether width and height keep their proportion. Original: the closed padlock of the control.</summary>
    [ObservableProperty]
    private bool _ratioLocked;

    /// <summary>
    /// Whether the faces take the size of the card when the window is accepted.
    /// Original: <c>Bloquear_CambioTamanoCaras</c>.
    /// </summary>
    [ObservableProperty]
    private bool _lockCardSize;

    partial void OnRatioLockedChanged(bool value)
    {
        if (value)
        {
            _ratio = (double)_grid.CardSize.Y == 0.0 ? 1.0 : (double)_grid.CardSize.X / (double)_grid.CardSize.Y;
        }
    }

    partial void OnCardWidthTextChanged(string value)
    {
        if (_loading)
        {
            return;
        }

        _grid.CardSize.X = Parse(value);
        if (RatioLocked)
        {
            _grid.CardSize.Y = Unit.FromPoint((double)_grid.CardSize.X / _ratio);
            _loading = true;
            CardHeightText = _grid.CardSize.Y.ToDisplayString();
            _loading = false;
        }

        Recalculate();
    }

    partial void OnCardHeightTextChanged(string value)
    {
        if (_loading)
        {
            return;
        }

        _grid.CardSize.Y = Parse(value);
        if (RatioLocked)
        {
            _grid.CardSize.X = Unit.FromPoint((double)_grid.CardSize.Y * _ratio);
            _loading = true;
            CardWidthText = _grid.CardSize.X.ToDisplayString();
            _loading = false;
        }

        Recalculate();
    }

    /// <summary>Exchanges width and height. Original: <c>Boton_Swap_Click</c>.</summary>
    public void SwapCardSize() => SetCardSize(new UnitPoint { X = _grid.CardSize.Y, Y = _grid.CardSize.X });

    /// <summary>The "tabletop" group keeps its faces at the size of the card. Original: <c>Boton_BloquearTamano_Click</c>.</summary>
    public void ToggleLockCardSize()
    {
        if (_group != SettingsGroup.Tabletop)
        {
            LockCardSize = !LockCardSize;
        }
    }

    /// <summary>
    /// Gives the cards one of the sizes measured in the deck; a side of 0 becomes 1.
    /// Original: <c>ActualizaDatosEstadisticos</c> of the window.
    /// </summary>
    public void UseSize(UnitPoint size) => SetCardSize(new UnitPoint
    {
        X = (double)size.X == 0.0 ? Unit.FromDisplayString("1") : size.X,
        Y = (double)size.Y == 0.0 ? Unit.FromDisplayString("1") : size.Y,
    });

    private void SetCardSize(UnitPoint size)
    {
        _grid.CardSize = new UnitPoint(size);
        if (RatioLocked)
        {
            _ratio = (double)size.Y == 0.0 ? 1.0 : (double)size.X / (double)size.Y;
        }

        _loading = true;
        CardWidthText = size.X.ToDisplayString();
        CardHeightText = size.Y.ToDisplayString();
        _loading = false;
        Recalculate();
    }

    // ---- Spacing and bleed ----

    [ObservableProperty]
    private string _spacingXText;

    [ObservableProperty]
    private string _spacingYText;

    [ObservableProperty]
    private string _bleedText;

    partial void OnSpacingXTextChanged(string value) => Typed(() => _grid.Spacing.X = Parse(value));

    partial void OnSpacingYTextChanged(string value) => Typed(() => _grid.Spacing.Y = Parse(value));

    partial void OnBleedTextChanged(string value) => Typed(() => _grid.Bleed = Parse(value));

    private void Typed(Action apply)
    {
        if (!_loading)
        {
            apply();
            Recalculate();
        }
    }

    // ---- Rows and columns. Original: Control_Tabla ----

    /// <summary>Whether rows and columns are worked out from paper and card. Original: <c>CalcularAutomaticamente</c>.</summary>
    [ObservableProperty]
    private bool _autoCalculate;

    [ObservableProperty]
    private string _columnsText;

    [ObservableProperty]
    private string _rowsText;

    /// <summary>The width the columns take, with unit.</summary>
    [ObservableProperty]
    private string _totalWidthText = string.Empty;

    [ObservableProperty]
    private string _totalHeightText = string.Empty;

    /// <summary>Whether the columns fit the width of the sheet (shown green, or red).</summary>
    [ObservableProperty]
    private bool _columnsFit = true;

    [ObservableProperty]
    private bool _rowsFit = true;

    public int Columns => _layout.Columns;

    public int Rows => _layout.Rows;

    /// <summary>Whether everything fits the sheet. Original: <c>ConfiguracionValida</c>.</summary>
    public bool IsValid => ColumnsFit && RowsFit;

    /// <summary>Raised to tell the user something, such as that a gutterfold needs an even number of rows.</summary>
    public event Action<string>? Notice;

    /// <summary>Raised when what the preview of the sheet draws has changed.</summary>
    public event EventHandler? PreviewChanged;

    partial void OnAutoCalculateChanged(bool value)
    {
        _layout.CalculateAutomatically = value;
        Recalculate();
    }

    partial void OnColumnsTextChanged(string value)
    {
        if (!_loading)
        {
            _layout.Columns = int.TryParse(value, out int columns) ? columns : 1;
            Measure();
        }
    }

    partial void OnRowsTextChanged(string value)
    {
        if (_loading)
        {
            return;
        }

        int rows = int.TryParse(value, out int typed) ? typed : 1;
        if (rows % 2 != 0 && LayoutCalculator.IsGutterfold(_layout))
        {
            Notice?.Invoke(StringResources.Get("Gutter_paginas_par"));
            rows--;
            _loading = true;
            RowsText = rows.ToString();
            _loading = false;
        }

        _layout.Rows = rows;
        Measure();
    }

    /// <summary>With automatic calculation, fits rows and columns again. Original: <c>CalculaAutomaticamente</c>.</summary>
    private void Recalculate()
    {
        if (_loading)
        {
            return;
        }

        if (AutoCalculate)
        {
            (int columns, int rows) = LayoutCalculator.Fit(_grid, _layout, _group);
            _layout.Columns = columns;
            _layout.Rows = rows;
            _loading = true;
            ColumnsText = columns.ToString();
            RowsText = rows.ToString();
            _loading = false;
        }

        Measure();
    }

    /// <summary>Original: <c>ActualizaPreviewEtiquetasConfiguracionTamanoMax</c>.</summary>
    private void Measure()
    {
        UnitPoint paper = LayoutCalculator.Oriented(_layout.Paper);
        UnitPoint total = LayoutCalculator.TotalSize(_grid, _layout, _group);
        ColumnsFit = !(total.X.Millimeter > paper.X.Millimeter) && _layout.Columns > 0;
        RowsFit = !(total.Y.Millimeter > paper.Y.Millimeter) && _layout.Rows > 0;
        TotalWidthText = total.X.ToDisplayString(withSuffix: true, newLine: false);
        TotalHeightText = total.Y.ToDisplayString(withSuffix: true, newLine: false);
        OnPropertyChanged(nameof(Columns));
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(IsValid));
        PreviewChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- Accepting ----

    /// <summary>
    /// What to ask before accepting a layout that does not fit the sheet, or null when it
    /// fits. Original: <c>BotonOkValidar</c>.
    /// </summary>
    public string? QuestionBeforeAccepting()
    {
        if (IsValid)
        {
            return null;
        }

        var error = new AppError
        {
            Kind = ErrorKind.Warning,
            Message = StringResources.Get("Cartas_no_entran_alto") + " OR " + StringResources.Get("Cartas_no_entran_ancho"),
        };
        if (_grid.Spacing.X.Millimeter > 0.0)
        {
            error.Message = StringResources.Get("Form_Setup_LabelSeparacionX") + " X>0";
        }

        if (_grid.Spacing.Y.Millimeter > 0.0)
        {
            error.Message = StringResources.Get("Form_Setup_LabelSeparacionY") + " Y>0";
        }

        error.AppendResource("Continuar");
        return error.Message;
    }

    /// <summary>The layout as the window shows it. Original: <c>Boton_Ok_Click</c>.</summary>
    public LayoutResult Result()
    {
        var layout = new PrintLayout();
        layout.CopyFrom(_layout);
        layout.FoldBottom = IsGutterfold ? SelectedFold == 0 : _layout.FoldBottom;
        var grid = new Grid();
        grid.CopyFrom(_grid);
        return new LayoutResult(layout, DoublePanel, grid, LockCardSize);
    }

    private static Unit Parse(string? text) => string.IsNullOrEmpty(text) ? Unit.Zero : Unit.FromDisplayString(text);

    private static UnitPoint Unit2(string? x, string? y) => new() { X = Parse(x), Y = Parse(y) };
}
