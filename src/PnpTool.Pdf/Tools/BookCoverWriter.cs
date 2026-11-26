using ImageMagick;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Element;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Imaging;
using PnpTool.Pdf.Layout;

namespace PnpTool.Pdf.Tools;

/// <summary>
/// Writes the cover of a book as a one-sheet PDF: back cover, spine and front cover centred
/// on the sheet with their bleed, the calibration strip at the foot and the cut marks.
/// Original: <c>FicheroPdfMaquetadoLibro</c>.
/// </summary>
public static class BookCoverWriter
{
    /// <summary>
    /// Writes the PDF. The settings must have passed <see cref="BookCover.Check"/>.
    /// Original: <c>CrearPDF</c>, which also opened the file; that is left to the caller.
    /// </summary>
    /// <param name="progress">Told the percentage done.</param>
    public static void Write(string file, BookCoverSettings settings, Action<int>? progress = null)
    {
        progress?.Invoke(0);
        UnitPoint paper = BookCover.OrientedPaper(settings);
        UnitPoint frame = BookCover.Frame(settings);
        float paperWidth = (float)paper.X.Point;
        float paperHeight = (float)paper.Y.Point;
        float bleed = (float)settings.Bleed.Point;
        float startX = (float)((paper.X.Point - frame.X.Point) / 2.0);
        float startY = (float)((paper.Y.Point - frame.Y.Point) / 2.0);
        float coverWidth = (float)(settings.CoverSize.X.Point + settings.Bleed.Point);
        float coverHeight = (float)(settings.CoverSize.Y.Point + (2.0 * settings.Bleed.Point));
        float spineWidth = (float)settings.SpineWidth.Point;

        using var writer = new PdfWriter(file);
        using var pdf = new PdfDocument(writer);
        using var document = new Document(pdf);
        pdf.GetDocumentInfo().SetAuthor("PNP Tool");
        PdfPage page = pdf.AddNewPage(new PageSize(paperWidth, paperHeight));
        var canvas = new PdfCanvas(page);

        string strip = System.IO.Path.Combine(AppSettings.ImagesFolder, "calibracion.jpg");
        if (File.Exists(strip))
        {
            document.Add(new Image(ImageDataFactory.Create(strip))
                .ScaleAbsolute(200f, 15f)
                .SetFixedPosition((paperWidth / 2f) - 100f, (float)Unit.FromMillimeter(3.0).Point));
        }

        progress?.Invoke(33);
        Place(document, settings, BookCoverPart.Left, settings.LeftImagePath, startX, startY, coverWidth, coverHeight);
        progress?.Invoke(66);
        Place(document, settings, BookCoverPart.Spine, settings.SpineImagePath, startX + coverWidth, startY, spineWidth, coverHeight);
        progress?.Invoke(90);
        Place(document, settings, BookCoverPart.Right, settings.RightImagePath, startX + coverWidth + spineWidth, startY, coverWidth, coverHeight);

        // Cut marks: the four edges of the cover across the sheet, and the two folds of the
        // spine above and below it. Original: InsertarMarcasDeCorte.
        canvas.SaveState();
        canvas.SetLineWidth(1f);
        canvas.SetStrokeColor(ColorConstants.BLACK);
        double bottom = startY + bleed;
        double top = paperHeight - startY - bleed;
        double left = startX + bleed;
        double right = startX + frame.X.Point - bleed;
        Line(canvas, 0.0, bottom, paperWidth, bottom);
        Line(canvas, 0.0, top, paperWidth, top);
        Line(canvas, left, 0.0, left, paperHeight);
        Line(canvas, right, 0.0, right, paperHeight);
        double fold = left + settings.CoverSize.X.Point;
        double below = bottom - (2.0 * bleed);
        double above = top + (2.0 * bleed);
        Line(canvas, fold, 0.0, fold, below);
        Line(canvas, fold, above, fold, paperHeight);
        fold += spineWidth;
        Line(canvas, fold, above, fold, paperHeight);
        Line(canvas, fold, 0.0, fold, below);
        canvas.RestoreState();
        canvas.Release();
        progress?.Invoke(100);
    }

    private static void Place(Document document, BookCoverSettings settings, BookCoverPart part, string path, float x, float y, float width, float height)
    {
        using var image = new MagickImage(path);
        image.ResetPage();
        MagickImage shown = settings.Effect == ImageEffectType.Mirror ? BookCoverMirror.Apply(image, settings, part) : image;
        try
        {
            (ImageData data, _) = PdfLayoutWriter.ToImageData(shown, pdfX: false);
            document.Add(new Image(data).ScaleAbsolute(width, height).SetFixedPosition(x, y));
        }
        finally
        {
            if (!ReferenceEquals(shown, image))
            {
                shown.Dispose();
            }
        }
    }

    private static void Line(PdfCanvas canvas, double x1, double y1, double x2, double y2)
    {
        canvas.MoveTo(x1, y1);
        canvas.LineTo(x2, y2);
        canvas.ClosePathFillStroke();
    }
}
