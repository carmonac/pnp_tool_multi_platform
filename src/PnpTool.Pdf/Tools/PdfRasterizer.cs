using ImageMagick;
using SkiaSharp;

namespace PnpTool.Pdf.Tools;

/// <summary>
/// Turns pages of a PDF into images with PDFium, which ships with the program. The original
/// read PDFs through ImageMagick, which needs Ghostscript installed on the computer.
/// </summary>
public static class PdfRasterizer
{
    /// <summary>Number of pages of a PDF file.</summary>
    public static int PageCount(string file)
    {
        using FileStream stream = File.OpenRead(file);
        return PDFtoImage.Conversion.GetPageCount(stream);
    }

    /// <summary>
    /// Renders one page (the first is 0) at the given resolution on a white background and
    /// returns it as a PNG image that states that resolution.
    /// </summary>
    public static MagickImage RenderPage(string file, int pageIndex, int dpi)
    {
        using FileStream stream = File.OpenRead(file);
        using SKBitmap bitmap = PDFtoImage.Conversion.ToImage(stream, pageIndex, options: new PDFtoImage.RenderOptions(Dpi: dpi, BackgroundColor: SKColors.White));
        using SKData png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        var image = new MagickImage(png.ToArray()) { Format = MagickFormat.Png, Density = new Density(dpi, DensityUnit.PixelsPerInch) };
        image.ResetPage();
        return image;
    }
}
