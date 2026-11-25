using System.Globalization;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using PnpTool.Core.Primitives;
using Path = System.IO.Path;
using PointF = PnpTool.Core.Primitives.PointF;

namespace PnpTool.Pdf.Tools;

/// <summary>Which pages a change applies to. Original: the index of the pages list in the alignment window.</summary>
public enum PageSelection
{
    All = 0,

    /// <summary>The second, fourth, … page: the backs of a double-sided document.</summary>
    Even = 1,

    /// <summary>The first, third, … page.</summary>
    Odd = 2,

    /// <summary>The pages named in a text such as "1;3;5-8".</summary>
    Listed = 3,
}

/// <summary>
/// Changes to the pages of an existing PDF: sheet size, and position and rotation of the
/// content, to correct a printer that does not line the two sides of a sheet up. Original:
/// <c>WindowModificarTamanoHojaPdfModelo</c>, <c>WindowModificarAlineacionPdfModelo</c> and
/// <c>FicheroPdfMaquetadoNoDependienteLibreria.TamanoPagina</c>.
/// </summary>
public static class PdfPageTools
{
    /// <summary>Size of the first page in points. Original: <c>TamanoPagina</c>.</summary>
    public static PointF PageSize(string file)
    {
        using var document = new PdfDocument(new PdfReader(file));
        Rectangle size = document.GetPage(1).GetPageSize();
        return new PointF(size.GetWidth(), size.GetHeight());
    }

    /// <summary>
    /// Writes a copy of the PDF, named <c>&lt;name&gt;_NewSize.pdf</c> beside it, whose sheets
    /// have the new size. The content keeps its size and its distance from the top-right
    /// corner; the sheet grows or shrinks at the left and bottom. Returns the path of the copy.
    /// Original: <c>ModificaTamano</c>.
    /// </summary>
    public static string ChangePageSize(string file, UnitPoint newSize)
    {
        string destination = Path.Combine(Path.GetDirectoryName(file) ?? string.Empty, Path.GetFileNameWithoutExtension(file) + "_NewSize.pdf");
        PointF previous = PageSize(file);
        double growX = Math.Round(newSize.X.RoundedPoints() - previous.X, 2);
        double growY = Math.Round(newSize.Y.RoundedPoints() - previous.Y, 2);
        using var document = new PdfDocument(new PdfReader(file), new PdfWriter(destination));
        for (int i = 1; i <= document.GetNumberOfPages(); i++)
        {
            PdfPage page = document.GetPage(i);
            Rectangle box = page.GetMediaBox();
            var resized = new Rectangle((float)(box.GetX() - growX), (float)(box.GetY() - growY), (float)(box.GetWidth() + growX), (float)(box.GetHeight() + growY));
            page.SetMediaBox(resized);
            if (page.GetPdfObject().ContainsKey(PdfName.CropBox))
            {
                page.SetCropBox(resized);
            }
        }

        return destination;
    }

    /// <summary>
    /// The pages named by a text such as "1;3;5-8", "-3" (up to the third) or "4-" (from the
    /// fourth on), as zero-based indexes in order. Original: <c>ObtenerPaginasSeleccionadas</c>,
    /// in which a range discarded the pages named before it; here they add up. Pages beyond
    /// the document are left out.
    /// </summary>
    public static List<int> ParsePages(int pageCount, string? text)
    {
        var selected = new SortedSet<int>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        foreach (string part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Contains('-'))
            {
                string[] ends = part.Split('-');
                int first = string.IsNullOrEmpty(ends[0]) ? 0 : int.Parse(ends[0]) - 1;
                int last = string.IsNullOrEmpty(ends[1]) ? pageCount - 1 : int.Parse(ends[1]) - 1;
                for (int i = first; i <= last; i++)
                {
                    selected.Add(i);
                }
            }
            else
            {
                selected.Add(int.Parse(part) - 1);
            }
        }

        return [.. selected.Where(page => page >= 0 && page < pageCount)];
    }

    private static bool Applies(PageSelection selection, int pageIndex, List<int> listed) => selection switch
    {
        PageSelection.All => true,
        PageSelection.Even => pageIndex % 2 == 1,
        PageSelection.Odd => pageIndex % 2 == 0,
        PageSelection.Listed => listed.Contains(pageIndex),
        _ => false,
    };

    /// <summary>
    /// Writes a copy of the PDF in which the content of the chosen pages is moved and turned.
    /// </summary>
    /// <param name="source">The PDF.</param>
    /// <param name="destination">The copy to write.</param>
    /// <param name="pages">Pages for <see cref="PageSelection.Listed"/>, as in <see cref="ParsePages"/>.</param>
    /// <param name="selection">Which pages to change.</param>
    /// <param name="offset">
    /// How far the sheet's window moves over the content: a positive X shows the content
    /// further left, a positive Y further down.
    /// </param>
    /// <param name="angle">Degrees to turn the content about the centre of the sheet, counter-clockwise.</param>
    /// <remarks>
    /// Original: <c>ModificaAlineacion</c>. There the angle only produced a small shift written
    /// after the page's content, where it changes nothing, so the content was never turned.
    /// Here it is turned, by an instruction placed before the content.
    /// </remarks>
    public static void Align(string source, string destination, string? pages, PageSelection selection, UnitPoint offset, double angle)
    {
        using var document = new PdfDocument(new PdfReader(source), new PdfWriter(destination));
        int count = document.GetNumberOfPages();
        List<int> listed = ParsePages(count, pages);
        for (int index = 0; index < count; index++)
        {
            if (!Applies(selection, index, listed))
            {
                continue;
            }

            PdfPage page = document.GetPage(index + 1);
            Rectangle box = page.GetMediaBox();
            var moved = new Rectangle(box.GetX() + (float)(double)offset.X, box.GetY() + (float)(double)offset.Y, box.GetWidth(), box.GetHeight());
            page.SetMediaBox(moved);
            page.SetCropBox(moved);
            if (angle != 0.0)
            {
                double centreX = moved.GetX() + (moved.GetWidth() / 2.0);
                double centreY = moved.GetY() + (moved.GetHeight() / 2.0);
                // Written as raw operators: "q … cm" before the page's content and "Q" after it,
                // which the library's canvas would reject as unbalanced within one stream.
                float[] matrix = new float[6];
                AffineTransform.GetRotateInstance(angle * Math.PI / 180.0, centreX, centreY).GetMatrix(matrix);
                string numbers = string.Join(" ", matrix.Select(value => value.ToString("0.######", CultureInfo.InvariantCulture)));
                page.NewContentStreamBefore().GetOutputStream().WriteString("q " + numbers + " cm\n");
                page.NewContentStreamAfter().GetOutputStream().WriteString("\nQ\n");
            }
        }
    }

    /// <summary>Turns the content of every page of a PDF file, replacing the file.</summary>
    public static void RotateInPlace(string file, double angle)
    {
        string rotated = file + ".rotated";
        Align(file, rotated, null, PageSelection.All, new UnitPoint(), angle);
        File.Move(rotated, file, overwrite: true);
    }
}
