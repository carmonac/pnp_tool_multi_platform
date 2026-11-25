using System.Xml.Serialization;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.Core.Projects;

/// <summary>Original: <c>LineasDeCorte.TiposLineaCorte</c>.</summary>
[XmlType("TiposLineaCorte")]
public enum CutLineType
{
    [XmlEnum("Sin_linea")] None,
    Normal,
    [XmlEnum("Cruz")] Cross,
    [XmlEnum("Completa")] Full,
    [XmlEnum("Mezcla")] Mixed,
    [XmlEnum("Punto")] Dot,
}

/// <summary>Cut-line options of one card face. Original: <c>LineasDeCorte.ConfiguracionLineasDeCorte</c>.</summary>
[XmlType("ConfiguracionLineasDeCorte")]
public sealed class CutLineSettings
{
    [XmlElement("Tipolinea")]
    public CutLineType LineType { get; set; } = CutLineType.None;

    /// <summary>Colour as "A_R_G_B".</summary>
    public string Color { get; set; } = AppSettings.ColorBlack;

    [XmlElement("Grosor")]
    public Unit Thickness { get; set; } = Unit.Zero;

    [XmlElement("Separacion")]
    public int Gap { get; set; }

    [XmlElement("LineaCorteCruzHastaBorde")]
    public bool CrossLinesToEdge { get; set; }

    /// <summary>Original: <c>Copiar</c>.</summary>
    public void CopyFrom(CutLineSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        LineType = source.LineType;
        Color = source.Color;
        Thickness = source.Thickness;
        Gap = source.Gap;
        CrossLinesToEdge = source.CrossLinesToEdge;
    }

    /// <summary>Original: <c>EsIgual</c>.</summary>
    public bool IsEqualTo(CutLineSettings? other) =>
        other != null
        && LineType == other.LineType
        && Color == other.Color
        && Thickness.EqualsInMillimeters(other.Thickness)
        && Gap == other.Gap
        && CrossLinesToEdge == other.CrossLinesToEdge;
}
