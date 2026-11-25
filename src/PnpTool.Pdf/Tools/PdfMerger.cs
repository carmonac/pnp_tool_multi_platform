using System.Globalization;
using iText.Kernel.Pdf;
using PnpTool.Core;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PointD = PnpTool.Core.Primitives.PointD;

namespace PnpTool.Pdf.Tools;

/// <summary>
/// Joins PDF files into one, either one after another or by interleaving the pages of a file
/// of fronts with those of a file of backs. Original: <c>FormUnirPdfModeloIText</c>.
/// </summary>
public sealed class PdfMerger
{
    private double _cards;
    private double _sheets;
    private readonly PointD _paper = new();
    private readonly PointD _card = new();
    private readonly PointD _spacing = new();
    private double _bleed;
    private int _doubleSided;

    /// <summary>Take one page from the first file, one from the second, and so on. Original: <c>Intercalar</c>.</summary>
    public bool Interleave { get; set; }

    /// <summary>Original: <c>FicheroDestino</c>.</summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>The files to join, in order. Original: <c>FicherosEntrada</c> and <c>CopiarCadenas</c>.</summary>
    public IReadOnlyList<string> Sources { get; set; } = [];

    /// <summary>Called once with the number of steps. Original: <c>StartChanged</c>.</summary>
    public event Action<int>? Started;

    /// <summary>Called with the number of steps done. Original: <c>ProgressChanged</c>.</summary>
    public event Action<int>? ProgressChanged;

    /// <summary>
    /// What is wrong with the request, if anything: fewer than two files, no destination, or,
    /// for interleaving, not exactly two files or files with different numbers of pages.
    /// Original: <c>TodoOk</c>.
    /// </summary>
    public AppError Validate()
    {
        var error = new AppError();
        if (Sources.Count <= 1)
        {
            error.Kind = ErrorKind.Error;
            error.AppendResource("no_hay ficheros_unir_pdf");
        }

        if (string.IsNullOrEmpty(Destination))
        {
            error.Kind = ErrorKind.Error;
            error.AppendResource("faltan_datos");
        }

        if (Interleave)
        {
            if (Sources.Count != 2)
            {
                error.Kind = ErrorKind.Error;
                error.AppendResource("aviso_intercalar_pdf");
            }
            else if (PageCount(Sources[0]) != PageCount(Sources[1]))
            {
                error.Kind = ErrorKind.Error;
                error.AppendResource("Paginas_pdf_no_iguales");
            }
        }

        return error;
    }

    private static int PageCount(string file)
    {
        using var document = new PdfDocument(new PdfReader(file));
        return document.GetNumberOfPages();
    }

    /// <summary>Joins the files in the way chosen by <see cref="Interleave"/>.</summary>
    public void Merge()
    {
        if (Interleave)
        {
            MergeInterleaved();
        }
        else
        {
            MergeInOrder();
        }
    }

    /// <summary>
    /// Front, back, front, back… from the two files. When the second file has a different
    /// number of pages its first page is used as the back of every front.
    /// Original: <c>FusionarIntercalar</c>.
    /// </summary>
    public void MergeInterleaved()
    {
        using var output = new PdfDocument(new PdfWriter(Destination));
        using var fronts = new PdfDocument(new PdfReader(Sources[0]));
        using var backs = new PdfDocument(new PdfReader(Sources[1]));
        int frontCount = fronts.GetNumberOfPages();
        int backCount = backs.GetNumberOfPages();
        Started?.Invoke(frontCount);
        ProgressChanged?.Invoke(0);
        for (int i = 0; i < frontCount; i++)
        {
            fronts.CopyPagesTo(i + 1, i + 1, output);
            int back = frontCount != backCount ? 1 : i + 1;
            backs.CopyPagesTo(back, back, output);
            ProgressChanged?.Invoke(i + 1);
        }
    }

    /// <summary>
    /// The files one after another. The properties PNP Tool writes into its PDFs are combined:
    /// card and sheet counts are added up, and the sizes are kept when all the files agree
    /// (-1 otherwise). Original: <c>FusionarNormal</c>.
    /// </summary>
    public void MergeInOrder()
    {
        using var output = new PdfDocument(new PdfWriter(Destination));
        Started?.Invoke(Sources.Count);
        ProgressChanged?.Invoke(0);
        for (int i = 0; i < Sources.Count; i++)
        {
            using var input = new PdfDocument(new PdfReader(Sources[i]));
            ReadProperties(input);
            input.CopyPagesTo(1, input.GetNumberOfPages(), output);
            ProgressChanged?.Invoke(i + 1);
        }

        WriteProperties(output);
    }

    /// <summary>Original: <c>LeerAtributos</c>.</summary>
    private void ReadProperties(PdfDocument input)
    {
        _cards = Combine(input, PdfMetadataKeys.CardCount, _cards, add: true);
        _sheets = Combine(input, PdfMetadataKeys.SheetCount, _sheets, add: true);
        _paper.X = Combine(input, PdfMetadataKeys.SheetWidth, _paper.X, add: false);
        _paper.Y = Combine(input, PdfMetadataKeys.SheetHeight, _paper.Y, add: false);
        _card.X = Combine(input, PdfMetadataKeys.CardWidth, _card.X, add: false);
        _card.Y = Combine(input, PdfMetadataKeys.CardHeight, _card.Y, add: false);
        _spacing.X = Combine(input, PdfMetadataKeys.SpacingX, _spacing.X, add: false);
        _spacing.Y = Combine(input, PdfMetadataKeys.SpacingY, _spacing.Y, add: false);
        _bleed = Combine(input, PdfMetadataKeys.Bleed, _bleed, add: false);
        _doubleSided = Convert.ToInt32(Combine(input, PdfMetadataKeys.DoubleSided, _doubleSided, add: false));
    }

    /// <summary>
    /// The value of a property after taking one more file into account. A file that lacks the
    /// property gives -1. Original: <c>LeeAtributo</c>, which failed on a PDF not made by PNP Tool.
    /// </summary>
    private static double Combine(PdfDocument input, string key, double soFar, bool add)
    {
        string? text = input.GetDocumentInfo().GetMoreInfo(key);
        if (string.IsNullOrEmpty(text))
        {
            return -1.0;
        }

        double value = text switch
        {
            "True" => 1,
            "False" => 0,
            "0" => 0.0,
            _ => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : 0.0,
        };
        if (add && soFar >= 0.0)
        {
            return soFar + value;
        }

        if (soFar != value)
        {
            return soFar != 0.0 ? -1.0 : value;
        }

        return soFar;
    }

    private static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Original: <c>Finalizar_PDF</c>.</summary>
    private void WriteProperties(PdfDocument output)
    {
        PdfDocumentInfo info = output.GetDocumentInfo();
        info.SetAuthor(PdfMetadataKeys.Author);
        string id = FileNames.DocumentId(string.Empty);
        info.SetTitle(id);
        info.SetMoreInfo(PdfMetadataKeys.PdfId, id);
        info.SetMoreInfo(PdfMetadataKeys.DoubleSided, _doubleSided == 1 ? "True" : "False");
        info.SetMoreInfo(PdfMetadataKeys.CardCount, Number(_cards));
        info.SetMoreInfo(PdfMetadataKeys.GroupIndex, Number(-1.0));
        info.SetMoreInfo(PdfMetadataKeys.SheetCount, Number(_sheets));
        info.SetMoreInfo(PdfMetadataKeys.SheetWidth, Number(_paper.X));
        info.SetMoreInfo(PdfMetadataKeys.SheetHeight, Number(_paper.Y));
        info.SetMoreInfo(PdfMetadataKeys.CardWidth, Number(_card.X));
        info.SetMoreInfo(PdfMetadataKeys.CardHeight, Number(_card.Y));
        info.SetMoreInfo(PdfMetadataKeys.SpacingX, Number(_spacing.X));
        info.SetMoreInfo(PdfMetadataKeys.SpacingY, Number(_spacing.Y));
        info.SetMoreInfo(PdfMetadataKeys.Bleed, Number(_bleed));
        info.SetMoreInfo(PdfMetadataKeys.ProgramVersion, AppSettings.ProgramVersion);
    }
}
