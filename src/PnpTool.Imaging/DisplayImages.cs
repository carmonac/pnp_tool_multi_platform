using ImageMagick;
using PnpTool.Core;
using PnpTool.Core.Primitives;

namespace PnpTool.Imaging;

/// <summary>
/// Pixels ready to be shown on screen: 8 bits per channel in blue, green, red, alpha order,
/// not premultiplied, rows from the top with no padding. Any user-interface library can wrap
/// this in a bitmap; this project does not depend on one.
/// </summary>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="Pixels"><c>Width × Height × 4</c> bytes.</param>
/// <param name="DpiX">Horizontal resolution, <see cref="ImageDensity.Default"/> when the image states none.</param>
/// <param name="DpiY">Vertical resolution.</param>
public sealed record DisplayImage(int Width, int Height, byte[] Pixels, double DpiX, double DpiY)
{
    /// <summary>Bytes per row.</summary>
    public int Stride => Width * 4;
}

/// <summary>
/// Turns images into <see cref="DisplayImage"/>s. Replaces the conversions to
/// <c>System.Drawing.Image</c> and WPF bitmaps of the original, and the Windows shell
/// thumbnails its image panel used (<c>ImagenesWpf.DevuelveIconoSistema</c>).
/// </summary>
public static class DisplayImages
{
    /// <summary>
    /// Longest side in pixels of a panel thumbnail, 0 for the image at full size. The sizes
    /// are those of the Windows shell thumbnails the original asked for.
    /// </summary>
    public static int ThumbnailSize(ImageResolutionType resolution) => resolution switch
    {
        ImageResolutionType.SmallBitmap => 32,
        ImageResolutionType.MediumBitmap => 96,
        ImageResolutionType.LargeBitmap => 256,
        ImageResolutionType.ExtraLargeBitmap => 1024,
        _ => 0,
    };

    /// <summary>The pixels of a loaded image, converted to sRGB when it is in another colour space.</summary>
    public static DisplayImage From(MagickImage image)
    {
        using var copy = (MagickImage)image.Clone();
        if (copy.ColorSpace != ColorSpace.sRGB)
        {
            copy.ColorSpace = ColorSpace.sRGB;
        }

        copy.Alpha(AlphaAction.Set);
        using IPixelCollection<ushort> pixels = copy.GetPixels();
        byte[] bytes = pixels.ToByteArray(PixelMapping.BGRA)
            ?? throw new MagickCorruptImageErrorException("The image has no pixels.");
        PointU dpi = ImageDensity.Normalize(copy.Density);
        return new DisplayImage((int)copy.Width, (int)copy.Height, bytes, dpi.X, dpi.Y);
    }

    /// <summary>
    /// Loads a file for display, upright according to its EXIF orientation. With
    /// <paramref name="longestSide"/> above 0 a larger image is scaled down to fit that
    /// size, keeping its proportions; a smaller one is left as it is.
    /// </summary>
    public static DisplayImage Load(string path, int longestSide = 0)
    {
        using var image = new MagickImage(path);
        image.AutoOrient();
        image.ResetPage();
        if (longestSide > 0 && (image.Width > longestSide || image.Height > longestSide))
        {
            image.Thumbnail((uint)longestSide, (uint)longestSide);
        }

        return From(image);
    }

    /// <summary>Loads the thumbnail of a file for the image panel at the given setting.</summary>
    public static DisplayImage LoadThumbnail(string path, ImageResolutionType resolution) =>
        Load(path, ThumbnailSize(resolution));
}
