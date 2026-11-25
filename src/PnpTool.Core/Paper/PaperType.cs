using System.Xml.Serialization;

namespace PnpTool.Core.Paper;

/// <summary>Original: <c>Papel.TiposPapel</c>.</summary>
/// <remarks>The Soriano sizes stay so that existing files that name them still load.</remarks>
[XmlType("TiposPapel")]
public enum PaperType
{
    A4,
    A5,
    A3,
    A2,
    A1,
    A0,
    [XmlEnum("Carta")] Letter,
    Legal,
    [XmlEnum("Personalizado")] Custom,
    [XmlEnum("SorianoPortadas")] SorianoCovers,
    [XmlEnum("SorianoTickets330_480")] SorianoTickets330x480,
    [XmlEnum("SorianoTickets320_450")] SorianoTickets320x450,
    [XmlEnum("SorianoTickets225_320")] SorianoTickets225x320,
}

/// <summary>Paper stock, used to estimate book spine thickness. Original: <c>Papel.TiposVolumenPapel</c>.</summary>
[XmlType("TiposVolumenPapel")]
public enum PaperWeightType
{
    [XmlEnum("A4_80")] Plain80,
    [XmlEnum("crema_120")] Cream120,
    [XmlEnum("blanco_100")] White100,
    [XmlEnum("estucado_130")] Coated130,
    [XmlEnum("estucado_150")] Coated150,
    [XmlEnum("estucado_170")] Coated170,
}
