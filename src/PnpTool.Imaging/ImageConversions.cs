using ImageMagick;

namespace PnpTool.Imaging;

/// <summary>
/// Conversions between image formats and to the forms other parts of the program consume.
/// Original: the <c>Convertir…</c> members of <c>ImagenesGeneral</c>.
/// </summary>
/// <remarks>
/// Not ported, because they exist only to move images in and out of GDI and WPF:
/// <c>FromBitmapSource</c>, <c>ConvertirImageAMagickImage</c>, <c>ConvertirMagickImageAImagen</c>
/// and <c>ConvertirPngAJpg_v2</c> (unused). <see cref="DisplayImages"/> takes their place for
/// showing images on screen.
/// </remarks>
public static class ImageConversions
{
    /// <summary>
    /// The colour of the top-left pixel, or transparent when it cannot be read.
    /// Original: <c>GetFirstPixelColorOrTransparentColor</c>.
    /// </summary>
    public static IMagickColor<ushort> FirstPixelColor(MagickImage image)
    {
        using IPixelCollection<ushort> pixels = image.GetPixels();
        return pixels.GetPixel(0, 0).ToColor() ?? MagickColors.Transparent;
    }

    /// <summary>
    /// A JPEG copy of an image with transparency: transparent areas take the colour of the
    /// top-left pixel. Quality 100. Original: <c>ConvertirPngAJpg</c>.
    /// </summary>
    public static MagickImage PngToJpeg(MagickImage image)
    {
        var copy = (MagickImage)image.Clone();
        copy.BackgroundColor = FirstPixelColor(image);
        copy.Alpha(AlphaAction.Remove);
        copy.Format = MagickFormat.Jpeg;
        copy.Quality = 100u;
        return copy;
    }

    /// <summary>A JPEG copy at quality 80, for formats PDF cannot hold. Original: <c>ConvertirWebp_Jpg</c>.</summary>
    public static MagickImage WebpToJpeg(MagickImage image)
    {
        var copy = (MagickImage)image.Clone();
        copy.Format = MagickFormat.Jpeg;
        copy.Quality = 80u;
        return copy;
    }

    /// <summary>
    /// The encoded bytes to embed in a PDF: WebP and AVIF become JPEG, and PNG too when the
    /// PDF must be PDF/X, which does not allow transparency. Other formats are encoded as
    /// they are. Original: <c>ConvertirMagickImageAItextImage</c>.
    /// </summary>
    public static byte[] ToPdfImageBytes(MagickImage image, bool pdfX)
    {
        MagickImage? converted = image.Format switch
        {
            MagickFormat.Avif or MagickFormat.WebP => WebpToJpeg(image),
            MagickFormat.Png when pdfX => PngToJpeg(image),
            _ => null,
        };
        try
        {
            return (converted ?? image).ToByteArray();
        }
        finally
        {
            converted?.Dispose();
        }
    }

    /// <summary>
    /// The image encoded in its own format in a stream positioned at the start; WebP is
    /// written as JPEG. Original: <c>ConvertirMagickImageAStream</c>.
    /// </summary>
    public static MemoryStream ToStream(MagickImage image)
    {
        var stream = new MemoryStream();
        if (image.Format == MagickFormat.WebP)
        {
            using MagickImage jpeg = WebpToJpeg(image);
            jpeg.Write(stream, MagickFormat.Jpg);
        }
        else
        {
            image.Write(stream, image.Format);
        }

        stream.Position = 0L;
        return stream;
    }
}
