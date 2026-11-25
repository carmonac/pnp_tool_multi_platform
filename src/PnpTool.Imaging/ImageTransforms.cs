using ImageMagick;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.Imaging;

/// <summary>
/// Changes of size and orientation applied to a loaded image. Original: the resize, rotate
/// and watermark members of <c>ImagenesGeneral</c>.
/// </summary>
/// <remarks>
/// The GDI helpers of the original (<c>CortayRotaImage</c>, <c>EscalaImagen</c>,
/// <c>CreaMapadeBits</c>, <c>PintaBitmapColor</c>) have no counterpart: their callers work on
/// <see cref="MagickImage"/> here.
/// </remarks>
public static class ImageTransforms
{
    /// <summary>
    /// Scales the image to a percentage of its size, width and height each rounded down.
    /// Original: <c>ResizeMagicImagePorcentaje</c>.
    /// </summary>
    public static void ResizeByPercent(MagickImage image, double percent, FilterType filter)
    {
        percent /= 100.0;
        var geometry = new MagickGeometry
        {
            Width = (uint)(image.Width * percent),
            Height = (uint)(image.Height * percent),
            X = 0,
            Y = 0,
            IgnoreAspectRatio = true,
        };
        image.FilterType = filter;
        image.Resize(geometry);
    }

    /// <summary>
    /// Scales the image so that it prints at <paramref name="size"/> and then resamples it to
    /// the program's target resolution. An image that states no resolution is taken to fill
    /// <paramref name="size"/> already. Original: <c>ResizeMagicImageTamanoEspecifico</c>.
    /// </summary>
    public static void ResizeToPrintedSize(MagickImage image, UnitPoint size, FilterType filter)
    {
        var dpi = new PointI();
        switch (image.Density.Units)
        {
            case DensityUnit.Undefined:
                dpi.X = Convert.ToInt32(image.Width / size.X.Inch);
                dpi.Y = Convert.ToInt32(image.Height / size.Y.Inch);
                image.Density = new Density(dpi.X, dpi.Y, DensityUnit.PixelsPerInch);
                break;
            case DensityUnit.PixelsPerInch:
                dpi.X = Convert.ToInt32(image.Density.X);
                dpi.Y = Convert.ToInt32(image.Density.Y);
                break;
            case DensityUnit.PixelsPerCentimeter:
                dpi.X = Convert.ToInt32(image.Density.X * 2.54);
                dpi.Y = Convert.ToInt32(image.Density.Y * 2.54);
                break;
        }

        var geometry = new MagickGeometry
        {
            Width = (uint)(size.X.Inch * dpi.X) + 1,
            Height = (uint)(size.Y.Inch * dpi.Y) + 1,
            X = 0,
            Y = 0,
            IgnoreAspectRatio = true,
        };
        image.FilterType = filter;
        image.Resize(geometry);
        image.Resample(AppSettings.TargetDpi, AppSettings.TargetDpi);
        image.Density = new Density(AppSettings.TargetDpi, AppSettings.TargetDpi, DensityUnit.PixelsPerInch);
    }

    /// <summary>
    /// Turns the image clockwise by the angle, then mirrors it left-right and top-bottom as
    /// asked. Returns the same image. Original: <c>RotarMagickImage</c>.
    /// </summary>
    public static MagickImage Rotate(MagickImage image, RotationProperties rotation)
    {
        if (rotation.Angle != 0)
        {
            image.Rotate(rotation.Angle);
        }

        if (rotation.FlipHorizontal)
        {
            image.Flop();
        }

        if (rotation.FlipVertical)
        {
            image.Flip();
        }

        return image;
    }

    /// <summary>
    /// Draws the watermark over the image, scaled to fit inside it. The watermark image is
    /// resized by this call. Returns the image. Original: <c>SuperponMarcaAgua_v2</c>.
    /// </summary>
    public static MagickImage OverlayWatermark(MagickImage image, MagickImage watermark, FilterType filter)
    {
        watermark.FilterType = filter;
        watermark.Resize(image.Width, image.Height);
        image.Composite(watermark, CompositeOperator.Over);
        return image;
    }
}
