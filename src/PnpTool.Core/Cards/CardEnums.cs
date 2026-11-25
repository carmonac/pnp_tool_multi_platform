using System.Xml.Serialization;

namespace PnpTool.Core.Cards;

/// <summary>Original: <c>CaraDeCarta.EnumTipoCara</c>.</summary>
[XmlType("EnumTipoCara")]
public enum FaceType
{
    [XmlEnum("Frontal")] Front,
    [XmlEnum("Trasera")] Back,
    [XmlEnum("Ambas")] Both,
    [XmlEnum("Ninguna")] None,
}

/// <summary>What a card face holds. Original: <c>ImagenesGeneral.TiposImagenes</c>.</summary>
[XmlType("TiposImagenes")]
public enum ImageKind
{
    /// <summary>An image file.</summary>
    [XmlEnum("normal")] Normal,

    /// <summary>A plain colour; the face's path holds the colour as "A_R_G_B".</summary>
    [XmlEnum("blanca")] Blank,

    /// <summary>Nothing.</summary>
    [XmlEnum("Vacia")] Empty,

    Error,
}

/// <summary>Effect applied to card images. Original: <c>ImagenesGeneral.TiposEfectos</c>.</summary>
[XmlType("TiposEfectos")]
public enum ImageEffectType
{
    [XmlEnum("Sin_Efecto")] None,
    [XmlEnum("Esquinas_Redondeadas")] RoundedCorners,
    [XmlEnum("Espejo")] Mirror,
    Blur,
    Autolevel,
    [XmlEnum("Contraste")] Contrast,
    [XmlEnum("TokenCircular")] CircularToken,
    [XmlEnum("TokenHexagono")] HexagonToken,
    [XmlEnum("TokenPentagono")] PentagonToken,
    [XmlEnum("CrearContorno")] CreateOutline,
}
