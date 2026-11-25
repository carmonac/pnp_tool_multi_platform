using ImageMagick;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;

namespace PnpTool.Imaging;

/// <summary>
/// Questions about image files that are answered from the file header, without decoding
/// the pixels. Original: the format and validity helpers of <c>ImagenesGeneral</c>.
/// </summary>
/// <remarks>
/// The original also mapped formats to <c>System.Drawing.Imaging.ImageFormat</c>
/// (<c>ToImageFormat</c>, <c>DevuelveImageFormat</c>); nothing here goes through GDI, so
/// those are not ported.
/// </remarks>
public static class ImageFormats
{
    /// <summary>The format of the file, read from its contents. Original: <c>DevuelveMagickFormatImagen</c>.</summary>
    public static MagickFormat FormatOf(string path) => new MagickImageInfo(path).Format;

    /// <summary>
    /// The format a file extension stands for: "png" or ".PNG" give <see cref="MagickFormat.Png"/>.
    /// An empty extension means JPEG. Throws <see cref="ArgumentException"/> for an extension
    /// that is not the name of a format. Original: <c>DevuelveMagickFormatImagen_Extension</c>.
    /// </summary>
    public static MagickFormat FormatFromExtension(string extension)
    {
        string name = extension.TrimStart('.');
        if (name == string.Empty)
        {
            return MagickFormat.Jpg;
        }

        name = char.ToUpperInvariant(name[0]) + name[1..].ToLowerInvariant();
        if (name == "Webp")
        {
            name = "WebP";
        }

        return Enum.Parse<MagickFormat>(name);
    }

    /// <summary>Whether the image is stored in CMYK. Original: <c>EsCMYK_ImageMagick</c>, which answered with an error object.</summary>
    public static bool IsCmyk(string path) => new MagickImageInfo(path).ColorSpace == ColorSpace.CMYK;

    /// <summary>
    /// An error when the file cannot be read as an image, with the translated "corrupt image"
    /// message. Original: <c>EsCorrupta</c>.
    /// </summary>
    public static AppError CheckReadable(string path)
    {
        var error = new AppError();
        try
        {
            _ = new MagickImageInfo(path);
        }
        catch (Exception exception) when (exception is MagickException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            error.Message = path + " - " + StringResources.Get("Imagen_Corrupta");
            error.Kind = ErrorKind.Error;
        }

        return error;
    }

    /// <summary>
    /// Whether the file is a still image the program can use: readable, and not a video or a
    /// PDF. Original: <c>EsImagen</c>.
    /// </summary>
    public static bool IsImage(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        try
        {
            return new MagickImageInfo(path).Format is not (MagickFormat.Avi or MagickFormat.Mp4 or MagickFormat.Mpeg or MagickFormat.Mpg or MagickFormat.Pdf);
        }
        catch (Exception exception) when (exception is MagickException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }
}
