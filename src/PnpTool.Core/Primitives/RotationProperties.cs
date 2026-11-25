using System.Xml.Serialization;

namespace PnpTool.Core.Primitives;

/// <summary>Rotation and flips applied to an image. Original: <c>PropiedadesRotacion</c>.</summary>
[XmlType("PropiedadesRotacion")]
public class RotationProperties
{
    /// <summary>Clockwise angle in degrees, normalised to 0–359 by the methods of this class.</summary>
    [XmlElement("Angulo_Rotacion")]
    public int Angle { get; set; }

    [XmlElement("Voltear_Vertical")]
    public bool FlipVertical { get; set; }

    [XmlElement("Voltear_Horizontal")]
    public bool FlipHorizontal { get; set; }

    public RotationProperties()
    {
    }

    public RotationProperties(int angle, bool flipHorizontal, bool flipVertical)
    {
        Angle = Normalize(angle);
        FlipHorizontal = flipHorizontal;
        FlipVertical = flipVertical;
    }

    /// <summary>Original: <c>Copiar</c>.</summary>
    public void CopyFrom(RotationProperties other)
    {
        Angle = other.Angle;
        FlipVertical = other.FlipVertical;
        FlipHorizontal = other.FlipHorizontal;
    }

    /// <summary>Adds the other rotation and toggles the flips it has set. Original: <c>MezclaDatosRotacion</c>.</summary>
    public void Combine(RotationProperties other)
    {
        AddAngle(other.Angle);
        ToggleFlips(other.FlipVertical, other.FlipHorizontal);
    }

    /// <summary>Original: <c>SumaDatosRotacionSoloAngulo</c>.</summary>
    public void AddAngle(int angle) => Angle = Normalize(Angle + angle);

    /// <summary>Original: <c>MezclaDatosRotacionVVVH</c>.</summary>
    public void ToggleFlips(bool flipVertical, bool flipHorizontal)
    {
        FlipHorizontal ^= flipHorizontal;
        FlipVertical ^= flipVertical;
    }

    /// <summary>Original: <c>EsIgual</c>.</summary>
    public bool IsEqualTo(RotationProperties other) =>
        Angle == other.Angle && FlipVertical == other.FlipVertical && FlipHorizontal == other.FlipHorizontal;

    /// <summary>
    /// Rotation needed to show an image upright given its EXIF orientation tag (1–8).
    /// Original: <c>ComprobarRotacionExif_V3</c>; the table is copied as is, including
    /// orientations 5 and 7 mapping to the same result.
    /// </summary>
    public static RotationProperties FromExifOrientation(int orientation) => orientation switch
    {
        1 => new RotationProperties(0, flipHorizontal: false, flipVertical: false),   // TopLeft
        2 => new RotationProperties(0, flipHorizontal: true, flipVertical: false),    // TopRight
        3 => new RotationProperties(180, flipHorizontal: false, flipVertical: false), // BottomRight
        4 => new RotationProperties(0, flipHorizontal: false, flipVertical: true),    // BottomLeft
        5 => new RotationProperties(90, flipHorizontal: true, flipVertical: false),   // LeftTop
        6 => new RotationProperties(90, flipHorizontal: false, flipVertical: false),  // RightTop
        7 => new RotationProperties(90, flipHorizontal: true, flipVertical: false),   // RightBottom
        8 => new RotationProperties(270, flipHorizontal: false, flipVertical: false), // LeftBottom
        _ => new RotationProperties(0, flipHorizontal: false, flipVertical: false),
    };

    private static int Normalize(int angle) => ((angle % 360) + 360) % 360;
}
