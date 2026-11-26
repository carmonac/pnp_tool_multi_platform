using ImageMagick;
using PnpTool.Core.Projects;

namespace PnpTool.Imaging;

/// <summary>
/// Grows an image of a book cover into its bleed by mirroring its edges: above and below
/// for the three images, and also on the outer side for the back and front covers.
/// Original: <c>EfectoEspejoImage_Libro</c>, which drew with GDI.
/// </summary>
public static class BookCoverMirror
{
    /// <summary>The image with its mirrored bleed; the image given is not changed.</summary>
    public static MagickImage Apply(MagickImage image, BookCoverSettings settings, BookCoverPart part)
    {
        int width = (int)image.Width;
        int height = (int)image.Height;
        double scaleX = (double)settings.CoverSize.X + (part == BookCoverPart.Spine ? 0.0 : (double)settings.Bleed);
        double scaleY = (double)settings.CoverSize.Y + (2.0 * (double)settings.Bleed);
        int newWidth = Convert.ToInt32(width * (scaleX / (double)settings.CoverSize.X));
        int newHeight = Convert.ToInt32(height * (scaleY / (double)settings.CoverSize.Y));
        int side = newWidth - width;
        int band = (newHeight - height) / 2;

        var result = new MagickImage(MagickColors.White, (uint)newWidth, (uint)newHeight)
        {
            Density = image.Density,
            Format = image.Format == MagickFormat.Unknown ? MagickFormat.Png : image.Format,
        };
        int left = part == BookCoverPart.Left ? side : 0;
        result.Composite(image, left, band, CompositeOperator.Copy);

        if (side > 0 && part == BookCoverPart.Left)
        {
            // The first columns of the image, mirrored, to its left.
            Paste(result, left, band, side, height, flop: true, flip: false, 0, band);
        }

        if (side > 0 && part == BookCoverPart.Right)
        {
            // The last columns of the image, mirrored, to its right.
            Paste(result, width - side, band, side, height, flop: true, flip: false, width, band);
        }

        if (band > 0)
        {
            // The first and last rows, sides included, mirrored above and below.
            Paste(result, 0, band, newWidth, band, flop: false, flip: true, 0, 0);
            Paste(result, 0, height, newWidth, band, flop: false, flip: true, 0, newHeight - band);
        }

        return result;
    }

    private static void Paste(MagickImage canvas, int x, int y, int width, int height, bool flop, bool flip, int targetX, int targetY)
    {
        using IMagickImage<ushort> strip = canvas.Clone();
        strip.Crop(new MagickGeometry(x, y, (uint)width, (uint)height));
        strip.ResetPage();
        if (flop)
        {
            strip.Flop();
        }

        if (flip)
        {
            strip.Flip();
        }

        canvas.Composite(strip, targetX, targetY, CompositeOperator.Copy);
    }
}
