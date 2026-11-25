using System.Xml.Serialization;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Templates;

namespace PnpTool.Core.Projects;

/// <summary>
/// Everything a style file (.pnps) stores, and the settings part of a project (.pnp).
/// Original: <c>ConfiguracionProyectoVActual</c>.
/// </summary>
[XmlRoot("ConfiguracionProyectoVActual")]
[XmlType("ConfiguracionProyectoVActual")]
public class ProjectSettings
{
    [XmlElement("ParametrosDeEntradaProyecto")]
    public InputParameters InputParameters { get; set; } = new();

    [XmlElement("GrupoProyectoGeneral")]
    public ProjectGroup Group { get; set; } = new();

    [XmlElement("CuadriculaProyecto")]
    public Grid Grid { get; set; } = new();

    [XmlElement("DisposicionImpresionProyecto")]
    public PrintLayout PrintLayout { get; set; } = new();

    [XmlElement("ConfiguracionCaraFrontal")]
    public FaceSettings Front { get; set; } = new();

    [XmlElement("ConfiguracionCaraTrasera")]
    public FaceSettings Back { get; set; } = new();

    /// <summary>Offset applied to backs to correct printer misalignment.</summary>
    [XmlElement("DesplazamientoTrasera")]
    public UnitPoint BackOffset { get; set; } = new();

    [XmlElement("RotacionTrasera")]
    public double BackRotation { get; set; }

    [XmlElement("EncabezadoPieProyecto")]
    public HeaderFooter HeaderFooter { get; set; } = new();

    [XmlElement("MarcaDeAguaProyecto")]
    public Watermark Watermark { get; set; } = new();

    [XmlElement("OtrosParametrosProyecto")]
    public OtherSettings Other { get; set; } = new();

    [XmlElement("Configuracionlibro")]
    public BookSettings Book { get; set; } = new();

    /// <summary>The face used where a card has no image.</summary>
    [XmlElement("CaraBlanca")]
    public CardFace BlankFace { get; set; }

    /// <summary>True when each panel shows a front and a back.</summary>
    [XmlElement("PanelDoble")]
    public bool DoublePanel { get; set; }

    [XmlElement("CarasRelleno")]
    public FaceType FillFaces { get; set; }

    [XmlElement("ToolStripTextBox_Color_imagen_vacia")]
    public string EmptyImageColor { get; set; } = string.Empty;

    /// <summary>The back shared by all cards when <see cref="PrintLayout.BackImage"/> is <see cref="BackImageMode.SameBack"/>.</summary>
    [XmlElement("CaraTraseraIgual")]
    public CardFace SharedBack { get; set; } = new();

    [XmlElement("Fuente")]
    public CustomFont Font { get; set; } = new();

    [XmlElement("Bloquear_TamanoCarta")]
    public bool LockCardSize { get; set; } = true;

    public ProjectSettings()
    {
        BlankFace = new CardFace
        {
            Path = AppSettings.ColorWhite,
            Size = new UnitPoint(),
            ImageKind = ImageKind.Blank,
        };
    }

    /// <summary>Original: <c>Copiar</c>.</summary>
    public void CopyFrom(ProjectSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        InputParameters.CopyFrom(source.InputParameters);
        Group.CopyFrom(source.Group);
        Grid.CopyFrom(source.Grid);
        PrintLayout.CopyFrom(source.PrintLayout);
        Front.CopyFrom(source.Front);
        Back.CopyFrom(source.Back);
        BackOffset = new UnitPoint(source.BackOffset);
        BackRotation = source.BackRotation;
        HeaderFooter = source.HeaderFooter.Clone();
        Watermark.CopyFrom(source.Watermark);
        Other.CopyFrom(source.Other);
        Book.CopyFrom(source.Book);
        BlankFace.CopyFrom(source.BlankFace);
        DoublePanel = source.DoublePanel;
        FillFaces = source.FillFaces;
        EmptyImageColor = source.EmptyImageColor;
        SharedBack.CopyFrom(source.SharedBack);
        Font.CopyFrom(source.Font);
        LockCardSize = source.LockCardSize;
    }
}

/// <summary>An open project: deck, settings and advanced template. Original: <c>Proyecto</c>.</summary>
public class Project
{
    /// <summary>Original: <c>ElMazo</c>.</summary>
    public Deck Deck { get; set; } = new();

    /// <summary>Original: <c>Configuracion</c>.</summary>
    public ProjectSettings Settings { get; set; } = new();

    /// <summary>Original: <c>PlantillAvanzada</c>.</summary>
    public AdvancedTemplate AdvancedTemplate { get; set; } = new();

    /// <summary>Original: <c>ProyectoHaCambiado</c>.</summary>
    public bool HasChanged { get; set; }

    /// <summary>Original: <c>Copiar</c>.</summary>
    public void CopyFrom(Project source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Deck.CopyFrom(source.Deck);
        Settings.CopyFrom(source.Settings);
        AdvancedTemplate = source.AdvancedTemplate.Clone();
    }
}
