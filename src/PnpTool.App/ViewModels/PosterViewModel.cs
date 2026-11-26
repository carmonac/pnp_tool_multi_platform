using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Core;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Pdf.Tools;

namespace PnpTool.App.ViewModels;

/// <summary>
/// The tool that splits a large picture over several sheets to glue into a poster. The
/// Windows program starts the separate program PosteRazor for this
/// (<c>PosterazorToolStripMenuItem_Click</c>); here it is part of the program.
/// </summary>
public sealed partial class PosterViewModel : ViewModelBase
{
    private readonly bool _spanish = CultureInfo.CurrentCulture.TwoLetterISOLanguageName is "es" or "ca";

    public PosterViewModel()
    {
        SizeModes = [Text("Hojas a lo ancho", "Sheets across"), Text("Ancho del póster", "Width of the poster"), Text("% del tamaño de la imagen", "% of the size of the image")];
        HorizontalOptions = [Text("Izquierda", "Left"), Text("Centro", "Centre"), Text("Derecha", "Right")];
        VerticalOptions = [Text("Arriba", "Top"), Text("Centro", "Centre"), Text("Abajo", "Bottom")];
        _marginText = Unit.FromMillimeter(5.0).ToDisplayString();
        _overlapText = Unit.FromMillimeter(10.0).ToDisplayString();
        Paper.PropertyChanged += OnPaperChanged;
        PropertyChanged += OnOwnChanged;
        Recalculate();
    }

    public PaperChoiceViewModel Paper { get; } = new();

    [ObservableProperty]
    private string _imagePath = string.Empty;

    [ObservableProperty]
    private string _marginText;

    [ObservableProperty]
    private string _overlapText;

    public IReadOnlyList<string> SizeModes { get; }

    /// <summary>Position in <see cref="SizeModes"/>, which follows <see cref="PosterSizeMode"/>.</summary>
    [ObservableProperty]
    private int _selectedSizeMode;

    /// <summary>Sheets across, width or percentage, as <see cref="SelectedSizeMode"/> says.</summary>
    [ObservableProperty]
    private string _sizeText = "2";

    public IReadOnlyList<string> HorizontalOptions { get; }

    public IReadOnlyList<string> VerticalOptions { get; }

    [ObservableProperty]
    private int _selectedHorizontal = 1;

    [ObservableProperty]
    private int _selectedVertical = 1;

    [ObservableProperty]
    private bool _cutMarks = true;

    [ObservableProperty]
    private double _progress;

    /// <summary>The sheets and the size of the poster, or what is wrong with the settings.</summary>
    [ObservableProperty]
    private string _summary = string.Empty;

    /// <summary>The poster as the settings give it; null while they do not give one.</summary>
    public PosterPlan? Plan { get; private set; }

    /// <summary>Raised when <see cref="Plan"/> changes, for the picture of the sheets.</summary>
    public event Action? PlanChanged;

    /// <summary>Raised to tell the user something.</summary>
    public event Action<string>? Notice;

    public string OverlapLabel => Text("Solape", "Overlap");

    public string AlignmentLabel => Text("Alineación", "Alignment");

    public string CutMarksLabel => Text("Marcas de recorte", "Trim marks");

    public string ImageHint => Text("Pulsa o suelta aquí una imagen", "Click or drop an image here");

    /// <summary>The settings as the window shows them.</summary>
    public PosterSettings Settings => new()
    {
        ImagePath = ImagePath,
        Paper = Paper.OrientedSize,
        Margin = Length(MarginText),
        Overlap = Length(OverlapText),
        SizeMode = (PosterSizeMode)SelectedSizeMode,
        Sheets = Number(SizeText),
        Width = Length(SizeText),
        Percent = Number(SizeText),
        Horizontal = (PosterAlignment)SelectedHorizontal,
        Vertical = (PosterAlignment)SelectedVertical,
        CutMarks = CutMarks,
    };

    /// <summary>What stops the poster from being made, or null.</summary>
    public string? Problem()
    {
        (AppError error, PosterPlan? plan) = PosterWriter.Plan(Settings);
        return plan == null ? Translate(error.Message) : null;
    }

    /// <summary>The name offered for the PDF.</summary>
    public static string SuggestedName => FileNames.DocumentId("Poster");

    /// <summary>Writes the poster and opens it. Gives whether it was made.</summary>
    public async Task<bool> CreateAsync(string file)
    {
        PosterSettings settings = Settings;
        string? problem = Problem();
        if (problem != null)
        {
            Notice?.Invoke(problem);
            return false;
        }

        try
        {
            AppError error = await Task.Run(() => PosterWriter.Write(file, settings, percent => Progress = percent));
            if (error.HasError)
            {
                Notice?.Invoke(error.Message);
                return false;
            }
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

    private void OnPaperChanged(object? sender, PropertyChangedEventArgs e) => Recalculate();

    private void OnOwnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectedSizeMode))
        {
            // A value that means the same poster in the new way of giving its size.
            SizeText = (PosterSizeMode)SelectedSizeMode switch
            {
                PosterSizeMode.Sheets => "2",
                PosterSizeMode.Width => Plan == null ? Unit.FromMillimeter(400.0).ToDisplayString() : Unit.FromPoint(Plan.PosterWidth).ToDisplayString(),
                _ => "100",
            };
        }

        if (e.PropertyName is not (nameof(Summary) or nameof(Progress)))
        {
            Recalculate();
        }
    }

    private void Recalculate()
    {
        (AppError error, PosterPlan? plan) = PosterWriter.Plan(Settings);
        Plan = plan;
        Summary = plan == null
            ? Translate(error.Message)
            : string.Format(
                CultureInfo.InvariantCulture,
                Text("{0} × {1} = {2} hojas · póster de {3} × {4}", "{0} × {1} = {2} sheets · poster of {3} × {4}"),
                plan.Columns,
                plan.Rows,
                plan.Sheets.Count,
                Unit.FromPoint(plan.PosterWidth).ToDisplayString(withSuffix: true, newLine: false),
                Unit.FromPoint(plan.PosterHeight).ToDisplayString(withSuffix: true, newLine: false));
        PlanChanged?.Invoke();
    }

    /// <summary>The messages of the poster engine, which are in English, in Spanish when the program is.</summary>
    private string Translate(string message) => !_spanish
        ? message
        : message switch
        {
            "No image is selected" => "No hay ninguna imagen elegida",
            "The paper has no size" => "El papel no tiene tamaño",
            "The margins leave no room on the sheet" => "Los márgenes no dejan sitio en la hoja",
            "The overlap must be smaller than the printed area of a sheet" => "El solape debe ser menor que la zona impresa de la hoja",
            "The poster has no size" => "El póster no tiene tamaño",
            "The image has no size" => "La imagen no tiene tamaño",
            _ => message,
        };

    private string Text(string spanish, string english) => _spanish ? spanish : english;

    private static double Number(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0.0;

    private static Unit Length(string? text) => string.IsNullOrEmpty(text) ? Unit.Zero : Unit.FromDisplayString(text);
}
