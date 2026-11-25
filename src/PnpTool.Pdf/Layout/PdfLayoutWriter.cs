using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ImageMagick;
using iText.IO.Image;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Xobject;
using iText.Kernel.XMP;
using iText.Layout;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Core.Templates;
using PnpTool.Core.Xml;
using PnpTool.Imaging;
using Path = System.IO.Path;

namespace PnpTool.Pdf.Layout;

/// <summary>What <see cref="PdfLayoutWriter.Create"/> produced.</summary>
/// <param name="Files">The PDF files written, the main one first.</param>
/// <param name="Folder">Folder holding them.</param>
/// <param name="SingleFile">Whether the result is one document, which the caller may open; otherwise it should show the folder.</param>
public sealed record PdfOutput(IReadOnlyList<string> Files, string Folder, bool SingleFile);

/// <summary>
/// Writes the PDF of a project with iText. Every sheet is first written as a PDF of its own
/// in the work folder; the sheets are then joined into the output file, or files, with the
/// shared back inserted where the project has one. Original: <c>FicheroPdfMaquetadoItext</c>.
/// </summary>
/// <remarks>
/// Not here: what the original did after writing (opening the PDF or its folder, and a web
/// page), which is left to the caller through <see cref="PdfOutput"/>; the Spanish-deck and
/// paper-miniature post-processing, which belong to their own tools; and the cut lines,
/// marks, footer and frames, which are in the other part of this class.
/// </remarks>
public partial class PdfLayoutWriter
{
    private readonly PageLayout _layout;
    private readonly ProjectSettings _settings;
    private readonly FaceSettings _front;
    private readonly FaceSettings _back;
    private readonly PrintLayout _print;
    private readonly Grid _grid;
    private readonly InputParameters _input;
    private readonly ProjectGroup _group;
    private readonly OtherSettings _other;
    private readonly string _sharedBackLastPageFile;
    private readonly bool _cmykProject;
    private readonly List<string> _outputFiles = [];
    private int _totalImages;
    private PdfFont? _font;
    private ImageData? _imageData;
    private Document? _document;
    private PdfDocument? _pdf;
    private PdfWriter? _writer;
    private FileStream? _iccStream;
    private PdfCanvas? _canvas;
    private bool _newSheet;

    /// <summary>Set to stop after the image being written. Original: <c>Cancelar</c>.</summary>
    public bool Cancel { get; set; }

    /// <summary>Receives the percentage done and a message. Original: <c>ReportProgressDelegate</c>.</summary>
    public Action<int, string>? ReportProgress { get; set; }

    /// <summary>
    /// Rotates the pages of a PDF file in place. Needed for sheets of backs when the project
    /// sets a back rotation; provided by the page-alignment tool. Arguments: file and angle.
    /// </summary>
    public static Action<string, double>? RotatePages { get; set; }

    /// <summary>The layout being written, for the decorations and for tests.</summary>
    public PageLayout Layout => _layout;

    public PdfLayoutWriter(PdfImage[] images, string documentId, ProjectSettings settings, string workFolder, AdvancedTemplate template)
    {
        Directory.CreateDirectory(workFolder);
        _layout = new PageLayout(images, documentId, settings, workFolder, template);
        _totalImages = _layout.Images.Length;
        _settings = settings;
        _front = settings.Front;
        _back = settings.Back;
        _print = settings.PrintLayout;
        _grid = settings.Grid;
        _input = settings.InputParameters;
        _group = settings.Group;
        _other = settings.Other;
        _sharedBackLastPageFile = AppSettings.TempFolder + "TraseraIgual_UltimaPagina.pdf";
        _cmykProject = _other.ColorSpace == ProjectColorSpace.CMYK;
    }

    private void Progress(int percent, string message) => ReportProgress?.Invoke(percent, message);

    // ------------------------------------------------------------ decorations (other part of the class)

    partial void DrawFooter(int pageNumber, FaceType face, bool overrideEmpty, PdfImage[]? images);

    partial void DrawCutLines(FaceType face);

    partial void DrawPerforationMarks();

    partial void DrawDocumentId();

    partial void DrawPageWatermark();

    partial void DrawCalibrationImage();

    // ------------------------------------------------------------ writing

    /// <summary>Writes the document. Original: <c>CrearPDF</c>.</summary>
    public PdfOutput Create()
    {
        _layout.TotalSheets = _totalImages / _print.CardsPerSheet;
        Progress(0, StringResources.Get("Crear_PDF_Preparando_PDF"));
        _layout.FlipPositionsForPdf(PositionTable.Fronts);
        if (_group.ProjectType == ProjectType.Gutterfold)
        {
            WriteGutterfold();
        }
        else
        {
            WriteSheets(_layout.PartFilePrefix + "_0.pdf", FaceType.Front, null);
        }

        PdfOutput output = Finish(_layout.WorkFolder);
        _layout.DisposeWatermark();
        Progress(100, AppSettings.ProcessEndMarker);
        return output;
    }

    /// <summary>
    /// Writes the cells sheet by sheet, each sheet to a file of its own named with the sheet
    /// number. Original: <c>CrearPDF_Normal</c>.
    /// </summary>
    private void WriteSheets(string file, FaceType firstFace, PdfImage[]? images)
    {
        Log.Write("Inicia crea pdf " + _group.ProjectType.ToOriginalName());
        double step = 100.0 / _totalImages;
        double done = 0.0;
        if (_print.DoubleSided)
        {
            _layout.ComputePositions(FaceType.Back);
            _layout.FlipPositionsForPdf(PositionTable.Backs);
        }

        _layout.CurrentFace = firstFace;
        _layout.CurrentSheet = 0;
        StartSheet(file, 0, firstFace, images);
        DrawCalibrationImage();
        Progress(0, StringResources.Get("Crear_PDF_Insertando_imagenes"));
        int index = 0;
        _newSheet = true;
        bool sheetDone;
        do
        {
            Log.Write("Inserta imagen " + index);
            _layout.SetPositionForCurrentCell();
            PlaceImage(index, _group.PDFX);
            sheetDone = _layout.Advance().SheetDone;
            if (sheetDone)
            {
                _newSheet = true;
                FinishSheet();
                CloseFile(file);
                if (index < _totalImages - 1)
                {
                    sheetDone = false;
                    _layout.SetFaceForCurrentSheet();
                    file = _layout.PartFilePrefix + "_" + _layout.CurrentSheet + ".pdf";
                    StartSheet(file, _layout.CurrentSheet, _layout.CurrentFace, null);
                }
            }

            done += step;
            Progress(Convert.ToInt32(done), StringResources.Get("Imagen") + " " + index + "/" + _totalImages);
            index++;
        }
        while (index < _totalImages && !Cancel);

        if (!sheetDone && (_layout.Cell.X != 0 || _layout.Cell.Y != 0))
        {
            FinishSheet();
            CloseFile(file);
        }

        CloseFile(file);
    }

    /// <summary>
    /// For a shared back: the cells of the last sheet of backs, which has backs only behind
    /// the cards the last sheet of fronts really holds. Original: <c>ModificaListadoImagenes_TraseraIgual_UltimaPagina</c>.
    /// </summary>
    private PdfImage[] LastSharedBackSheet()
    {
        int perSheet = _print.CardsPerSheet;
        int first = ((_totalImages / perSheet) - 1) * perSheet;
        bool template = _group.ProjectType == ProjectType.AdvancedStyles;
        int column = template ? 0 : _print.Columns - 1;
        int row = 0;
        var cells = new PdfImage[perSheet];
        for (int i = first; i < _totalImages; i++)
        {
            int place = column + (_print.Columns * row);
            cells[place] = new PdfImage(_settings.SharedBack) { Kind = _layout.Images[i].Kind };
            if (row > 0)
            {
                cells[place].SameAsPrevious = true;
            }

            if (!template)
            {
                column--;
                if (column == -1)
                {
                    column = _print.Columns - 1;
                    row++;
                }
            }
            else
            {
                cells[place].Rotation.Angle = 360 - _layout.Images[place].Rotation.Angle;
                column++;
            }
        }

        return cells;
    }

    /// <summary>Original: <c>GeneraPDFTraseraIgual_UltimaPagina</c>.</summary>
    private void WriteLastSharedBackSheet()
    {
        _layout.Images = LastSharedBackSheet();
        _totalImages = _layout.Images.Length;
        WriteSheets(_sharedBackLastPageFile, FaceType.Back, _layout.Images);
    }

    /// <summary>
    /// Closes the file being written. A sheet of backs is then rotated when the project asks
    /// for it. Original: <c>FinalizarPDF</c>.
    /// </summary>
    private void CloseFile(string? file)
    {
        Log.Write("finaliza pdf " + _layout.CurrentSheet);
        try
        {
            _canvas?.Release();
            _document?.Close();
            if (_pdf is { } pdf && !pdf.IsClosed())
            {
                pdf.Close();
            }
        }
        finally
        {
            _iccStream?.Close();
            _writer?.Close();
            _writer?.Dispose();
            _canvas = null;
            _document = null;
        }

        if (!string.IsNullOrEmpty(file) && _layout.CurrentFace == FaceType.Back && Convert.ToInt32(_settings.BackRotation) != 0)
        {
            if (RotatePages != null)
            {
                RotatePages(file, _settings.BackRotation);
            }
            else
            {
                Log.Write("Back rotation not applied: no page rotation available", force: true);
            }
        }

        _layout.Cell.X = 0;
        _layout.Cell.Y = 0;
    }

    /// <summary>Opens a new file with one page for a sheet. Original: <c>InicializarPDF</c>.</summary>
    private void StartSheet(string file, int pageNumber, FaceType face, PdfImage[]? images)
    {
        Log.Enter(nameof(StartSheet));
        OpenFile(file);
        PdfPage page = _pdf!.AddNewPage(PageSizeOfSheet());
        Rectangle trim = page.GetPageSize();
        trim.SetX(trim.GetX() + 32f);
        trim.SetY(trim.GetY() + 32f);
        trim.SetWidth(trim.GetWidth() - 32f);
        trim.SetHeight(trim.GetHeight() - 32f);
        page.SetTrimBox(trim);
        _canvas = new PdfCanvas(page);
        DrawFooter(pageNumber, face, overrideEmpty: false, images);
        _layout.SetEffectFor(face);
        Log.Exit(nameof(StartSheet));
    }

    private PageSize PageSizeOfSheet() =>
        new((float)_print.Paper.Size.X.Point, (float)_print.Paper.Size.Y.Point);

    /// <summary>Creates the PDF document and its metadata. Original: <c>GenerarPDF</c>.</summary>
    private void OpenFile(string file)
    {
        _writer = new PdfWriter(file, new WriterProperties().SetPdfVersion(PdfVersion.PDF_1_3));
        _pdf = new PdfDocument(_writer);
        if (_group.PDFX)
        {
            const string version = "PDF/X-3:2002";
            const string key = "GTS_PDFXVersion";
            AddOutputIntent();
            WriteDocumentInfo(file, key, version);
            _pdf.GetCatalog().GetPdfObject().Put(new PdfName(key), new PdfString(version));
            WriteXmp(file, key, version);
        }
        else
        {
            AddOutputIntent();
            WriteDocumentInfo(file, string.Empty, string.Empty);
        }

        AddDefaultFont();
        _document = new Document(_pdf);
    }

    /// <summary>
    /// The font of the footer texts, embedded in the document. The original embedded Times New
    /// Roman; Liberation Serif, which has the same letter widths, takes its place.
    /// Original: <c>GeneraFuenteparaPDF</c>.
    /// </summary>
    private void AddDefaultFont()
    {
        string path = Path.Combine(AppPaths.FontsFolder, "LiberationSerif-Regular.ttf");
        if (!File.Exists(path))
        {
            _font = null;
            return;
        }

        _font = PdfFontFactory.CreateFont(path, "Windows-1252", PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
        _pdf!.AddFont(_font);
    }

    /// <summary>Original: <c>CreaxmpMeta</c>.</summary>
    private void WriteXmp(string file, string key, string version)
    {
        XMPMeta xmp = XMPMetaFactory.Create();
        XMPMetaFactory.GetSchemaRegistry().RegisterNamespace("http://www.npes.org/pdfx/ns/id/", "pdfxid");
        XMPMetaFactory.GetSchemaRegistry().RegisterNamespace("http://ns.adobe.com/pdfx/1.3/", "pdfx");
        xmp.SetProperty("http://ns.adobe.com/xap/1.0/mm/", "DocumentID", "uuid:" + PageLayout.UniqueHash(file));
        xmp.SetProperty("http://ns.adobe.com/pdf/1.3/", "Trapped", "False");
        xmp.SetProperty("http://ns.adobe.com/pdfx/1.3/", key, version);
        xmp.SetProperty("http://www.npes.org/pdfx/ns/id/", key, version);
        string now = XMPUtils.ConvertFromDate(XMPDateTimeFactory.GetCurrentDateTime());
        xmp.SetProperty("http://ns.adobe.com/xap/1.0/", "CreateDate", now);
        xmp.SetProperty("http://ns.adobe.com/xap/1.0/", "ModifyDate", now);
        xmp.SetProperty("http://ns.adobe.com/xap/1.0/", "MetadataDate", now);
        xmp.SetProperty("http://ns.adobe.com/xap/1.0/mm/", "RenditionClass", "default");
        xmp.SetLocalizedText("http://purl.org/dc/elements/1.1/", "title", "x-default", "x-default", Path.GetFileNameWithoutExtension(file));
        _pdf!.SetXmpMetadata(xmp);
    }

    /// <summary>
    /// Declares the project's ICC profile as the output intent of the document. A project
    /// without a profile file gets none; the original failed in that case.
    /// Original: <c>CreapdfOutputIntent</c>.
    /// </summary>
    private void AddOutputIntent()
    {
        string profile = _other.ColorProfilePath;
        if (string.IsNullOrEmpty(profile) || !File.Exists(profile))
        {
            _iccStream = null;
            return;
        }

        _iccStream = new FileStream(profile, FileMode.Open, FileAccess.Read);
        string name = Path.GetFileNameWithoutExtension(profile);
        var intent = new PdfOutputIntent(name, string.Empty, "https://www.color.org", name, _iccStream);
        intent.SetOutputIntentSubtype(new PdfName("GTS_PDFX"));
        _pdf!.AddOutputIntent(intent);
    }

    private static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Title and the program's own properties. Original: <c>InsertarMetadatosDocumentInfo</c>.</summary>
    private void WriteDocumentInfo(string file, string key, string version)
    {
        PdfDocumentInfo info = _pdf!.GetDocumentInfo();
        info.SetTitle(Path.GetFileNameWithoutExtension(file));
        info.SetTrapped(new PdfName("False"));
        if (key.Length > 0)
        {
            info.SetMoreInfo(key, version);
        }

        info.SetMoreInfo(PdfMetadataKeys.PdfId, _layout.DocumentId);
        info.SetMoreInfo(PdfMetadataKeys.GroupIndex, _group.Group.ToOriginalName());
        info.SetMoreInfo(PdfMetadataKeys.SheetWidth, Number(_print.Paper.Size.X.Millimeter));
        info.SetMoreInfo(PdfMetadataKeys.SheetHeight, Number(_print.Paper.Size.Y.Millimeter));
        info.SetMoreInfo(PdfMetadataKeys.CardWidth, Number(_grid.CardSize.X.Millimeter));
        info.SetMoreInfo(PdfMetadataKeys.CardHeight, Number(_grid.CardSize.Y.Millimeter));
        info.SetMoreInfo(PdfMetadataKeys.SpacingX, Number(_grid.Spacing.X.Millimeter));
        info.SetMoreInfo(PdfMetadataKeys.SpacingY, Number(_grid.Spacing.Y.Millimeter));
        info.SetMoreInfo(PdfMetadataKeys.Bleed, Number(_grid.Bleed.Millimeter));
        info.SetMoreInfo(PdfMetadataKeys.DoubleSided, _print.DoubleSided.ToString());
        info.SetMoreInfo(PdfMetadataKeys.ProgramVersion, AppSettings.ProgramVersion);
        string profile = _other.ColorProfilePath;
        if (!string.IsNullOrEmpty(profile))
        {
            profile = Path.GetFileName(profile);
        }

        info.SetMoreInfo("ICC", _other.ColorSpace + " " + profile);
    }

    /// <summary>Counts written when a joined document is closed. Original: <c>InsertarMetadatosAlCerrar</c>.</summary>
    private void WriteClosingInfo()
    {
        int sheets = _pdf!.GetNumberOfPages();
        PdfDocumentInfo info = _pdf.GetDocumentInfo();
        info.SetMoreInfo(PdfMetadataKeys.PdfId, _layout.DocumentId);
        int cards = _print.Columns * _print.Rows * sheets;
        info.SetMoreInfo(PdfMetadataKeys.CardCount, (_print.DoubleSided ? cards / 2 : cards).ToString());
        info.SetMoreInfo(PdfMetadataKeys.SheetCount, sheets.ToString());
        info.SetMoreInfo(PdfMetadataKeys.Hash, ContentHash(sheets, _layout.Images.Length));
    }

    /// <summary>
    /// Check value stored in the document, from its number of sheets and of cells. The text
    /// hashed is that of the original (<c>DevuelveHashSoriano</c>), so that documents made
    /// here are told apart from tampered ones wherever the Windows program's are.
    /// </summary>
    public static string ContentHash(int sheets, int cells) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sheets.ToString() + cells + "PNPSoriano")));

    /// <summary>Marks and texts drawn over a finished sheet. Original: <c>FinalizarHojaPDF</c>.</summary>
    private void FinishSheet()
    {
        Log.Write("finaliza hoja pdf " + _layout.CurrentSheet);
        _layout.CurrentSheet++;
        DrawCutLines(_layout.CurrentFace);
        if (_settings.Book.PerforationMarks.Type != PerforationMarkType.None)
        {
            DrawPerforationMarks();
        }

        if (_other.GenerateFileId)
        {
            DrawDocumentId();
        }

        DrawPageWatermark();
    }

    // ------------------------------------------------------------ gutterfold

    /// <summary>
    /// Writes a gutterfold: rows of fronts and rows of backs alternate on the same sheet.
    /// Original: <c>CrearPDF_Glutter</c>.
    /// </summary>
    private void WriteGutterfold()
    {
        double step = 100.0 / _totalImages;
        double done = 0.0;
        string file = _layout.PartFilePrefix + "_0.pdf";
        _layout.ComputePositions(FaceType.Back);
        _layout.FlipPositionsForPdf(PositionTable.Backs);
        _layout.CurrentFace = FaceType.Front;
        StartSheet(file, 0, FaceType.Front, null);
        DrawCalibrationImage();
        Progress(0, StringResources.Get("Crear_PDF_Insertando_imagenes"));
        int index = 0;
        UnitPoint faceSize = _front.Size;
        _newSheet = true;
        bool sheetDone;
        do
        {
            _layout.SetPositionForCurrentCell();
            AdjustGutterfoldPosition(faceSize);
            PlaceImage(index, _group.PDFX);

            (bool rowChanged, sheetDone) = _layout.Advance();
            _layout.OnRowChanged(rowChanged);
            if (rowChanged)
            {
                if (_layout.Cell.Y % 2 == 0)
                {
                    _layout.CurrentFace = FaceType.Front;
                    faceSize = _front.Size;
                }
                else
                {
                    _layout.CurrentFace = FaceType.Back;
                    faceSize = _back.Size;
                }
            }

            if (sheetDone)
            {
                _newSheet = true;
                _layout.CurrentFace = FaceType.Front;
                FinishSheet();
                CloseFile(file);
                file = string.Empty;
                if (index < _totalImages - 1)
                {
                    file = _layout.PartFilePrefix + "_" + _layout.CurrentSheet + ".pdf";
                    StartSheet(file, _layout.CurrentSheet, _layout.CurrentFace, null);
                }

                sheetDone = false;
            }

            index++;
            done += step;
            Progress(Convert.ToInt32(done), StringResources.Get("Imagen") + " " + index + "/" + _totalImages);
        }
        while (index < _totalImages && !Cancel);

        if (!sheetDone && (_layout.Cell.X != 0 || _layout.Cell.Y != 0))
        {
            _layout.CurrentFace = FaceType.Front;
            FinishSheet();
            CloseFile(file);
        }

        _layout.HasSharedBack = false;
        CloseFile(null);
    }

    /// <summary>
    /// Centres the image of a gutterfold cell: positions were computed for the cell, and the
    /// face may be smaller. Original: <c>CrearPDF_Glutter_ModificaPosicionPintarPDF</c>.
    /// </summary>
    private void AdjustGutterfoldPosition(UnitPoint faceSize)
    {
        UnitPoint position = _layout.Position;
        if (_layout.CurrentFace == FaceType.Front)
        {
            position.Y = (double)position.Y - (((double)_grid.CardSize.Y - (double)faceSize.Y) / 2.0);
        }
        else
        {
            position.Y = (double)position.Y - ((double)_grid.CardSize.Y - (double)_front.Size.Y);
            position.Y = (double)position.Y + (((double)_grid.CardSize.Y - (double)faceSize.Y) / 2.0);
        }

        position.X = (double)position.X + (((double)_grid.CardSize.X - (double)faceSize.X) / 2.0);
    }

    // ------------------------------------------------------------ cells

    /// <summary>Draws the cell at the current position. Original: <c>InsertaImagenEnPDF</c>.</summary>
    private void PlaceImage(int index, bool pdfX)
    {
        PdfImage? cell = _layout.Images[index];
        if (cell == null)
        {
            return;
        }

        Log.Enter(index + cell.Path);
        if (cell.Kind == ImageKind.Empty)
        {
            return;
        }

        if (_group.ProjectType == ProjectType.AdvancedStyles)
        {
            (_layout.Position, _layout.TargetSize) = _layout.TemplatePlace(index, _layout.Position);
        }

        float x = (float)_layout.Position.X.Point;
        float y = (float)_layout.Position.Y.Point;
        float width = (float)_layout.TargetSize.X.Point;
        float height = (float)_layout.TargetSize.Y.Point;
        if (_settings.FillFaces != FaceType.None)
        {
            DrawCardBackground(index);
        }

        switch (cell.Kind)
        {
            case ImageKind.Normal:
                if (!cell.SameAsPrevious || _newSheet)
                {
                    _imageData = ToImageData(_layout.PrepareImage(index, pdfX));
                    _newSheet = false;
                }

                _canvas!.AddImageFittedIntoRectangle(_imageData, new Rectangle(x, y, width, height), false);
                break;
            case ImageKind.Blank:
                _canvas!.SaveState();
                ImageData solid = ToImageData(SolidColorImages.Create(cell.Path, _other.ColorProfile, _other.ColorSpace, MagickFormat.Jpg));
                _canvas.AddImageFittedIntoRectangle(solid, new Rectangle(x, y, width, height), false);
                _canvas.RestoreState();
                break;
        }

        Log.Exit();
    }

    /// <summary>
    /// The colour behind a card, covering its cell and the spacing after it. Original:
    /// <c>PintaColorFondoCarta</c> and <c>PintaColorFondoCarta_Normal</c>.
    /// </summary>
    private void DrawCardBackground(int index)
    {
        FaceSettings current = _layout.CurrentFace == FaceType.Front ? _front : _back;
        if (!current.PaintBackground || _group.ProjectType == ProjectType.AdvancedStyles)
        {
            return;
        }

        FaceType face = _layout.CurrentFace;
        if (_group.ProjectType == ProjectType.Gutterfold)
        {
            face = _layout.FaceForCurrentRow();
        }

        float width = (float)(_grid.CardSize.X.Point + _grid.Spacing.X.Point);
        float height = (float)(_grid.CardSize.Y.Point + _grid.Spacing.Y.Point);
        UnitPoint position = _layout.PositionOfCurrentCell(face, centreInCell: false);
        position.Y = (double)_print.Paper.Size.Y - (double)position.Y - (double)_grid.CardSize.Y;

        string color;
        if (_front.BackgroundEffect == CardBackgroundType.SolidColor)
        {
            color = _front.BackgroundFillColor;
            if (face != FaceType.Front && _settings.FillFaces is FaceType.Back or FaceType.Both)
            {
                color = _back.BackgroundFillColor;
            }
        }
        else
        {
            color = _layout.Images[index].BackgroundColor;
        }

        _canvas!.SaveState();
        MagickFormat format = ImageFormats.FormatFromExtension(Path.GetExtension(_layout.Images[index].Path));
        ImageData solid = ToImageData(SolidColorImages.Create(color, _other.ColorProfile, _other.ColorSpace, format));
        _canvas.AddImageFittedIntoRectangle(solid, new Rectangle((float)position.X.Point, (float)position.Y.Point, width, height), false);
        _canvas.RestoreState();
    }

    /// <summary>
    /// Encodes a prepared image for the PDF and disposes it. An image iText cannot take is
    /// replaced by the "not found" image. Original: <c>CargarImagenNueva_DevolverError</c>.
    /// </summary>
    private ImageData ToImageData(MagickImage image) => ToImageData(image, _group.PDFX).Data;

    /// <summary>Whether an image file can be put into a PDF. Original: <c>CheckImagenNuevaParaItext</c>.</summary>
    public static AppError CheckImage(string? path, bool pdfX) =>
        string.IsNullOrEmpty(path) ? new AppError() : ToImageData(new MagickImage(path), pdfX).Error;

    internal static (ImageData Data, AppError Error) ToImageData(MagickImage image, bool pdfX)
    {
        var error = new AppError();
        try
        {
            MagickFormat format = image.Format;
            byte[] bytes = ImageConversions.ToPdfImageBytes(image, pdfX);
            ImageData data = format != MagickFormat.Tiff
                ? ImageDataFactory.Create(bytes)
                : ImageDataFactory.CreateTiff(bytes, true, 1, true);
            return (data, error);
        }
        catch (iText.IO.Exceptions.IOException exception)
        {
            using var notFound = new MagickImage(StockImages.PathOf(StockImage.NotFound));
            ImageData data = ImageDataFactory.Create(ImageConversions.ToPdfImageBytes(notFound, pdfX));
            error.Kind = ErrorKind.Error;
            error.Message = exception.Message;
            return (data, error);
        }
        finally
        {
            image.Dispose();
        }
    }

    /// <summary>
    /// A stock image as PDF image data, converted to CMYK for a CMYK project.
    /// Original: <c>ImagenesGeneral.ImageDataFromResource</c>.
    /// </summary>
    internal static ImageData StockImageData(StockImage stock, OtherSettings other, bool pdfX)
    {
        using var image = new MagickImage(StockImages.PathOf(stock));
        if (other.ColorSpace == ProjectColorSpace.CMYK)
        {
            ColorManagement.RgbToCmyk(image, (IColorProfile?)other.ColorProfile ?? ColorManagement.DefaultCmykProfile());
        }

        return ImageDataFactory.Create(ImageConversions.ToPdfImageBytes(image, pdfX));
    }

    // ------------------------------------------------------------ shared back

    /// <summary>
    /// Draws the shared back on a page and returns it as a form to stamp behind every sheet.
    /// Original: <c>RellenaXFormTraseraigual</c>.
    /// </summary>
    private PdfFormXObject FillSharedBackForm(BackImageMode mode, int pageNumber, PdfPage page)
    {
        _canvas = new PdfCanvas(page);
        CardFace back = _settings.SharedBack;
        int perSheet = _print.CardsPerSheet;
        for (int i = 0; i < perSheet && i < _layout.Images.Length; i++)
        {
            _layout.Images[i] = new PdfImage(back) { SameAsPrevious = i > 0 };
        }

        _layout.CurrentSheet = 0;
        DrawFooter(pageNumber, FaceType.Back, overrideEmpty: true, null);
        _layout.SetEffectFor(FaceType.Back);
        if (mode == BackImageMode.Pattern)
        {
            back.Path = BackPattern.Create(_settings);
        }

        _imageData = ToImageData(_layout.PrepareImage(back.Path, back.Rotation, _group.PDFX));
        switch (mode)
        {
            case BackImageMode.SameBack:
            case BackImageMode.DifferentBack:
                DrawSharedBackGrid();
                break;
            case BackImageMode.OnlyOne:
            case BackImageMode.Pattern:
                DrawSharedBackWholeSheet();
                break;
        }

        FinishSheet();
        return new PdfFormXObject(page);
    }

    /// <summary>One image over the whole sheet. Original: <c>RellenaXFormTraseraigualSoloUna</c>.</summary>
    private void DrawSharedBackWholeSheet()
    {
        _layout.Cell.Y = _print.Rows - 1;
        _layout.Cell.X = 0;
        _layout.CurrentFace = FaceType.Back;
        _layout.TargetSize.X = _print.Paper.Size.X;
        _layout.TargetSize.Y = _print.Paper.Size.Y;
        _layout.Position = new UnitPoint();
        if (_settings.FillFaces is FaceType.Back or FaceType.Both)
        {
            DrawCardBackground(0);
        }

        var area = new Rectangle(
            (float)_layout.Position.X.Point,
            (float)_layout.Position.Y.Point,
            (float)_layout.TargetSize.X.Point,
            (float)_layout.TargetSize.Y.Point);
        _canvas!.AddImageFittedIntoRectangle(_imageData, area, false);
    }

    /// <summary>Original: <c>ModificaAnguloXFormTraseraIgual</c>.</summary>
    private Unit SharedBackX(int angle, int index, int exifAngle, Unit x)
    {
        if (_group.ProjectType == ProjectType.AdvancedStyles)
        {
            AdvancedTemplateCard place = _layout.Template.Cards[index];
            angle = (angle + place.Rotation - exifAngle) % 360;
            if (angle == 90)
            {
                return (double)_layout.Position.X + (double)place.Size.X;
            }
        }
        else
        {
            angle -= exifAngle;
            if (angle is 90 or 270)
            {
                return (double)_layout.Position.X + (double)_layout.TargetSize.X;
            }
        }

        return x;
    }

    /// <summary>The back repeated behind every card of the sheet. Original: <c>RellenaXFormTraseraigualStandar</c>.</summary>
    private void DrawSharedBackGrid()
    {
        _layout.Cell.Y = 0;
        _layout.Cell.X = 0;
        int perSheet = _print.CardsPerSheet;
        bool template = _group.ProjectType == ProjectType.AdvancedStyles;
        _layout.CurrentFace = FaceType.Back;
        _layout.TargetSize = new UnitPoint(_layout.BackImageSize);
        if (_settings.FillFaces is FaceType.Back or FaceType.Both)
        {
            for (int i = 0; i < perSheet; i++)
            {
                if (template)
                {
                    _layout.TargetSize = new UnitPoint(_layout.Template.Cards[i].Size);
                }

                DrawCardBackground(i);
                _layout.Advance();
            }
        }

        RotationProperties exif = MagickImageInspector.ExifRotation(_settings.SharedBack.Path);
        const int angle = 0;
        for (int i = 0; i < perSheet; i++)
        {
            _layout.SetPositionForCurrentCell();
            var place = new AdvancedTemplateCard();
            if (template)
            {
                place = _layout.Template.Cards[i];
                _layout.TargetSize = new UnitPoint(place.Size);
            }

            var point = new UnitPoint(_layout.Position);
            point.X = SharedBackX(angle, i, exif.Angle, point.X);
            if (template && place.Rotation != 0)
            {
                point.X = (double)point.X - (double)_layout.TargetSize.X;
            }

            AffineTransform transform = AffineTransform.GetTranslateInstance(point.X.Point, point.Y.Point);
            transform.Concatenate(AffineTransform.GetScaleInstance(_layout.TargetSize.X.Point, _layout.TargetSize.Y.Point));
            transform.Concatenate(AffineTransform.GetRotateInstance(Math.PI / 180.0 * angle));
            float[] matrix = new float[6];
            transform.GetMatrix(matrix);
            _canvas!.AddImageWithTransformationMatrix(_imageData, matrix[0], matrix[1], matrix[2], matrix[3], matrix[4], matrix[5]);
            _layout.Advance();
        }
    }

    /// <summary>A file with one sheet of the shared back. Original: <c>GeneraPDFTraseraUnica</c>.</summary>
    private void WriteSharedBackFile(string file)
    {
        _layout.UseSharedBackSheet();
        _totalImages = _layout.Images.Length;
        WriteSheets(file, FaceType.Back, null);
    }

    // ------------------------------------------------------------ joining

    /// <summary>
    /// Joins the sheet files into the output chosen in the project: one document, one file per
    /// sheet, or documents up to a maximum size. Original: <c>TerminarCrearPDF</c>.
    /// </summary>
    private PdfOutput Finish(string workFolder)
    {
        BackImageMode backImage = _print.BackImage;
        int count = Directory.GetFiles(workFolder, "*.pdf", SearchOption.TopDirectoryOnly).Length;
        var parts = new string[count];
        for (int i = 0; i < count; i++)
        {
            parts[i] = workFolder + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(_input.OutputPdf) + "_" + i + ".pdf";
        }

        string folder = Path.GetDirectoryName(_input.OutputPdf) ?? string.Empty;
        bool single;
        switch (_other.PdfOutput)
        {
            case PdfOutputMode.Separate:
                FinishSeparate(parts, folder);
                single = false;
                break;
            case PdfOutputMode.MaximumSize:
                single = FinishByMaximumSize(parts, workFolder);
                break;
            default:
                FinishSingle(parts);
                single = true;
                break;
        }

        foreach (string part in parts)
        {
            DeleteQuietly(part);
        }

        _print.BackImage = backImage;
        return new PdfOutput(_outputFiles, folder, single);
    }

    private static void DeleteQuietly(string file)
    {
        try
        {
            if (!string.IsNullOrEmpty(file))
            {
                File.Delete(file);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Original: <c>TerminarCrearPDFSeparados</c>.</summary>
    private void FinishSeparate(string[] parts, string folder)
    {
        foreach (string part in parts)
        {
            string destination = Path.Combine(folder, Path.GetFileName(part));
            File.Copy(part, destination, overwrite: true);
            _outputFiles.Add(destination);
        }

        if (_layout.HasSharedBack)
        {
            string back = Path.Combine(folder, Path.GetFileNameWithoutExtension(_input.OutputPdf)) + "_back_x" + parts.Length + ".pdf";
            WriteSharedBackFile(back);
            _outputFiles.Add(back);
        }

        Progress(100, StringResources.Get("Documentos_Generados"));
    }

    /// <summary>Original: <c>TerminarCrearPDFSolo1</c>.</summary>
    private void FinishSingle(string[] parts)
    {
        Progress(0, StringResources.Get("Crear_PDF_PDF_Final"));
        if (_layout.HasSharedBack)
        {
            JoinWithSharedBack(_input.OutputPdf, parts);
        }
        else
        {
            Join(_input.OutputPdf, parts, split: false, 0);
        }

        _outputFiles.Add(_input.OutputPdf);
    }

    /// <summary>Original: <c>TerminarCrearPDFTamanoMaximo</c>. Returns whether a single document resulted.</summary>
    private bool FinishByMaximumSize(string[] parts, string workFolder)
    {
        if (_print.DoubleSided || _layout.HasSharedBack)
        {
            parts = PairFrontsWithBacks(parts, workFolder);
        }

        List<string> written = Join(_input.OutputPdf, parts, split: true, _other.PdfMaximumSize);
        _outputFiles.AddRange(written);
        if (written.Count != 1)
        {
            Progress(100, StringResources.Get("Documentos_Generados"));
        }

        foreach (string part in parts)
        {
            DeleteQuietly(part);
        }

        return written.Count == 1;
    }

    /// <summary>
    /// Joins each sheet of fronts with its sheet of backs so that a size limit never separates
    /// them. Original: <c>TerminarCrearPDFTamanoMaximoUnirFrontalTrasera</c>.
    /// </summary>
    private string[] PairFrontsWithBacks(string[] parts, string workFolder)
    {
        var pairs = new List<string>();
        string sharedBack = string.Empty;
        if (_layout.HasSharedBack)
        {
            sharedBack = Path.Combine(Path.GetDirectoryName(_input.OutputPdf) ?? string.Empty, Path.GetFileNameWithoutExtension(_input.OutputPdf)) + "_back.pdf";
            WriteSharedBackFile(sharedBack);
            parts = _layout.InterleaveSharedBack(parts, sharedBack);
        }

        int pair = 0;
        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            string joined = workFolder + Path.DirectorySeparatorChar + i + "_A" + pair + ".pdf";
            string front = parts[i];
            string back = parts[i + 1];
            Progress(0, "Join PDF File");
            OpenFile(joined);
            CopyPages(front);
            CopyPages(back);
            WriteClosingInfo();
            CloseFile(joined);
            DeleteQuietly(front);
            if (!_layout.HasSharedBack)
            {
                DeleteQuietly(back);
            }

            pairs.Add(joined);
            pair++;
        }

        DeleteQuietly(sharedBack);
        return [.. pairs];
    }

    private int CopyPages(string file)
    {
        using var source = new PdfDocument(new PdfReader(file));
        int pages = source.GetNumberOfPages();
        source.CopyPagesTo(1, pages, _pdf);
        return pages;
    }

    /// <summary>
    /// Joins files into <paramref name="destination"/>. With <paramref name="split"/> a new
    /// document (<c>name_1.pdf</c>, <c>name_2.pdf</c>, …) is started whenever the next file
    /// would take the current one to <paramref name="maximumMegabytes"/> or more. Returns the
    /// documents written. Original: <c>UnirPDF</c>.
    /// </summary>
    private List<string> Join(string destination, string[] sources, bool split, int maximumMegabytes)
    {
        string stem = Path.GetDirectoryName(destination) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(destination);
        var written = new List<string> { destination };
        double size = 0.0;
        int extra = 0;
        int done = 0;
        Progress(0, "Join PDF File");
        OpenFile(destination);
        foreach (string source in sources)
        {
            if (string.IsNullOrEmpty(source))
            {
                continue;
            }

            double megabytes = new FileInfo(source).Length / 1000000.0;

            // The original also split before the first file when that file alone reached the
            // limit, leaving a first document with nothing but a blank page.
            if (split && done > 0 && size + megabytes >= maximumMegabytes)
            {
                WriteClosingInfo();
                CloseFile(null);
                extra++;
                string next = stem + "_" + extra + ".pdf";
                written.Add(next);
                OpenFile(next);
                size = megabytes;
            }
            else
            {
                size += megabytes;
            }

            CopyPages(source);
            done++;
            Progress(Convert.ToInt32((double)done / sources.Length * 100.0), StringResources.Get("unir_pdf") + " " + done + "/" + sources.Length);
        }

        WriteClosingInfo();
        CloseFile(null);
        return written;
    }

    /// <summary>
    /// Joins the sheets of fronts with the shared back after each of them. The back is drawn
    /// once and reused. With a back per card position the last sheet gets a back sheet of its
    /// own, holding backs only behind the cards that sheet has. Original: <c>UnirPDFMismaTrasera</c>.
    /// </summary>
    private void JoinWithSharedBack(string destination, string[] sources)
    {
        if (sources.Length == 0 || sources[0] == null)
        {
            return;
        }

        int sheets = 0;
        int done = 0;
        Progress(0, "Join PDF File");
        OpenFile(destination);
        PageSize pageSize = PageSizeOfSheet();
        PdfPage formPage = _pdf!.AddNewPage(pageSize);
        var form = new PdfFormXObject(formPage);
        _layout.CurrentFace = FaceType.Back;
        BackImageMode mode = _print.BackImage;
        foreach (string source in sources)
        {
            if (done == 0)
            {
                form = FillSharedBackForm(mode, _pdf.GetNumberOfPages(), formPage);
            }

            if (source == null)
            {
                continue;
            }

            sheets += CopyPages(source);
            var canvas = new PdfCanvas(_pdf.AddNewPage(pageSize));
            canvas.AddXObject(form);
            canvas.Release();
            sheets++;
            done++;
            Progress(Convert.ToInt32((double)done / sources.Length * 100.0), StringResources.Get("unir_pdf") + " " + done + "/" + sources.Length);
        }

        bool ownLastBack = mode is BackImageMode.SameBack or BackImageMode.DifferentBack;
        if (ownLastBack)
        {
            _pdf.RemovePage(sheets + 1);
        }

        WriteClosingInfo();
        _pdf.RemovePage(1);
        CloseFile(null);
        if (ownLastBack)
        {
            WriteLastSharedBackSheet();
            string joined = destination + "_2";
            Join(joined, [destination, _sharedBackLastPageFile], split: false, 0);
            File.Delete(_sharedBackLastPageFile);
            File.Move(joined, destination, overwrite: true);
        }
    }
}
