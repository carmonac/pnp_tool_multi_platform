using CommunityToolkit.Mvvm.ComponentModel;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Paper;
using PnpTool.Core.Primitives;
using PnpTool.Pdf.Tools;

namespace PnpTool.App.ViewModels;

/// <summary>
/// The two tools that change an existing PDF: the size of its sheets, and the place and
/// turn of what is on them, to make up for a printer that does not line both sides up.
/// Original: <c>Window_ModificarTamanoHojaPDF</c> and <c>Window_ModificarAlineacionPDF</c>,
/// which the ribbon offered as two entries of one button.
/// </summary>
public sealed partial class ModifyPdfViewModel : ViewModelBase
{
    public ModifyPdfViewModel()
    {
        PageOptions =
        [
            StringResources.Get("Paginas_Todas"),
            StringResources.Get("Paginas_Pares"),
            StringResources.Get("Paginas_Impares"),
            StringResources.Get("Paginas_Seleccion"),
        ];
    }

    /// <summary>The PDF to change.</summary>
    [ObservableProperty]
    private string _file = string.Empty;

    /// <summary>The size of its sheets now, as "210 - 297 A4".</summary>
    [ObservableProperty]
    private string _currentSizeText = string.Empty;

    /// <summary>The new size of the sheets.</summary>
    public PaperChoiceViewModel Paper { get; } = new();

    public IReadOnlyList<string> PageOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PagesTyped))]
    private int _selectedPages;

    /// <summary>The pages to change, as "1;4-6", when they are chosen one by one.</summary>
    [ObservableProperty]
    private string _pagesText = string.Empty;

    public bool PagesTyped => SelectedPages == (int)PageSelection.Listed;

    [ObservableProperty]
    private string _offsetXText = "0";

    [ObservableProperty]
    private string _offsetYText = "0";

    /// <summary>The turn, in degrees.</summary>
    [ObservableProperty]
    private string _angleText = "0";

    /// <summary>Raised to tell the user something.</summary>
    public event Action<string>? Notice;

    partial void OnFileChanged(string value)
    {
        CurrentSizeText = string.Empty;
        if (!System.IO.File.Exists(value))
        {
            return;
        }

        try
        {
            PointF size = PdfPageTools.PageSize(value);
            CurrentSizeText = Core.Paper.Paper.Describe(size.X, size.Y);
        }
        catch (Exception exception) when (exception is IOException or iText.Kernel.Exceptions.PdfException or iText.IO.Exceptions.IOException)
        {
            Notice?.Invoke(exception.Message);
        }
    }

    /// <summary>The file the copy with another size is written to, beside the original.</summary>
    public string ResizedFile => Path.Combine(Path.GetDirectoryName(File) ?? string.Empty, Path.GetFileNameWithoutExtension(File) + "_NewSize.pdf");

    /// <summary>
    /// Writes the copy with sheets of the size chosen. Gives its path, or null when something
    /// is missing or fails. Original: <c>Boton_Comenzar_Click</c>.
    /// </summary>
    public string? ChangeSize()
    {
        UnitPoint size = Paper.Paper.Size;
        if (!System.IO.File.Exists(File) || (double)size.X <= 0.0 || (double)size.Y <= 0.0)
        {
            Notice?.Invoke(StringResources.Get("faltan_datos"));
            return null;
        }

        try
        {
            string written = PdfPageTools.ChangePageSize(File, size);
            Notice?.Invoke(StringResources.Get("proceso_completado"));
            return written;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or iText.Kernel.Exceptions.PdfException or iText.IO.Exceptions.IOException)
        {
            Notice?.Invoke(exception.Message);
            return null;
        }
    }

    /// <summary>
    /// Writes a copy with the pages chosen moved and turned. Gives whether it was done.
    /// Original: <c>Boton_Procesar</c> of the alignment window.
    /// </summary>
    public bool Align(string? destination)
    {
        if (!System.IO.File.Exists(File) || string.IsNullOrEmpty(destination))
        {
            Notice?.Invoke(StringResources.Get("faltan_datos"));
            return false;
        }

        if (string.Equals(Path.GetFullPath(destination), Path.GetFullPath(File), StringComparison.OrdinalIgnoreCase))
        {
            Notice?.Invoke("Same File origin / destination");
            return false;
        }

        var offset = new UnitPoint { X = Parse(OffsetXText), Y = Parse(OffsetYText) };
        double angle = double.TryParse(AngleText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double typed) ? typed : 0.0;
        try
        {
            PdfPageTools.Align(File, destination, PagesText, (PageSelection)SelectedPages, offset, angle);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or iText.Kernel.Exceptions.PdfException or iText.IO.Exceptions.IOException)
        {
            Notice?.Invoke(exception.Message);
            return false;
        }
    }

    private static Unit Parse(string? text) => string.IsNullOrEmpty(text) || text == "-" ? Unit.Zero : Unit.FromDisplayString(text);
}
