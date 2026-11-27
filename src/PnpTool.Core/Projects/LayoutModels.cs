using System.Xml.Serialization;
using PnpTool.Core.Primitives;
using PaperSheet = PnpTool.Core.Paper.Paper;

namespace PnpTool.Core.Projects;

/// <summary>Card size, spacing and bleed on the sheet. Original: <c>Cuadricula</c>.</summary>
[XmlType("Cuadricula")]
public class Grid
{
    [XmlElement("TamanoCarta")]
    public UnitPoint CardSize { get; set; } = new();

    [XmlElement("Separacion")]
    public UnitPoint Spacing { get; set; } = new();

    [XmlElement("Sangrado")]
    public Unit Bleed { get; set; } = Unit.Zero;

    [XmlElement("CandadoRatioImagen")]
    public bool LockImageRatio { get; set; }

    /// <summary>Original: <c>Copiar</c>.</summary>
    public void CopyFrom(Grid source)
    {
        ArgumentNullException.ThrowIfNull(source);
        CardSize = new UnitPoint(source.CardSize);
        Spacing = new UnitPoint(source.Spacing);
        Bleed = Unit.FromPoint(source.Bleed.Point);
        LockImageRatio = source.LockImageRatio;
    }

    /// <summary>Original: <c>EsIgual</c>. As in the original, only the horizontal spacing is compared.</summary>
    public bool IsEqualTo(Grid? other) =>
        other != null
        && CardSize.EqualsInMillimeters(other.CardSize)
        && Spacing.X.EqualsInMillimeters(other.Spacing.X)
        && Bleed.EqualsInMillimeters(other.Bleed);
}

/// <summary>Rows, columns, paper and sides. Original: <c>DisposicionImpresion</c>.</summary>
[XmlType("DisposicionImpresion")]
public class PrintLayout
{
    [XmlElement("Filas")]
    public int Rows { get; set; }

    [XmlElement("Columnas")]
    public int Columns { get; set; }

    /// <summary>Cards per sheet. Original: <c>TamanoMatriz</c>.</summary>
    [XmlIgnore]
    public int CardsPerSheet { get; private set; }

    [XmlElement("CalcularAutomaticamente")]
    public bool CalculateAutomatically { get; set; }

    [XmlElement("Papel")]
    public PaperSheet Paper { get; set; } = new();

    [XmlElement("DobleCara")]
    public bool DoubleSided { get; set; }

    [XmlElement("ImagenTrasera")]
    public BackImageMode BackImage { get; set; } = BackImageMode.NoBack;

    public bool FoldBottom { get; set; } = true;

    /// <summary>Sets cards per sheet to rows × columns. Original: <c>FijaMatriz</c>.</summary>
    public void UpdateCardsPerSheet() => CardsPerSheet = Rows * Columns;

    /// <summary>Sets cards per sheet directly, for advanced templates. Original: <c>FijaMatrizAvanzada</c>.</summary>
    public void SetCardsPerSheet(int count) => CardsPerSheet = count;

    /// <summary>Original: <c>Copiar</c>.</summary>
    public void CopyFrom(PrintLayout source)
    {
        Rows = source.Rows;
        Columns = source.Columns;
        CardsPerSheet = source.CardsPerSheet;
        CalculateAutomatically = source.CalculateAutomatically;
        Paper.CopyFrom(source.Paper);
        DoubleSided = source.DoubleSided;
        BackImage = source.BackImage;
        FoldBottom = source.FoldBottom;
    }

    /// <summary>Original: <c>EsIgual</c>. As in the original, sides and back-image mode are not compared.</summary>
    public bool IsEqualTo(PrintLayout? other) =>
        other != null
        && Rows == other.Rows
        && Columns == other.Columns
        && CalculateAutomatically == other.CalculateAutomatically
        && Paper.IsEqualTo(other.Paper)
        && FoldBottom == other.FoldBottom;
}

/// <summary>Original: <c>EncabezadoPie.PosicionNumeroPagina</c>.</summary>
[XmlType("PosicionNumeroPagina")]
public enum PageNumberPosition
{
    [XmlEnum("Izquierda")] Left,
    [XmlEnum("Centro")] Center,
    [XmlEnum("Derecha")] Right,
}

/// <summary>Calibration marks, signature and page number. Original: <c>EncabezadoPie</c>.</summary>
[XmlType("EncabezadoPie")]
public class HeaderFooter
{
    [XmlElement("MostrarCalibracionImagen")]
    public bool ShowCalibrationImage { get; set; }

    [XmlElement("TamanoImagenCalibracion")]
    public UnitPoint CalibrationImageSize { get; set; } = new();

    [XmlElement("MostrarCalibracion")]
    public bool ShowCalibrationMark { get; set; }

    [XmlElement("TamanoMarcaCalibracion")]
    public Unit CalibrationMarkSize { get; set; } = Unit.Zero;

    [XmlElement("PosicionMarcaCalibracion")]
    public UnitPoint CalibrationMarkPosition { get; set; } = new();

    [XmlElement("TextoFirma")]
    public string SignatureText { get; set; } = string.Empty;

    [XmlElement("MostrarNumeroPagina")]
    public bool ShowPageNumber { get; set; }

    [XmlElement("NumeroPaginaInicial")]
    public int FirstPageNumber { get; set; }

    [XmlElement("PosicionNumeroPaginaProyecto")]
    public PageNumberPosition PageNumberPosition { get; set; } = PageNumberPosition.Center;

    /// <summary>
    /// Original: <c>EsIgual</c>, which compared the two size and position pairs by reference;
    /// here by their values.
    /// </summary>
    public bool IsEqualTo(HeaderFooter? other) =>
        other != null
        && ShowCalibrationImage == other.ShowCalibrationImage
        && CalibrationImageSize.EqualsInMillimeters(other.CalibrationImageSize)
        && ShowCalibrationMark == other.ShowCalibrationMark
        && CalibrationMarkSize.EqualsInMillimeters(other.CalibrationMarkSize)
        && CalibrationMarkPosition.EqualsInMillimeters(other.CalibrationMarkPosition)
        && SignatureText == other.SignatureText
        && ShowPageNumber == other.ShowPageNumber
        && FirstPageNumber == other.FirstPageNumber
        && PageNumberPosition == other.PageNumberPosition;

    /// <summary>
    /// A copy with its own size and position. The original's <c>Clone</c> shared them with
    /// the settings copied, so typing in the header window changed the project even when the
    /// window was cancelled.
    /// </summary>
    public HeaderFooter Clone() => new()
    {
        ShowCalibrationImage = ShowCalibrationImage,
        CalibrationImageSize = new UnitPoint(CalibrationImageSize),
        ShowCalibrationMark = ShowCalibrationMark,
        CalibrationMarkPosition = new UnitPoint(CalibrationMarkPosition),
        CalibrationMarkSize = CalibrationMarkSize,
        SignatureText = SignatureText,
        ShowPageNumber = ShowPageNumber,
        FirstPageNumber = FirstPageNumber,
        PageNumberPosition = PageNumberPosition,
    };
}

/// <summary>Which style group the project belongs to. Original: <c>GrupoProyecto</c>.</summary>
[XmlType("GrupoProyecto")]
public class ProjectGroup
{
    [XmlIgnore]
    public bool PDFX { get; set; }

    [XmlElement("TipoProyecto")]
    public ProjectType ProjectType { get; set; } = ProjectType.Card;

    [XmlElement("IndiceComboGrupo")]
    public SettingsGroup Group { get; set; } = SettingsGroup.Cards;

    [XmlElement("IndiceComboSubgrupo")]
    public int SubgroupIndex { get; set; }

    /// <summary>Original: <c>Copiar</c>.</summary>
    public void CopyFrom(ProjectGroup source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ProjectType = source.ProjectType;
        Group = source.Group;
        SubgroupIndex = source.SubgroupIndex;
        PDFX = source.PDFX;
    }
}

/// <summary>Original: <c>MarcaDeAgua.TiposMarcaAgua</c>.</summary>
[XmlType("TiposMarcaAgua")]
public enum WatermarkType
{
    [XmlEnum("SinMarcaAgua")] None,
    [XmlEnum("AImagen")] OnImage,
    [XmlEnum("APagina")] OnPage,
}

/// <summary>Original: <c>MarcaDeAgua</c>.</summary>
[XmlType("MarcaDeAgua")]
public class Watermark
{
    [XmlElement("Tipo")]
    public WatermarkType Type { get; set; } = WatermarkType.None;

    [XmlElement("Ruta")]
    public string Path { get; set; } = string.Empty;

    /// <summary>Original: <c>Copiar</c>.</summary>
    public void CopyFrom(Watermark source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Path = source.Path;
        Type = source.Type;
    }

    public bool IsEqualTo(Watermark other) => Type == other.Type && Path == other.Path;
}

/// <summary>Values given on the command line. Original: <c>ParametrosDeEntrada</c>.</summary>
[XmlType("ParametrosDeEntrada")]
public class InputParameters
{
    /// <summary>True when command-line parameters were given.</summary>
    [XmlElement("Hay")]
    public bool Present { get; set; }

    [XmlElement("NombreProyectoInput")]
    public string InputProject { get; set; } = string.Empty;

    [XmlElement("NombreProyectoOutput")]
    public string OutputProject { get; set; } = string.Empty;

    [XmlElement("FicheroEstilo")]
    public string StyleFile { get; set; } = string.Empty;

    [XmlElement("ImagenTrasera")]
    public string BackImage { get; set; } = string.Empty;

    [XmlElement("PDFSalida")]
    public string OutputPdf { get; set; } = string.Empty;

    [XmlElement("CarpetaImagenes")]
    public string ImagesFolder { get; set; } = string.Empty;

    /// <summary>Original: <c>Copiar</c>.</summary>
    public void CopyFrom(InputParameters source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Present = source.Present;
        InputProject = source.InputProject;
        OutputProject = source.OutputProject;
        StyleFile = source.StyleFile;
        BackImage = source.BackImage;
        OutputPdf = source.OutputPdf;
        ImagesFolder = source.ImagesFolder;
    }
}

/// <summary>Unit of a font size as stored in files; member names match <c>System.Drawing.GraphicsUnit</c>.</summary>
public enum FontSizeUnit
{
    World,
    Display,
    Pixel,
    Point,
    Inch,
    Document,
    Millimeter,
}

/// <summary>Font style flags as stored in files; member names match <c>System.Drawing.FontStyle</c>.</summary>
[Flags]
public enum FontStyleFlags
{
    Regular = 0,
    Bold = 1,
    Italic = 2,
    Underline = 4,
    Strikeout = 8,
}

/// <summary>
/// Font used for page numbers, signature and ticket numbering. Original: <c>FuentePersonalizada</c>,
/// a serialisable copy of a <c>System.Drawing.Font</c>.
/// </summary>
[XmlType("FuentePersonalizada")]
public class CustomFont
{
    public string FontFamily { get; set; } = "Arial";

    [XmlElement("GraphicsUnit")]
    public FontSizeUnit Unit { get; set; } = FontSizeUnit.Point;

    public float Size { get; set; } = 8f;

    public FontStyleFlags Style { get; set; } = FontStyleFlags.Regular;

    /// <summary>Line height in pixels at 96 dpi as Windows reported it; 13 for the default Arial 8 pt.</summary>
    public int Height { get; set; } = 13;

    /// <summary>Original: <c>Copiar</c>.</summary>
    public void CopyFrom(CustomFont source)
    {
        FontFamily = source.FontFamily;
        Unit = source.Unit;
        Size = source.Size;
        Style = source.Style;
        Height = source.Height;
    }
}
