using System.Xml;
using System.Xml.Serialization;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Paper;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.Core.Files;

/// <summary>
/// Turns a project of PdfCartas, another program to lay out cards (a ".Bsk" file, XML), into a
/// PNP Tool project written beside it. Only version 4 of that format is read.
/// Original: <c>FicheroPdfCartas</c>.
/// </summary>
public static class PdfCartasImporter
{
    /// <summary>The only version of the PdfCartas format that is understood.</summary>
    public const int SupportedVersion = 4;

    /// <summary>The style the new project starts from.</summary>
    public const string BaseStyle = "Cartas_Standar.pnps";

    /// <summary>One image of the PdfCartas project. Original: <c>FicheroPdfCartas.Imagen</c>.</summary>
    [XmlType("Imagen")]
    public class BskImage
    {
        /// <summary>The image file; one that starts with "\" is beside the project. Empty for a blank card.</summary>
        public string? Url { get; set; }

        [XmlElement("Copias")]
        public int Copies { get; set; }
    }

    /// <summary>The PdfCartas project as it is in the file. Original: <c>FicheroPdfCartas.Bsk</c>.</summary>
    [XmlRoot("Bsk")]
    public class Bsk
    {
        public int Version { get; set; }

        /// <summary>1: eight cards on a landscape sheet; 2: the "Man…" values (manual).</summary>
        [XmlElement("Modo")]
        public int Mode { get; set; }

        /// <summary>0: A4, 1: A3, 2: the size in <see cref="PageWidth"/> and <see cref="PageHeight"/>.</summary>
        [XmlElement("ManTamaño")]
        public int PaperChoice { get; set; }

        [XmlElement("ManHorizontal")]
        public int Landscape { get; set; }

        [XmlElement("ManColumnas")]
        public int Columns { get; set; }

        [XmlElement("ManFilas")]
        public int Rows { get; set; }

        /// <summary>Millimetres.</summary>
        [XmlElement("ManPagAncho")]
        public int PageWidth { get; set; }

        /// <summary>Millimetres.</summary>
        [XmlElement("ManPagAlto")]
        public int PageHeight { get; set; }

        /// <summary>0: cards apart by <see cref="Margin"/>; anything else: cards together.</summary>
        [XmlElement("Colocar")]
        public int Placement { get; set; }

        /// <summary>Millimetres.</summary>
        [XmlElement("SepMargen")]
        public int Margin { get; set; }

        /// <summary>Card width in millimetres, 0 for none.</summary>
        [XmlElement("ForAncho")]
        public int CardWidth { get; set; }

        /// <summary>Card height in millimetres, 0 for none.</summary>
        [XmlElement("ForAlto")]
        public int CardHeight { get; set; }

        /// <summary>0: no cut lines, 1: full lines, anything else: crosses.</summary>
        [XmlElement("Corte")]
        public int CutLines { get; set; }

        [XmlElement("CorLineas")]
        public int CutLineCount { get; set; }

        /// <summary>Points.</summary>
        [XmlElement("CorLinAncho")]
        public int CutLineWidth { get; set; }

        [XmlArray("Imagenes")]
        public BskImage[]? Images { get; set; }
    }

    /// <summary>
    /// Reads the PdfCartas project and writes a PNP Tool project of the same name beside it
    /// (with a number when there is one already). The message of the result is the path of the
    /// new project, or what went wrong. Original: <c>LeeFichero</c>.
    /// </summary>
    public static AppError Import(string path)
    {
        var result = new AppError();
        if (!File.Exists(path))
        {
            return Failure(path + " not found");
        }

        Bsk? read;
        try
        {
            using var reader = XmlReader.Create(path);
            read = new XmlSerializer(typeof(Bsk)).Deserialize(reader) as Bsk;
        }
        catch (Exception exception) when (exception is InvalidOperationException or XmlException or IOException or UnauthorizedAccessException)
        {
            read = null;
        }

        if (read == null)
        {
            return Failure("Error al leer el fichero");
        }

        if (read.Version != SupportedVersion)
        {
            return Failure("Solo se soporta la V4 de PdfCartas");
        }

        var project = new Project();
        ProjectSettings settings = project.Settings;
        AppError style = ProjectFileStore.LoadStyle(Path.Combine(AppPaths.DefaultStylesFolder, BaseStyle), ref settings).Error;
        if (style.HasError)
        {
            return style;
        }

        project.Settings = settings;
        settings.PrintLayout = LayoutOf(read);
        settings.Grid = GridOf(read);
        settings.Front.CutLine = CutLinesOf(read);
        settings.Other.ImagePathMode = ImagePathMode.Absolute;
        project.Deck = DeckOf(read.Images ?? [], path);

        string target = FileNames.Unique(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty, Path.GetFileNameWithoutExtension(path) + ".pnp"));
        try
        {
            ProjectFileStore.SaveProject(target, project);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failure(exception.Message);
        }

        result.Message = target;
        return result;
    }

    private static AppError Failure(string message) => new() { Kind = ErrorKind.Error, Message = message };

    /// <summary>Original: <c>LeeEstiloDisposicionImpresionProyecto</c>.</summary>
    public static PrintLayout LayoutOf(Bsk read)
    {
        var layout = new PrintLayout();
        switch (read.Mode)
        {
            case 1:
                layout.Rows = 2;
                layout.Columns = 4;
                layout.Paper.Landscape = true;
                break;
            case 2:
                switch (read.PaperChoice)
                {
                    case 0:
                        layout.Paper.Type = PaperType.A4;
                        break;
                    case 1:
                        layout.Paper.Type = PaperType.A3;
                        break;
                    case 2:
                        layout.Paper.Type = PaperType.Custom;
                        layout.Paper.Size = new UnitPoint { X = Unit.FromMillimeter(read.PageWidth), Y = Unit.FromMillimeter(read.PageHeight) };
                        break;
                }

                // The original only read these three for a sheet of custom size, although they
                // are values of the manual mode like the paper itself.
                if (read.Landscape == 1)
                {
                    layout.Paper.Landscape = true;
                }

                if (read.Rows > 0 && read.Columns > 0)
                {
                    layout.Rows = read.Rows;
                    layout.Columns = read.Columns;
                }

                break;
        }

        layout.UpdateCardsPerSheet();
        return layout;
    }

    /// <summary>Original: <c>LeeEstiloCuadriculaProyecto</c>.</summary>
    public static Grid GridOf(Bsk read)
    {
        var grid = new Grid();
        if (read.Placement == 0)
        {
            grid.Spacing.X = Unit.FromMillimeter(read.Margin);
            grid.Spacing.Y = Unit.FromMillimeter(read.Margin);
        }

        if (read.CardHeight != 0)
        {
            grid.CardSize.Y = Unit.FromMillimeter(read.CardHeight);
        }

        if (read.CardWidth != 0)
        {
            grid.CardSize.X = Unit.FromMillimeter(read.CardWidth);
        }

        return grid;
    }

    /// <summary>Original: <c>LeeEstiloLineasCorte</c>.</summary>
    public static CutLineSettings CutLinesOf(Bsk read)
    {
        var lines = new CutLineSettings();
        if (read.CutLines != 0)
        {
            lines.LineType = read.CutLines == 1 ? CutLineType.Full : CutLineType.Cross;
            lines.Thickness = Unit.FromPoint(read.CutLineWidth);
        }

        return lines;
    }

    /// <summary>Original: <c>LeeElMazo</c>.</summary>
    public static Deck DeckOf(BskImage[] images, string path)
    {
        var deck = new Deck();
        string folder = Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;
        foreach (BskImage image in images)
        {
            string url = image.Url ?? string.Empty;
            if (url.Length == 0)
            {
                deck.Add(AppSettings.ColorWhite, ImageKind.Blank);
            }
            else
            {
                // PdfCartas is a Windows program: its paths have backslashes.
                string file = url.StartsWith('\\')
                    ? Path.Combine(folder, PathHelper.FromFile(url[1..]))
                    : PathHelper.FromFile(url);
                deck.Add(file, ImageKind.Normal);
            }

            deck.Cards[^1].Copies = image.Copies;
        }

        deck.RecountCopies();
        return deck;
    }
}
