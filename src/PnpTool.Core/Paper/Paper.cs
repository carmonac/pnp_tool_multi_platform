using System.Xml.Serialization;
using PnpTool.Core.Primitives;
using PnpTool.Core.Xml;

namespace PnpTool.Core.Paper;

/// <summary>A sheet of paper: size, named format and orientation. Original: <c>Papel</c>.</summary>
[XmlType("Papel")]
public class Paper
{
    [XmlElement("Tamano")]
    public UnitPoint Size { get; set; }

    [XmlElement("NombrePapel")]
    public PaperType Type { get; set; }

    /// <summary>True for landscape.</summary>
    [XmlElement("Horizontal")]
    public bool Landscape { get; set; }

    public Paper()
    {
        Size = new UnitPoint();
        Type = PaperType.Custom;
        Landscape = false;
    }

    /// <summary>Original: <c>Copiar</c>.</summary>
    public void CopyFrom(Paper other)
    {
        Size = new UnitPoint(other.Size);
        Landscape = other.Landscape;
        Type = other.Type;
    }

    /// <summary>Original: <c>EsIgual</c>.</summary>
    public bool IsEqualTo(Paper other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Size.EqualsInMillimeters(other.Size) && Landscape == other.Landscape && Type == other.Type;
    }

    /// <summary>Portrait size of a named format. Original: <c>DevuelveTamanoPapelXUnit</c>.</summary>
    public static UnitPoint GetSize(PaperType type)
    {
        SizeI size = GetSizeInMillimeters(type);
        return new UnitPoint
        {
            X = Unit.FromMillimeter(size.Width),
            Y = Unit.FromMillimeter(size.Height),
        };
    }

    /// <summary>
    /// Portrait size of a named format in whole millimetres; A4 for <see cref="PaperType.Custom"/>.
    /// Original: <c>DevuelveTamanoPapel</c>.
    /// </summary>
    public static SizeI GetSizeInMillimeters(PaperType type) => type switch
    {
        PaperType.A4 => new SizeI(210, 297),
        PaperType.A5 => new SizeI(148, 210),
        PaperType.A3 => new SizeI(297, 420),
        PaperType.A2 => new SizeI(420, 594),
        PaperType.A1 => new SizeI(594, 841),
        PaperType.A0 => new SizeI(841, 1189),
        PaperType.Letter => new SizeI(216, 279),
        PaperType.Legal => new SizeI(216, 355),
        PaperType.SorianoCovers => new SizeI(500, 330),
        PaperType.SorianoTickets330x480 => new SizeI(330, 480),
        PaperType.SorianoTickets320x450 => new SizeI(320, 450),
        PaperType.SorianoTickets225x320 => new SizeI(225, 320),
        _ => new SizeI(210, 297),
    };

    /// <summary>
    /// The standard format with exactly this portrait size in millimetres, or
    /// <see cref="PaperType.Custom"/>. Original: <c>DevuelveStringPapelSegunTamano</c>,
    /// which returned the name as text.
    /// </summary>
    public static PaperType FindType(int widthMm, int heightMm) => (widthMm, heightMm) switch
    {
        (148, 210) => PaperType.A5,
        (210, 297) => PaperType.A4,
        (297, 420) => PaperType.A3,
        (420, 594) => PaperType.A2,
        (594, 841) => PaperType.A1,
        (841, 1189) => PaperType.A0,
        (216, 279) => PaperType.Letter,
        (216, 355) => PaperType.Legal,
        _ => PaperType.Custom,
    };

    /// <summary>
    /// Describes a PDF page size given in points, for example "210 - 297 A4 " or, for a
    /// landscape page, "210 - 297 A4  + 90º". Original: <c>DevuelvePapelSegunTamano</c>.
    /// </summary>
    public static string Describe(float widthPoints, float heightPoints)
    {
        // The original divides by this single-precision constant rather than 72 / 25.4.
        const float PointsPerMillimeter = 2.8339338f;
        int width = Convert.ToInt32(Math.Round(widthPoints / PointsPerMillimeter, 0));
        int height = Convert.ToInt32(Math.Round(heightPoints / PointsPerMillimeter, 0));
        string rotated = string.Empty;
        if (width > height)
        {
            rotated = " + 90º";
            (width, height) = (height, width);
        }

        string name = FindType(width, height).ToFileName();
        return Unit.FromMillimeter(width).ToDisplayString() + " - " + Unit.FromMillimeter(height).ToDisplayString() + " " + name + " " + rotated;
    }

    /// <summary>
    /// Format stored in files under <paramref name="name"/>; A4 when unknown.
    /// Original: <c>DevuelveTipoPapelSegunString</c>.
    /// </summary>
    public static PaperType ParseType(string name) =>
        XmlEnumNames.TryParseFileName(name, out PaperType type) ? type : PaperType.A4;
}
