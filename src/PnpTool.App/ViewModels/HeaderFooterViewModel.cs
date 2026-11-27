using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Controls;
using PnpTool.Core;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.App.ViewModels;

/// <summary>What the header window gives back.</summary>
public sealed record HeaderFooterResult(HeaderFooter Header, Watermark Watermark);

/// <summary>
/// What is printed around the cards: the colour bar, the registration marks, the signature,
/// the page number, and the watermark. Original: <c>WindowSetupEncabezado</c> with its
/// controls <c>Control_ImagenCalibracion</c>, <c>Control_MarcaCalibracion</c>,
/// <c>Control_NumeroDeHoja</c> and <c>Control_MarcaDeAgua</c>.
/// </summary>
/// <remarks>
/// The sizes of the colour bar and of the registration marks are in points; where the marks
/// go is in the unit the program is set to. Numbers take a point as decimal separator.
/// </remarks>
public sealed partial class HeaderFooterViewModel : ViewModelBase
{
    /// <param name="header">The header settings of the project; they are not changed.</param>
    /// <param name="watermark">The watermark of the project; it is not changed.</param>
    public HeaderFooterViewModel(HeaderFooter header, Watermark watermark)
    {
        _showColorBar = header.ShowCalibrationImage;
        _colorBarWidthText = Points(header.CalibrationImageSize.X);
        _colorBarHeightText = Points(header.CalibrationImageSize.Y);

        _showMarks = header.ShowCalibrationMark;
        _markSizeText = Points(header.CalibrationMarkSize);
        _markXText = header.CalibrationMarkPosition.X.ToDisplayString();
        _markYText = header.CalibrationMarkPosition.Y.ToDisplayString();

        _signatureText = header.SignatureText;

        _showPageNumber = header.ShowPageNumber;
        _firstPageNumber = header.ShowPageNumber && header.FirstPageNumber == 0 ? 1 : header.FirstPageNumber;
        PositionOptions = PageNumberPositionOption.All();
        _selectedPosition = PositionOptions.First(option => option.Value == header.PageNumberPosition);

        WatermarkOptions = WatermarkTypeOption.All();
        _selectedWatermark = WatermarkOptions.First(option => option.Value == watermark.Type);
        _watermarkPath = watermark.Path;
    }

    // ---- Colour bar. Original: Control_ImagenCalibracion ----

    [ObservableProperty]
    private bool _showColorBar;

    /// <summary>In points.</summary>
    [ObservableProperty]
    private string _colorBarWidthText;

    /// <summary>In points.</summary>
    [ObservableProperty]
    private string _colorBarHeightText;

    // ---- Registration marks. Original: Control_MarcaCalibracion ----

    [ObservableProperty]
    private bool _showMarks;

    /// <summary>In points.</summary>
    [ObservableProperty]
    private string _markSizeText;

    [ObservableProperty]
    private string _markXText;

    [ObservableProperty]
    private string _markYText;

    // ---- Signature ----

    [ObservableProperty]
    private string _signatureText;

    // ---- Page number. Original: Control_NumeroDeHoja ----

    [ObservableProperty]
    private bool _showPageNumber;

    [ObservableProperty]
    private int _firstPageNumber;

    public IReadOnlyList<PageNumberPositionOption> PositionOptions { get; }

    [ObservableProperty]
    private PageNumberPositionOption _selectedPosition;

    /// <summary>Numbering turned on without a first number starts at 1. Original: <c>CheckMostrarNumeroHoja_Checked</c>.</summary>
    partial void OnShowPageNumberChanged(bool value)
    {
        if (value && FirstPageNumber == 0)
        {
            FirstPageNumber = 1;
        }
    }

    // ---- Watermark. Original: Control_MarcaDeAgua ----

    public IReadOnlyList<WatermarkTypeOption> WatermarkOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WatermarkPathOffered))]
    private WatermarkTypeOption _selectedWatermark;

    /// <summary>The image used as watermark.</summary>
    [ObservableProperty]
    private string _watermarkPath;

    public bool WatermarkPathOffered => SelectedWatermark.Value != WatermarkType.None;

    // ---- Accepting ----

    /// <summary>The settings as they stand in the window. Original: <c>Boton_Ok_Click</c>.</summary>
    public HeaderFooterResult Result() => new(
        new HeaderFooter
        {
            ShowCalibrationImage = ShowColorBar,
            CalibrationImageSize = new UnitPoint { X = FromPoints(ColorBarWidthText), Y = FromPoints(ColorBarHeightText) },
            ShowCalibrationMark = ShowMarks,
            CalibrationMarkSize = FromPoints(MarkSizeText),
            CalibrationMarkPosition = new UnitPoint { X = Parse(MarkXText), Y = Parse(MarkYText) },
            SignatureText = SignatureText ?? string.Empty,
            ShowPageNumber = ShowPageNumber,
            FirstPageNumber = FirstPageNumber,
            PageNumberPosition = SelectedPosition.Value,
        },
        new Watermark { Type = SelectedWatermark.Value, Path = WatermarkPath ?? string.Empty });

    private static string Points(Unit value) => value.ToDisplayString(UnitType.Point, withSuffix: false, newLine: false);

    private static Unit FromPoints(string? text) => string.IsNullOrEmpty(text) ? Unit.Zero : Unit.FromDisplayString(text, UnitType.Point);

    private static Unit Parse(string? text) => string.IsNullOrEmpty(text) || text == "-" ? Unit.Zero : Unit.FromDisplayString(text);
}
