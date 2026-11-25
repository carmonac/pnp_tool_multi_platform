using System.Xml.Serialization;
using PnpTool.Core.Xml;

namespace PnpTool.Core;

// Enums that the original kept inside the static class TiposGenerales, plus TiposMaquetacion.
// Member order is unchanged, and every renamed member carries its original name in [XmlEnum]
// because that name is what project and style files store.

/// <summary>Original: <c>TiposGenerales.TiposProyectos</c>.</summary>
[XmlType("TiposProyectos")]
public enum ProjectType
{
    [XmlEnum("Carta")] Card,
    [XmlEnum("Libro")] Book,
    Pocketmod,
    Pocketfold,
    Gutterfold,
    Tabletop,
    Ticket,
    Dice7Pixel,
    [XmlEnum("EstilosAvanzados")] AdvancedStyles,
    [XmlEnum("BarajaEspanola")] SpanishDeck,
    [XmlEnum("Libreto")] Booklet,
    Papermini,
}

/// <summary>Original: <c>TiposGenerales.GruposConfiguracion</c>.</summary>
[XmlType("GruposConfiguracion")]
public enum SettingsGroup
{
    [XmlEnum("Cartas")] Cards,
    [XmlEnum("Libros")] Books,
    Tokens,
    Tabletop,
    [XmlEnum("Personalizado")] Custom,
    Soriano,
    [XmlEnum("EstilosPersonalizados")] CustomStyles,
    [XmlEnum("Dados")] Dice,
    Tickets,
    Dice7Pixel,
    [XmlEnum("EstilosAvanzados")] AdvancedStyles,
    [XmlEnum("BarajaEspanola")] SpanishDeck,
    Tipia,
}

/// <summary>How card backs are supplied. Original: <c>TiposGenerales.ImagenesTraseras</c>.</summary>
[XmlType("ImagenesTraseras")]
public enum BackImageMode
{
    [XmlEnum("Sin_trasera"), OriginalName("SinTrasera")] NoBack,
    [XmlEnum("Trasera_igual"), OriginalName("TraseraIgual")] SameBack,
    [XmlEnum("Trasera_diferente"), OriginalName("TraseraDiferente")] DifferentBack,
    [XmlEnum("Ninguna")] None,
    [XmlEnum("SoloUna")] OnlyOne,
    [XmlEnum("Patron")] Pattern,
}

/// <summary>Measurement system chosen by the user. Original: <c>TiposGenerales.TiposUnidades</c>.</summary>
[XmlType("TiposUnidades")]
public enum UnitType
{
    [XmlEnum("mm")] Millimeter,
    [XmlEnum("inch")] Inch,
    [XmlEnum("pt")] Point,
}

/// <summary>
/// Answer of the add-images dialog: how many fronts and backs were chosen.
/// Original: <c>TiposGenerales.TiposSalidasAnadirImagenes</c>.
/// </summary>
[XmlType("TiposSalidasAnadirImagenes")]
public enum AddImagesResult
{
    [XmlEnum("FNT1")] ManyFrontsOneBack,
    [XmlEnum("FNTN")] ManyFrontsManyBacks,
    [XmlEnum("F1TN")] OneFrontManyBacks,
    [XmlEnum("F1T1")] OneFrontOneBack,
    Ask,
}

/// <summary>Original: <c>TiposGenerales.TiposResolucionImagen</c>.</summary>
[XmlType("TiposResolucionImagen")]
public enum ImageResolutionType
{
    SmallBitmap,
    MediumBitmap,
    LargeBitmap,
    ExtraLargeBitmap,
    Original,
}

/// <summary>Original: <c>TiposGenerales.TiposActualizaciones</c>.</summary>
[XmlType("TiposActualizaciones")]
public enum UpdateChannel
{
    [XmlEnum("Mayor")] Major,
    [XmlEnum("Menor")] Minor,
    Tester,
    [XmlEnum("Ninguna")] None,
}

/// <summary>Original: <c>TiposGenerales.OpcionesSalidaCambioCara</c>.</summary>
[XmlType("OpcionesSalidaCambioCara")]
public enum FaceChangeChoice
{
    [XmlEnum("frontales")] Fronts,
    [XmlEnum("traseras")] Backs,
    [XmlEnum("agrupar")] Group,
    [XmlEnum("igualar")] Match,
    [XmlEnum("cancelar")] Cancel,
}

/// <summary>Original: <c>TiposGenerales.SalidasMenuArchivo</c>.</summary>
[XmlType("SalidasMenuArchivo")]
public enum FileMenuResult
{
    [XmlEnum("Nuevo")] New,
    [XmlEnum("Guardar")] Save,
    [XmlEnum("GuardarComo")] SaveAs,
    [XmlEnum("Crear")] Create,
    [XmlEnum("Cerrar")] Close,
    [XmlEnum("Abrir")] Open,
    [XmlEnum("Configuracion")] Settings,
    [XmlEnum("Cuenta")] Account,
}

/// <summary>Original: <c>TiposGenerales.TiposFondoCarta</c>.</summary>
[XmlType("TiposFondoCarta")]
public enum CardBackgroundType
{
    [XmlEnum("ColorSolido")] SolidColor,
    [XmlEnum("ColorUnico")] SingleColor,
}

/// <summary>Original: <c>TiposGenerales.EfectosMarcoExterior</c>.</summary>
[XmlType("EfectosMarcoExterior")]
public enum OuterFrameEffect
{
    [XmlEnum("ColorSolido")] SolidColor,
    [XmlEnum("Espejo")] Mirror,
}

/// <summary>Original: <c>TiposMaquetacion</c>.</summary>
[XmlType("TiposMaquetacion")]
public enum LayoutType
{
    [XmlEnum("Estandar")] Standard,
    [XmlEnum("Apilada")] Stacked,
    [XmlEnum("PorFilas")] ByRows,
}
