using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf.Canvas;
using iText.Layout.Properties;
using iText.Layout.Renderer;

namespace PnpTool.Pdf.Layout;

/// <summary>
/// Writes one line of text straight onto a page, where iText's layout would put a paragraph
/// given a fixed position: the original writes its footers, the file ID and the numbers of
/// custom decks as such paragraphs (<c>Paragraph.SetFixedPosition</c> and <c>Document.Add</c>).
/// </summary>
/// <remarks>
/// The layout of iText is not used for text because it fails under .NET 11 on ARM64 once the
/// runtime has optimised it: after some thirty paragraphs <c>LineRenderer.Layout</c> throws a
/// <see cref="NullReferenceException"/> on every call, so a project with page numbers made its
/// first sheets and then no more. With optimisation off (<c>DOTNET_JITMinOpts=1</c>) the same
/// code works. Drawing the line here gives the same text in the same place: the place was
/// measured against the layout of iText, and the comparison with the PDFs made on Windows
/// covers it.
/// </remarks>
public static class PdfTextLine
{
    /// <summary>The leading iText gives a paragraph: 1.35 times the height of its line.</summary>
    private const float Leading = 1.35f;

    /// <summary>The margin iText gives a paragraph, about whose lower end it turns it.</summary>
    private const float ParagraphMargin = 4f;

    /// <summary>The font iText takes when none is named.</summary>
    public static PdfFont DefaultFont() => PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

    /// <summary>
    /// From the bottom of a paragraph of one line to its baseline, for a font size. The line
    /// is as high as the font from ascender to descender; the leading adds to that height,
    /// half above and half below.
    /// </summary>
    public static float BaselineOffset(PdfFont font, float size)
    {
        float[] metrics = TextRenderer.CalculateAscenderDescender(font);
        float ascent = metrics[0] / 1000f;
        float descent = metrics[1] / 1000f;
        return size * (-descent + ((ascent - descent) * (Leading - 1f) / 2f));
    }

    /// <summary>
    /// Draws the text as a paragraph with its lower left corner at (x, y) and the given width
    /// would show it.
    /// </summary>
    /// <param name="upsideDown">
    /// Turned half a turn, as <c>SetRotationAngle(Math.PI)</c> does: about the left end of
    /// the paragraph, one margin below it. Only for text aligned to the left.
    /// </param>
    /// <param name="color">Black when null.</param>
    public static void Draw(PdfCanvas canvas, PdfFont font, float size, string text, float x, float y, float width, TextAlignment alignment = TextAlignment.LEFT, bool upsideDown = false, Color? color = null)
    {
        if (text.Length == 0 || size <= 0f)
        {
            return;
        }

        float offset = BaselineOffset(font, size);
        float textWidth = font.GetWidth(text, size);
        float left = alignment switch
        {
            TextAlignment.CENTER => x + ((width - textWidth) / 2f),
            TextAlignment.RIGHT => x + width - textWidth,
            _ => x,
        };

        canvas.SaveState();
        canvas.BeginText();
        canvas.SetFontAndSize(font, size);
        canvas.SetFillColor(color ?? ColorConstants.BLACK);
        if (upsideDown)
        {
            canvas.SetTextMatrix(-1f, 0f, 0f, -1f, x, y - (2f * ParagraphMargin) - offset);
        }
        else
        {
            canvas.SetTextMatrix(1f, 0f, 0f, 1f, left, y + offset);
        }

        canvas.ShowText(text);
        canvas.EndText();
        canvas.RestoreState();
    }
}
