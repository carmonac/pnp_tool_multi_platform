using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using SkiaSharp;

namespace PnpTool.Pdf.Tests;

/// <summary>Checks that iText writes a PDF and that PDFium loads and renders it on the current OS and CPU.</summary>
public class NativeLibrarySmokeTests
{
    private static byte[] CreateOnePageA4Pdf()
    {
        using var stream = new MemoryStream();
        using (var document = new PdfDocument(new PdfWriter(stream)))
        {
            document.AddNewPage(PageSize.A4);
        }

        return stream.ToArray();
    }

    [Fact]
    public void IText_writes_a_pdf_that_it_can_read_back()
    {
        byte[] pdf = CreateOnePageA4Pdf();

        using var document = new PdfDocument(new PdfReader(new MemoryStream(pdf)));
        Assert.Equal(1, document.GetNumberOfPages());
        Assert.Equal(PageSize.A4.GetWidth(), document.GetPage(1).GetPageSize().GetWidth());
    }

    [Fact]
    public void Pdfium_renders_a_page_without_ghostscript()
    {
        byte[] pdf = CreateOnePageA4Pdf();

        using SKBitmap page = PDFtoImage.Conversion.ToImage(pdf, 0);
        Assert.True(page.Width > 0 && page.Height > 0);
    }
}
