using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Paper;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Pdf.Tools;

namespace PnpTool.App.ViewModels;

/// <summary>
/// The tool that makes the cover of a book: back cover, spine and front cover on one sheet.
/// Original: <c>WindowPortadaLibro</c> with <c>FormPortadaLibroModelo</c>.
/// </summary>
public sealed partial class BookCoverViewModel : ViewModelBase
{
    public BookCoverViewModel()
    {
        Stocks = PaperStockOption.All();
        SpineSides =
        [
            (StringResources.Get("Form_Setup_libro_Combobox_Lomo_Izquierda"), BookSpineType.Left),
            (StringResources.Get("Form_Setup_libro_Combobox_Lomo_Derecha"), BookSpineType.Right),
        ];
        SpineSideTexts = [.. SpineSides.Select(side => side.Text)];
        Effects = [.. ImageEffectOption.All(withMirror: true).Where(option => option.Value is ImageEffectType.None or ImageEffectType.Mirror)];
        _selectedEffect = Effects.First(option => option.Value == ImageEffectType.Mirror);
        _selectedStock = Stocks[1];
        _pageThicknessText = Number(Stocks[1].PageThickness);
        UpdateSpine();
    }

    /// <summary>The sheet; the one of 500 by 330 for covers is offered first, as in the original.</summary>
    public PaperChoiceViewModel Paper { get; } = new(PaperType.SorianoCovers);

    public IReadOnlyList<PaperStockOption> Stocks { get; }

    [ObservableProperty]
    private PaperStockOption _selectedStock;

    /// <summary>The thickness of a page; it follows the paper chosen and can be typed.</summary>
    [ObservableProperty]
    private string _pageThicknessText;

    [ObservableProperty]
    private string _pageCountText = string.Empty;

    [ObservableProperty]
    private string _coverWidthText = string.Empty;

    [ObservableProperty]
    private string _coverHeightText = string.Empty;

    /// <summary>The width of the spine; it follows pages and paper and can be typed.</summary>
    [ObservableProperty]
    private string _spineWidthText = string.Empty;

    [ObservableProperty]
    private string _bleedText = "3";

    private IReadOnlyList<(string Text, BookSpineType Value)> SpineSides { get; }

    public IReadOnlyList<string> SpineSideTexts { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LeftLabel))]
    [NotifyPropertyChangedFor(nameof(RightLabel))]
    private int _selectedSpineSide;

    public IReadOnlyList<ImageEffectOption> Effects { get; }

    [ObservableProperty]
    private ImageEffectOption _selectedEffect;

    [ObservableProperty]
    private string _leftImage = string.Empty;

    [ObservableProperty]
    private string _spineImage = string.Empty;

    [ObservableProperty]
    private string _rightImage = string.Empty;

    [ObservableProperty]
    private double _progress;

    /// <summary>What the image on the left is: the back cover with the spine on the left, the front cover otherwise.</summary>
    public string LeftLabel => StringResources.Get(SpineSides[SelectedSpineSide].Value == BookSpineType.Right ? "Form_PortadaLibro_Portada" : "Form_PortadaLibro_ContraPortada");

    public string RightLabel => StringResources.Get(SpineSides[SelectedSpineSide].Value == BookSpineType.Right ? "Form_PortadaLibro_ContraPortada" : "Form_PortadaLibro_Portada");

    /// <summary>Raised to tell the user something.</summary>
    public event Action<string>? Notice;

    partial void OnSelectedStockChanged(PaperStockOption value) => PageThicknessText = Number(value.PageThickness);

    partial void OnPageThicknessTextChanged(string value) => UpdateSpine();

    partial void OnPageCountTextChanged(string value) => UpdateSpine();

    /// <summary>Original: <c>ActualizaValorLomo</c>.</summary>
    private void UpdateSpine() => SpineWidthText = Number(BookCover.EstimatedSpine(new BookCoverSettings
    {
        PageCount = int.TryParse(PageCountText, out int pages) ? pages : 0,
        PaperVolume = Decimal(PageThicknessText),
    }));

    /// <summary>Gives an image to one of the three parts.</summary>
    public void SetImage(BookCoverPart part, string path)
    {
        switch (part)
        {
            case BookCoverPart.Left:
                LeftImage = path;
                break;
            case BookCoverPart.Spine:
                SpineImage = path;
                break;
            default:
                RightImage = path;
                break;
        }
    }

    /// <summary>The settings as the window shows them.</summary>
    public BookCoverSettings Settings => new()
    {
        PaperSize = Paper.Paper.Size,
        PaperLandscape = true,
        PageCount = int.TryParse(PageCountText, out int pages) ? pages : 0,
        PaperVolume = Decimal(PageThicknessText),
        CoverSize = new UnitPoint { X = Length(CoverWidthText), Y = Length(CoverHeightText) },
        SpineWidth = Length(SpineWidthText),
        Spine = SpineSides[SelectedSpineSide].Value,
        LeftImagePath = LeftImage,
        SpineImagePath = SpineImage,
        RightImagePath = RightImage,
        Bleed = Length(BleedText),
        Effect = SelectedEffect.Value,
    };

    /// <summary>What stops the cover from being made, or null. Original: <c>ComprobarConfiguracion</c>.</summary>
    public string? Problem()
    {
        AppError error = BookCover.Check(Settings);
        return error.HasError ? error.Message : null;
    }

    /// <summary>The name offered for the PDF.</summary>
    public static string SuggestedName => FileNames.DocumentId("Portada");

    /// <summary>Writes the cover and opens it. Gives whether it was made. Original: <c>CrearPDFDirecto</c>.</summary>
    public async Task<bool> CreateAsync(string file)
    {
        BookCoverSettings settings = Settings;
        AppError error = BookCover.Check(settings);
        if (error.HasError)
        {
            Notice?.Invoke(error.Message);
            return false;
        }

        try
        {
            await Task.Run(() => BookCoverWriter.Write(file, settings, percent => Progress = percent));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ImageMagick.MagickException or iText.Kernel.Exceptions.PdfException or iText.IO.Exceptions.IOException)
        {
            Notice?.Invoke(exception.Message);
            return false;
        }
        finally
        {
            Progress = 0.0;
        }

        Platform.Services.OpenFile(file);
        return true;
    }

    private static string Number(double value) => Math.Round(value, 6).ToString(CultureInfo.InvariantCulture);

    private static double Decimal(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0.0;

    private static Unit Length(string? text) => string.IsNullOrEmpty(text) ? Unit.Zero : Unit.FromDisplayString(text);
}
