using System.Xml.Serialization;
using PnpTool.Core.Cards;
using PnpTool.Core.Paper;
using PnpTool.Core.Primitives;

namespace PnpTool.Core.Projects;

/// <summary>
/// Paper and sides as file versions 14 to 17 stored them: the paper's size, format and
/// orientation sat beside the rows and columns. Original: <c>DisposicionImpresionV0</c>.
/// </summary>
[XmlType("DisposicionImpresionV0")]
public sealed class LegacyPrintLayout
{
    [XmlElement("Filas")]
    public int Rows { get; set; }

    [XmlElement("Columnas")]
    public int Columns { get; set; }

    [XmlElement("CalcularAutomaticamente")]
    public bool CalculateAutomatically { get; set; }

    [XmlElement("Horizontal")]
    public bool Landscape { get; set; }

    [XmlElement("TamanoPapel")]
    public UnitPoint PaperSize { get; set; } = new();

    [XmlElement("IndiceComboPapel")]
    public PaperType PaperType { get; set; } = PaperType.A4;

    [XmlElement("DobleCara")]
    public bool DoubleSided { get; set; }

    [XmlElement("ImagenTrasera")]
    public BackImageMode BackImage { get; set; } = BackImageMode.NoBack;

    public bool FoldBottom { get; set; } = true;

    /// <summary>Original: <c>DisposicionImpresion.CopiarV0</c>.</summary>
    public PrintLayout Upgrade()
    {
        var layout = new PrintLayout
        {
            Rows = Rows,
            Columns = Columns,
            CalculateAutomatically = CalculateAutomatically,
            DoubleSided = DoubleSided,
            BackImage = BackImage,
            FoldBottom = FoldBottom,
        };
        layout.Paper.Landscape = Landscape;
        layout.Paper.Type = PaperType;
        layout.Paper.Size = PaperSize;
        layout.UpdateCardsPerSheet();
        return layout;
    }
}

/// <summary>
/// The settings part of a style or project of file versions 14 to 17, which
/// <see cref="Upgrade"/> turns into today's <see cref="ProjectSettings"/>.
/// Original: <c>ConfiguracionProyectoV14</c> to <c>ConfiguracionProyectoV17</c>, with
/// <c>ConfiguracionOtrosParametros</c> and <c>ConfiguracionOtrosParametrosV2</c>.
/// </summary>
/// <remarks>
/// The original had one class per version and copied from each to the next. The versions differ
/// in a handful of elements, and the XML reader leaves alone what a file does not have, so one
/// class holding the elements of all four reads any of them; <see cref="Upgrade"/> then applies
/// the steps the file's version still needs.
/// </remarks>
public sealed class LegacyProjectSettings
{
    [XmlElement("ParametrosDeEntradaProyecto")]
    public InputParameters InputParameters { get; set; } = new();

    [XmlElement("GrupoProyectoGeneral")]
    public ProjectGroup Group { get; set; } = new();

    [XmlElement("CuadriculaProyecto")]
    public Grid Grid { get; set; } = new();

    [XmlElement("DisposicionImpresionProyecto")]
    public LegacyPrintLayout PrintLayout { get; set; } = new();

    [XmlElement("ConfiguracionCaraFrontal")]
    public FaceSettings Front { get; set; } = new();

    [XmlElement("ConfiguracionCaraTrasera")]
    public FaceSettings Back { get; set; } = new();

    [XmlElement("DesplazamientoTrasera")]
    public UnitPoint BackOffset { get; set; } = new();

    /// <summary>Version 17 only.</summary>
    [XmlElement("RotacionTrasera")]
    public double BackRotation { get; set; }

    [XmlElement("EncabezadoPieProyecto")]
    public HeaderFooter HeaderFooter { get; set; } = new();

    [XmlElement("MarcaDeAguaProyecto")]
    public Watermark Watermark { get; set; } = new();

    /// <summary>
    /// Versions 14 to 16 stored only the first six values, version 17 all of today's; the rest
    /// keep their defaults when the file lacks them.
    /// </summary>
    [XmlElement("OtrosParametrosProyecto")]
    public OtherSettings Other { get; set; } = new();

    /// <summary>Version 15 onwards.</summary>
    [XmlElement("Configuracionlibro")]
    public BookSettings Book { get; set; } = new();

    /// <summary>Version 14 only; later inside <see cref="Book"/>.</summary>
    [XmlElement("MarcasPerforacion")]
    public PerforationMarks PerforationMarks { get; set; } = new();

    /// <summary>Version 14 only; later inside <see cref="Book"/>.</summary>
    [XmlElement("ImagenEnLibro")]
    public BookImagePlacement ImagePlacement { get; set; }

    [XmlElement("CaraBlanca")]
    public CardFace? BlankFace { get; set; }

    [XmlElement("PanelDoble")]
    public bool DoublePanel { get; set; }

    [XmlElement("CarasRelleno")]
    public FaceType FillFaces { get; set; }

    /// <summary>Versions 14 and 15: only one back, which became a kind of back image.</summary>
    [XmlElement("CheckBox_Trasera_100")]
    public bool OnlyOneBack { get; set; }

    [XmlElement("ToolStripTextBox_Color_imagen_vacia")]
    public string EmptyImageColor { get; set; } = string.Empty;

    [XmlElement("CaraTraseraIgual")]
    public CardFace SharedBack { get; set; } = new();

    [XmlElement("Fuente")]
    public CustomFont Font { get; set; } = new();

    /// <summary>Version 17 only.</summary>
    [XmlElement("Bloquear_TamanoCarta")]
    public bool LockCardSize { get; set; } = true;

    /// <summary>
    /// Today's settings for what a file of <paramref name="version"/> (14 to 17) held.
    /// Original: <c>CopiardeV14</c>, <c>CopiardeV15</c>, <c>CopiardeV16</c> and <c>ConvertirAV18</c>.
    /// </summary>
    public ProjectSettings Upgrade(int version)
    {
        var settings = new ProjectSettings
        {
            DoublePanel = DoublePanel,
            FillFaces = FillFaces,
            EmptyImageColor = EmptyImageColor,
            BackOffset = new UnitPoint(BackOffset),
            HeaderFooter = HeaderFooter.Clone(),
            PrintLayout = PrintLayout.Upgrade(),
        };
        settings.InputParameters.CopyFrom(InputParameters);
        settings.Group.CopyFrom(Group);
        settings.Grid.CopyFrom(Grid);
        settings.Front.CopyFrom(Front);
        settings.Back.CopyFrom(Back);
        settings.Watermark.CopyFrom(Watermark);
        settings.Font.CopyFrom(Font);
        if (BlankFace != null)
        {
            settings.BlankFace.CopyFrom(BlankFace);
        }

        // The original's last step (ConvertirAV18) left this one out, so the back shared by
        // all cards was lost in every upgrade.
        settings.SharedBack.CopyFrom(SharedBack);

        // 14 -> 15: the perforation marks and the place of images moved into the book settings.
        // 15 -> 16 took only those two from the book settings and set the spine on the left.
        if (version <= 15)
        {
            PerforationMarks marks = version == 14 ? PerforationMarks : Book.PerforationMarks;
            settings.Book.PerforationMarks.CopyFrom(marks);
            settings.Book.ImagePlacement = version == 14 ? ImagePlacement : Book.ImagePlacement;
            settings.Book.Spine = BookSpineType.Left;

            // 15 -> 16: "only one back" stopped being a setting of its own.
            if (OnlyOneBack)
            {
                settings.PrintLayout.BackImage = BackImageMode.OnlyOne;
            }
        }
        else
        {
            settings.Book.CopyFrom(Book);
        }

        // 16 -> 17: colour space, compression percentage, stacked layout and resize filter
        // appeared, with their defaults, and so did the back rotation and the card size lock.
        if (version <= 16)
        {
            settings.Other.ImagePathMode = Other.ImagePathMode;
            settings.Other.PdfOutput = Other.PdfOutput;
            settings.Other.PdfMaximumSize = Other.PdfMaximumSize;
            settings.Other.GenerateFileId = Other.GenerateFileId;
            settings.Other.FileIdSeed = Other.FileIdSeed;
            settings.Other.Compression = Other.Compression;
        }
        else
        {
            settings.Other.CopyFrom(Other);
            settings.BackRotation = BackRotation;
            settings.LockCardSize = LockCardSize;
        }

        return settings;
    }
}
