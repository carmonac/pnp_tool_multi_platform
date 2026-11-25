using PnpTool.Core.Cards;

namespace PnpTool.Core.Infrastructure;

/// <summary>
/// Reads the properties of an image file (size, colour space, profile, EXIF rotation).
/// Implemented by the imaging library; Core only declares what the models need.
/// Original: <c>ImagenesGeneral.ObtenerPropiedadesImagen</c>.
/// </summary>
public interface IImageInspector
{
    /// <summary>Fills a card face from an image file. A file that cannot be read gives a face marked corrupt.</summary>
    CardFace GetImageProperties(string path);
}

/// <summary>Holds the image inspector in use.</summary>
public static class ImageInspector
{
    private static IImageInspector? _current;

    /// <summary>Set once at start-up by the imaging library.</summary>
    public static IImageInspector Current
    {
        get => _current ?? throw new InvalidOperationException("No image inspector has been registered.");
        set => _current = value;
    }

    public static bool IsRegistered => _current is not null;
}
