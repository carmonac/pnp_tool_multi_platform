using System.Xml.Serialization;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.Core.Projects;

/// <summary>A position kept both in pixels and in length units. Original: <c>ConfiguracionProyectoTicket.Coordenadas</c>.</summary>
[XmlType("Coordenadas")]
public class TicketCoordinates
{
    [XmlElement("PosicionPixeles")]
    public PointI PixelPosition { get; set; } = new();

    [XmlElement("PosicionMetrico")]
    public UnitPoint MetricPosition { get; set; } = new();
}

/// <summary>Settings of a numbered-ticket project (.pnpt). Original: <c>ConfiguracionProyectoTicket</c>.</summary>
[XmlRoot("ConfiguracionProyectoTicket")]
[XmlType("ConfiguracionProyectoTicket")]
public class TicketProjectSettings
{
    private const int Slots = 10;

    /// <summary>Original: the public field <c>LineasPerforacion_PosicionMetricaX</c>.</summary>
    [XmlArray("LineasPerforacion_PosicionMetricaX")]
    [XmlArrayItem("Coordenadas")]
    public TicketCoordinates[] PerforationLinePositions = new TicketCoordinates[Slots];

    /// <summary>Original: the public field <c>NumeradoresPos</c>.</summary>
    [XmlArray("NumeradoresPos")]
    [XmlArrayItem("Coordenadas")]
    public TicketCoordinates[] NumberPositions = new TicketCoordinates[Slots];

    [XmlElement("TamanoTicket")]
    public UnitPoint TicketSize { get; set; } = new();

    /// <summary>X = columns, Y = rows.</summary>
    [XmlElement("NumeroColumnasFilas")]
    public PointI ColumnsAndRows { get; set; } = new();

    [XmlElement("IndiceComboPapel")]
    public int PaperIndex { get; set; }

    [XmlElement("TamanoPapel")]
    public UnitPoint PaperSize { get; set; } = new();

    [XmlElement("LineasCorteFrontal")]
    public CutLineType FrontCutLines { get; set; } = CutLineType.Normal;

    [XmlElement("GrosorCorteFrontal")]
    public Unit FrontCutLineThickness { get; set; } = 0;

    [XmlElement("ColorCorteFrontal")]
    public string FrontCutLineColor { get; set; } = AppSettings.ColorBlack;

    [XmlElement("TipoRutaImagenes")]
    public ImagePathMode ImagePathMode { get; set; }

    [XmlElement("FactorEscala")]
    public int ScaleFactor { get; set; } = 100;

    [XmlElement("RutaImagen")]
    public string ImagePath { get; set; } = string.Empty;

    [XmlElement("TicketPropiedadesRotacion")]
    public RotationProperties Rotation { get; set; } = new();

    [XmlElement("IDFichero_Semilla")]
    public string FileIdSeed { get; set; } = "SO";

    [XmlElement("Fuente")]
    public CustomFont Font { get; set; } = new();

    [XmlElement("FuenteColor")]
    public string FontColor { get; set; } = AppSettings.ColorBlack;

    [XmlElement("NumeradorDe")]
    public int NumberFrom { get; set; } = 1;

    [XmlElement("NumeradorHasta")]
    public int NumberTo { get; set; } = 1;

    [XmlElement("NumeradorMatrizPos")]
    public TicketCoordinates StubNumberPosition { get; set; } = new();

    [XmlElement("CuponesPorTicket")]
    public int CouponsPerTicket { get; set; } = 1;

    [XmlElement("TamanoCuponX")]
    public Unit CouponWidth { get; set; }

    [XmlElement("NumerosDiferentesEnCupones")]
    public bool DifferentNumbersOnCoupons { get; set; }

    [XmlElement("MostrarNumeroEnMatriz")]
    public bool ShowNumberOnStub { get; set; }

    [XmlElement("TamanoMatriz")]
    public Unit StubSize { get; set; }

    [XmlElement("NumeroEnMatrizVertical")]
    public bool StubNumberVertical { get; set; } = true;

    [XmlElement("Sangrado")]
    public Unit Bleed { get; set; } = Unit.FromMillimeter(2.0);

    public TicketProjectSettings()
    {
        for (int i = 0; i < Slots; i++)
        {
            PerforationLinePositions[i] = new TicketCoordinates();
            NumberPositions[i] = new TicketCoordinates();
        }
    }

    /// <summary>Shallow copy, as the original. Original: <c>Copiar</c>.</summary>
    public TicketProjectSettings Clone() => (TicketProjectSettings)MemberwiseClone();
}
