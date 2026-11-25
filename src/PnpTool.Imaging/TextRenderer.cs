using ImageMagick;
using PnpTool.Core.CustomDecks;
using PnpTool.Core.Primitives;
using SkiaSharp;

namespace PnpTool.Imaging;

/// <summary>
/// Draws a text into an image with a transparent background, to be laid over a card.
/// Original: <c>TextToBitmap</c>, which used WPF's text layout; here SkiaSharp draws it.
/// </summary>
public static class TextRenderer
{
    /// <summary>
    /// Renders the text in the given font at <paramref name="dpi"/>. The font size is in
    /// 1/96 inch, as in the original, so at 300 dpi a size of 96 gives letters an inch tall.
    /// The image is as wide as the longest line and as tall as its lines; line breaks in the
    /// text start new lines. An empty text gives a 1 × 1 transparent image.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="font">Family, size, weight, slant, width and colour. Decorations are not drawn, as in the original.</param>
    /// <param name="dpi">Resolution of the image.</param>
    /// <param name="padding">
    /// Offset of the text from the top-left corner in pixels. As in the original it moves the
    /// text without enlarging the image, so anything but 0 cuts the text off.
    /// </param>
    public static MagickImage Render(string? text, FontSpec font, double dpi, double padding = 0)
    {
        text ??= string.Empty;
        string[] lines = text.ReplaceLineEndings("\n").Split('\n');

        // Not disposed: bundled typefaces belong to the catalogue and system ones to Skia's cache.
        SKTypeface typeface = FontCatalog.Resolve(font);
        using var skFont = new SKFont(typeface, (float)(font.FontSize * dpi / 96.0)) { Subpixel = true, Edging = SKFontEdging.Antialias };
        SKFontMetrics metrics = skFont.Metrics;
        float lineHeight = metrics.Descent - metrics.Ascent + metrics.Leading;

        float widest = lines.Max(line => skFont.MeasureText(line));
        int width = Math.Max(1, (int)widest);
        int height = Math.Max(1, (int)(lineHeight * lines.Length));
        if (text.Length == 0)
        {
            width = 1;
            height = 1;
        }

        ArgbColor color = ArgbColor.Parse(font.Color);
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        using (var paint = new SKPaint { Color = new SKColor(color.R, color.G, color.B, 255), IsAntialias = true })
        {
            for (int i = 0; i < lines.Length; i++)
            {
                float baseline = (float)padding - metrics.Ascent + (i * lineHeight);
                canvas.DrawText(lines[i], (float)padding, baseline, SKTextAlign.Left, skFont, paint);
            }
        }

        using SKImage snapshot = surface.Snapshot();
        using SKData png = snapshot.Encode(SKEncodedImageFormat.Png, 100);
        var image = new MagickImage(png.ToArray()) { Density = new Density(dpi, DensityUnit.PixelsPerInch) };
        image.ResetPage();
        return image;
    }
}
