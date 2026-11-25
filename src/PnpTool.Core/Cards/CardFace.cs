using System.Xml.Serialization;
using ImageMagick;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.Core.Cards;

/// <summary>One side of a card. Original: <c>CaraDeCarta</c>.</summary>
[XmlType("CaraDeCarta")]
public class CardFace
{
    [XmlElement("TipoCara")]
    public FaceType FaceType { get; set; }

    /// <summary>
    /// File currently used for the face (may be a processed temporary copy), or the
    /// colour text when <see cref="ImageKind"/> is <see cref="Cards.ImageKind.Blank"/>.
    /// </summary>
    [XmlElement("Ruta")]
    public string Path { get; set; }

    /// <summary>File the user chose.</summary>
    [XmlElement("RutaOriginal")]
    public string OriginalPath { get; set; }

    [XmlElement("ColorFondo")]
    public string BackgroundColor { get; set; }

    [XmlElement("PropiedadesRotacion")]
    public RotationProperties Rotation { get; set; }

    [XmlElement("Tamano")]
    public UnitPoint Size { get; set; }

    [XmlElement("TipoImagen")]
    public ImageKind ImageKind { get; set; }

    [XmlElement("Seleccionada")]
    public bool Selected { get; set; }

    [XmlElement("Es_CMYK")]
    public bool IsCmyk { get; set; }

    [XmlElement("EsCorrupta")]
    public bool IsCorrupt { get; set; }

    /// <summary>Original: <c>PerfilColor</c>.</summary>
    [XmlIgnore]
    public ColorProfile? ColorProfile { get; set; }

    /// <summary>File size in megabytes. Original: <c>TamanoCaraEnDisco</c>.</summary>
    [XmlIgnore]
    public double SizeOnDiskMb { get; set; }

    public CardFace()
    {
        Size = new UnitPoint();
        Path = string.Empty;
        OriginalPath = string.Empty;
        Rotation = new RotationProperties();
        BackgroundColor = string.Empty;
    }

    public CardFace(string path, FaceType faceType, ImageKind imageKind)
        : this()
    {
        FaceType = faceType;
        ImageKind = imageKind;
        Path = path;
        OriginalPath = path;
    }

    /// <summary>Original: <c>Copiar</c>.</summary>
    public void CopyFrom(CardFace source)
    {
        ArgumentNullException.ThrowIfNull(source);
        FaceType = source.FaceType;
        Path = source.Path;
        OriginalPath = source.OriginalPath;
        BackgroundColor = source.BackgroundColor;
        Rotation = new RotationProperties();
        Rotation.CopyFrom(source.Rotation);
        Size.X = source.Size.X;
        Size.Y = source.Size.Y;
        ImageKind = source.ImageKind;
        Selected = source.Selected;
        IsCmyk = source.IsCmyk;
        IsCorrupt = source.IsCorrupt;
        SizeOnDiskMb = source.SizeOnDiskMb;
        ColorProfile = IccProfiles.Copy(source.ColorProfile);
    }

    /// <summary>Turns the face into a blank white back. Original: <c>ResetCaraCartaTraseraIgual</c>.</summary>
    public void ResetAsSharedBack()
    {
        FaceType = FaceType.Back;
        ImageKind = ImageKind.Blank;
        Size.X = 0;
        Size.Y = 0;
        SizeOnDiskMb = 0.0;
        Path = AppSettings.ColorWhite;
        BackgroundColor = string.Empty;
        Rotation.Angle = 0;
        Rotation.FlipVertical = false;
        Rotation.FlipHorizontal = false;
        IsCorrupt = false;
        IsCmyk = false;
        ColorProfile = null;
    }

    /// <summary>
    /// Applies a further rotation to an image face, swapping width and height for quarter
    /// turns. Original: <c>CambiaDatosRotacion</c>.
    /// </summary>
    public void ApplyRotation(RotationProperties change)
    {
        if (ImageKind != ImageKind.Normal)
        {
            return;
        }

        Rotation.AddAngle(change.Angle);
        int normalized = ((change.Angle % 360) + 360) % 360;
        if (normalized is 90 or 270)
        {
            (Size.X, Size.Y) = (Size.Y, Size.X);
        }

        if (change.FlipVertical)
        {
            Rotation.FlipVertical = !Rotation.FlipVertical;
        }

        if (change.FlipHorizontal)
        {
            Rotation.FlipHorizontal = !Rotation.FlipHorizontal;
        }
    }
}

/// <summary>Helpers for ICC colour profiles.</summary>
public static class IccProfiles
{
    /// <summary>Independent copy of a profile. Original: <c>ImagenesGeneral.CopiarPerfilColor</c>.</summary>
    public static ColorProfile? Copy(ColorProfile? profile) =>
        profile == null ? null : new ColorProfile(profile.ToByteArray());

    /// <summary>Loads a profile file, or returns null when the path is empty or the file is missing.</summary>
    public static ColorProfile? TryLoad(string? path) =>
        !string.IsNullOrEmpty(path) && File.Exists(path) ? new ColorProfile(path) : null;

    /// <summary>
    /// The given profile path, or the program's default for the colour space when none is given.
    /// Original: <c>GestionDelColor.DevuelvePerfilColor</c>. The original recursed forever when
    /// no default was configured; here that case returns an empty path.
    /// </summary>
    public static string PathOrDefault(string? path, Projects.ProjectColorSpace colorSpace)
    {
        if (!string.IsNullOrEmpty(path))
        {
            return path;
        }

        return colorSpace == Projects.ProjectColorSpace.RGB ? AppSettings.RgbIccPath : AppSettings.CmykIccPath;
    }
}
