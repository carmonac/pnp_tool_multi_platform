using ImageMagick;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.Imaging;

/// <summary>
/// Reads what the program needs to know about an image file before using it.
/// Original: <c>ImagenesGeneral.ObtenerPropiedadesImagen</c>, <c>GetColorProfile</c> and
/// <c>PropiedadesRotacion.ComprobarRotacionExif_V3</c>.
/// </summary>
public sealed class MagickImageInspector : IImageInspector
{
    /// <summary>Makes this the inspector the model classes use. Call once when the program starts.</summary>
    public static void Register() => ImageInspector.Current = new MagickImageInspector();

    public CardFace GetImageProperties(string path) => GetImageProperties(path, null);

    /// <summary>
    /// Fills <paramref name="face"/> (or a new face) from the file: paths, size on disk, colour
    /// space and profile, and the size it prints at given its resolution. An image whose EXIF
    /// orientation turns it a quarter gets width and height swapped. A file that cannot be
    /// read gives a face marked corrupt with only its original path set.
    /// </summary>
    public static CardFace GetImageProperties(string path, CardFace? face)
    {
        face ??= new CardFace();
        if (ImageFormats.CheckReadable(path).HasError)
        {
            face.OriginalPath = path;
            face.IsCorrupt = true;
            return face;
        }

        face.IsCorrupt = false;
        var info = new MagickImageInfo(path);
        face.IsCmyk = info.ColorSpace == ColorSpace.CMYK;
        face.SizeOnDiskMb = new FileInfo(path).Length / 1048576.0;
        face.ColorProfile = ColorProfileOf(path) as ColorProfile;
        face.Path = path;
        face.OriginalPath = path;
        face.Size = ImageDensity.PrintedSize(info);
        if (ExifRotation(info.Orientation).Angle is 90 or 270)
        {
            (face.Size.X, face.Size.Y) = (face.Size.Y, face.Size.X);
        }

        return face;
    }

    /// <summary>The ICC profile embedded in the file, or null. Original: <c>GetColorProfile</c>.</summary>
    /// <remarks>The original forced a garbage collection after each call; disposing the image is enough.</remarks>
    public static IColorProfile? ColorProfileOf(string path)
    {
        using var image = new MagickImage(path);
        image.ResetPage();
        return image.GetColorProfile();
    }

    /// <summary>How the image must be turned to stand upright, from its EXIF orientation.</summary>
    public static RotationProperties ExifRotation(Orientation orientation) =>
        RotationProperties.FromExifOrientation((int)orientation);

    /// <summary>The same for a file; no rotation when the path is empty or the file is missing.</summary>
    public static RotationProperties ExifRotation(string? path) =>
        string.IsNullOrEmpty(path) || !File.Exists(path)
            ? new RotationProperties()
            : ExifRotation(new MagickImageInfo(path).Orientation);
}
