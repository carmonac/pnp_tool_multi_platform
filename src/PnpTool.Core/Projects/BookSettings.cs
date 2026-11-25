using System.Xml.Serialization;
using PnpTool.Core.Cards;
using PnpTool.Core.Primitives;

namespace PnpTool.Core.Projects;

/// <summary>Original: <c>MarcasDePerforacion.TiposMarcasperforacion</c>.</summary>
[XmlType("TiposMarcasperforacion")]
public enum PerforationMarkType
{
    [XmlEnum("Ninguna")] None,
    [XmlEnum("Interior")] Inside,
    [XmlEnum("Exterior")] Outside,
}

/// <summary>Hole-punch marks for binding. Original: <c>MarcasDePerforacion</c>.</summary>
[XmlType("MarcasDePerforacion")]
public class PerforationMarks
{
    [XmlElement("Tipo")]
    public PerforationMarkType Type { get; set; } = PerforationMarkType.None;

    [XmlElement("Margen")]
    public Unit Margin { get; set; } = Unit.Zero;

    [XmlElement("Numero")]
    public int Count { get; set; }

    [XmlElement("Tamano")]
    public Unit Size { get; set; } = Unit.Zero;

    /// <summary>Original: <c>Copiar</c>.</summary>
    public void CopyFrom(PerforationMarks source)
    {
        Type = source.Type;
        Margin = source.Margin;
        Count = source.Count;
        Size = source.Size;
    }

    /// <summary>Original: <c>EsIgual</c>. Lengths must match in unit as well as value.</summary>
    public bool IsEqualTo(PerforationMarks other) =>
        Type == other.Type && Margin == other.Margin && Count == other.Count && Size == other.Size;
}

/// <summary>Original: <c>ConfiguracionLibro.ImagenesEnLibro</c>.</summary>
[XmlType("ImagenesEnLibro")]
public enum BookImagePlacement
{
    [XmlEnum("Centradas")] Centered,
    [XmlEnum("AlCentro")] TowardsSpine,
}

/// <summary>Original: <c>ConfiguracionLibro.ImprimirPorBorde</c>.</summary>
[XmlType("ImprimirPorBorde")]
public enum PrintFlipEdge
{
    [XmlEnum("Largo")] Long,
    [XmlEnum("Corto")] Short,
}

/// <summary>Original: <c>ConfiguracionPortadaLibro.TiposLomoLibro</c>.</summary>
[XmlType("TiposLomoLibro")]
public enum BookSpineType
{
    [XmlEnum("Derecha")] Right,
    [XmlEnum("Izquierda")] Left,
}

/// <summary>Book and booklet options. Original: <c>ConfiguracionLibro</c>.</summary>
[XmlType("ConfiguracionLibro")]
public class BookSettings
{
    [XmlElement("MarcasPerforacion")]
    public PerforationMarks PerforationMarks { get; set; } = new();

    [XmlElement("ImagenEnLibro")]
    public BookImagePlacement ImagePlacement { get; set; } = BookImagePlacement.Centered;

    [XmlElement("LomoLibro")]
    public BookSpineType Spine { get; set; } = BookSpineType.Left;

    [XmlElement("ImprimirBorde")]
    public PrintFlipEdge FlipEdge { get; set; } = PrintFlipEdge.Long;

    /// <summary>Original: <c>Copiar</c>.</summary>
    public void CopyFrom(BookSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        PerforationMarks.CopyFrom(source.PerforationMarks);
        ImagePlacement = source.ImagePlacement;
        Spine = source.Spine;
        FlipEdge = source.FlipEdge;
    }

    /// <summary>Original: <c>EsIgual</c>.</summary>
    public bool IsEqualTo(BookSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return PerforationMarks.IsEqualTo(other.PerforationMarks)
            && ImagePlacement == other.ImagePlacement
            && Spine == other.Spine
            && FlipEdge == other.FlipEdge;
    }
}

/// <summary>Options of the book-cover tool. Original: <c>ConfiguracionPortadaLibro</c>.</summary>
[XmlType("ConfiguracionPortadaLibro")]
public class BookCoverSettings
{
    [XmlElement("NombreArchivo")]
    public string FileName { get; set; } = string.Empty;

    [XmlElement("TamanoPapel")]
    public UnitPoint PaperSize { get; set; } = new();

    [XmlElement("NumeroPaginas")]
    public int PageCount { get; set; }

    /// <summary>Paper thickness factor used to compute the spine width.</summary>
    [XmlElement("VolumenPapel")]
    public double PaperVolume { get; set; }

    [XmlElement("TamanoPortada")]
    public UnitPoint CoverSize { get; set; } = new();

    [XmlElement("Ancholomo")]
    public Unit SpineWidth { get; set; }

    [XmlElement("LomoLibro")]
    public BookSpineType Spine { get; set; } = BookSpineType.Left;

    [XmlElement("RutaIzquierda")]
    public string LeftImagePath { get; set; } = string.Empty;

    [XmlElement("RutaLomo")]
    public string SpineImagePath { get; set; } = string.Empty;

    [XmlElement("RutaDerecha")]
    public string RightImagePath { get; set; } = string.Empty;

    [XmlElement("Sangrado")]
    public Unit Bleed { get; set; } = Unit.FromPoint(Unit.FromMillimeter(3.0).Point);

    [XmlElement("Efecto")]
    public ImageEffectType Effect { get; set; } = ImageEffectType.Mirror;

    [XmlElement("PapelHorizontal")]
    public bool PaperLandscape { get; set; } = true;

    [XmlElement("TamanoMarco")]
    public UnitPoint FrameSize { get; set; } = new();
}
