using System.Security.Cryptography;
using System.Text;
using ImageMagick;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Core.Templates;
using PnpTool.Imaging;

namespace PnpTool.Pdf.Layout;

/// <summary>Which table of positions is meant. Original: <c>QueArrayPosiciones</c>.</summary>
public enum PositionTable
{
    Fronts,
    Backs,
}

/// <summary>
/// Everything about laying a project out on sheets that does not depend on the PDF library:
/// where each cell goes, which cell comes next, how each image is prepared, and the texts of
/// the footer. The PDF writer drives it. Original: <c>FicheroPdfMaquetadoNoDependienteLibreria</c>.
/// </summary>
/// <remarks>
/// Positions are measured from the top-left corner of the sheet, as on screen;
/// <see cref="FlipPositionsForPdf"/> turns a table into PDF coordinates, which start at the
/// bottom-left. All arithmetic is done in points, as in the original.
/// </remarks>
public class PageLayout
{
    /// <summary>Original: <c>configuracion</c>.</summary>
    public ProjectSettings Settings { get; }

    /// <summary>Original: <c>configuracionPlantillaAvanzada</c>.</summary>
    public AdvancedTemplate Template { get; }

    /// <summary>Folder where the parts of the PDF are written. Original: <c>RutaTemporal</c>.</summary>
    public string WorkFolder { get; }

    /// <summary>Original: <c>IDDocumento</c>.</summary>
    public string DocumentId { get; }

    /// <summary>Start of the names of the part files: work folder plus the name of the output PDF. Original: <c>RutaDocumentos</c>.</summary>
    public string PartFilePrefix { get; }

    /// <summary>Size of the footer text in points. Original: <c>TamanotextoPDF</c>.</summary>
    public int FooterTextSize { get; } = 8;

    /// <summary>The cells of the document. Original: <c>ListadoImagenes</c>.</summary>
    public PdfImage[] Images { get; set; }

    /// <summary>Column and row of the object being placed on the current sheet. Original: <c>ColumnaFila_actual</c>.</summary>
    public PointI Cell { get; set; } = new();

    /// <summary>Original: <c>HojaActual</c>.</summary>
    public int CurrentSheet { get; set; }

    /// <summary>Original: <c>HojasTotales</c>.</summary>
    public int TotalSheets { get; set; }

    /// <summary>Size of one object of the grid: a card, or a whole dice net. Original: <c>Tamano_Marco</c>.</summary>
    public UnitPoint ObjectSize { get; set; } = new();

    /// <summary>Size at which back images are drawn. Original: <c>Tamano_Carta_En_PDF_Trasera</c>.</summary>
    public UnitPoint BackImageSize { get; private set; }

    /// <summary>Top-left corner of the grid on the sheet. Original: <c>PosicionEnHojaPDFInicial</c>.</summary>
    public UnitPoint GridOrigin { get; set; }

    /// <summary>Position of each cell of a sheet of fronts, by column and row. Original: <c>ArrayPosicionesFrontales</c>.</summary>
    public UnitPoint[,] FrontPositions { get; set; } = new UnitPoint[1, 1];

    /// <summary>Original: <c>ArrayPosicionesTraseras</c>.</summary>
    public UnitPoint[,] BackPositions { get; set; } = new UnitPoint[1, 1];

    /// <summary>Size of the whole grid, spacing included. Original: <c>Tamano_MarcoExterior</c>.</summary>
    public UnitPoint GridSize { get; set; } = new();

    /// <summary>Face of the sheet being written. Original: <c>CaraHojaActual</c>.</summary>
    public FaceType CurrentFace { get; set; } = FaceType.Front;

    /// <summary>Position of the cell being written. Original: <c>PosicionEnHojaPDF</c>.</summary>
    public UnitPoint Position { get; set; } = new();

    /// <summary>Area at the edge of the sheet the printer cannot reach. Original: <c>Margen_Seguridad</c>.</summary>
    public UnitPoint SafetyMargin { get; set; } = new();

    /// <summary>Every card has the same back, written once. Original: <c>EsTraseraIgual</c>.</summary>
    public bool HasSharedBack { get; set; }

    /// <summary>Size at which the image of the current cell is drawn. Original: <c>TamanoDestino</c>.</summary>
    public UnitPoint TargetSize { get; set; }

    private readonly PointI _cellsPerObject;
    private readonly UnitPoint _frontImageSize;
    private PointI _cellInObject = new();
    private ArgbColor _frontColor;
    private ArgbColor _backColor;
    private ArgbColor _currentColor;
    private bool _hasEffect;
    private ImageEffectType _effect = ImageEffectType.None;
    private int _effectSize;
    private MagickImage _watermark;

    public PageLayout(PdfImage[] images, string documentId, ProjectSettings settings, string workFolder, AdvancedTemplate template)
    {
        Log.Enter(nameof(PageLayout));
        Settings = settings;
        Template = template;
        Images = images;
        WorkFolder = workFolder;
        PartFilePrefix = workFolder + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(settings.InputParameters.OutputPdf);
        DocumentId = documentId;
        TotalSheets = Images.Length / settings.PrintLayout.CardsPerSheet;
        _cellsPerObject = CellsPerObject(settings.Group.Group);
        ObjectSize.X = (double)settings.Grid.CardSize.X * _cellsPerObject.X;
        ObjectSize.Y = (double)settings.Grid.CardSize.Y * _cellsPerObject.Y;
        DeletePartFiles(workFolder);
        _frontImageSize = ImageSize(settings.Front.Effect, settings.Front.Size);
        BackImageSize = ImageSize(settings.Back.Effect, settings.Back.Size);
        GridOrigin = ComputeGridOrigin();
        if (IsGutterfold(settings.PrintLayout.DoubleSided, settings.PrintLayout.BackImage))
        {
            ComputeGutterfoldPositions();
        }
        else
        {
            ComputePositions(FaceType.Front);
        }

        _frontColor = ArgbColor.Parse(settings.Front.PaintBackground ? settings.Front.BackgroundFillColor : AppSettings.ColorWhite);
        _backColor = ArgbColor.Parse(settings.Back.PaintBackground ? settings.Back.BackgroundFillColor : AppSettings.ColorWhite);
        _currentColor = _frontColor;

        // The original zeroed the margin for the printing company's styles; that group is not offered here.
        SafetyMargin = new UnitPoint(AppSettings.PrinterMargin);

        PrintLayout print = settings.PrintLayout;
        GridSize.X = ((double)settings.Grid.CardSize.X * print.Columns) + ((print.Columns - 1) * (double)settings.Grid.Spacing.X);
        GridSize.Y = ((double)settings.Grid.CardSize.Y * print.Rows) + ((print.Rows - 1) * (double)settings.Grid.Spacing.Y);
        HasSharedBack = print.BackImage is BackImageMode.SameBack or BackImageMode.OnlyOne or BackImageMode.Pattern;
        TargetSize = new UnitPoint(_frontImageSize.X.Point, _frontImageSize.Y.Point);
        _watermark = LoadWatermark(_frontImageSize.X.Point, _frontImageSize.Y.Point);
        Log.Exit(nameof(PageLayout));
    }

    /// <summary>
    /// A project printed on one side with backs is a gutterfold: fronts and backs alternate by
    /// row on the same sheet. Original: <c>FuncionesGenerales.SetupEsGlutter</c>.
    /// </summary>
    public static bool IsGutterfold(bool doubleSided, BackImageMode backImage) =>
        !doubleSided && backImage != BackImageMode.NoBack;

    /// <summary>
    /// Columns and rows of cells that make one object of the grid: 3 × 5 for a dice net, 1 × 1
    /// for everything else. Original: <c>EstableceFilasColumnasEnObjeto</c>.
    /// </summary>
    public static PointI CellsPerObject(SettingsGroup group) =>
        group == SettingsGroup.Dice ? new PointI(3, 5) : new PointI(1, 1);

    /// <summary>
    /// A name for the PDF that changes every second. Original: <c>DevuelveHashUnicoPDF</c>.
    /// </summary>
    public static string UniqueHash(string name) => UniqueHash(name, DateTime.Now);

    public static string UniqueHash(string name, DateTime time)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes("^PNPTool" + name + time.ToString("yyyyMMdd_HHmmss")));
        return Convert.ToHexStringLower(hash);
    }

    private static void DeletePartFiles(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(folder, "*.pdf"))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    // ------------------------------------------------------------ images

    /// <summary>Loads the watermark scaled to the given size in points. Original: <c>CargaMarcaAguaAimagen</c>.</summary>
    private MagickImage LoadWatermark(double width, double height)
    {
        if (Settings.Watermark.Path == string.Empty)
        {
            return new MagickImage();
        }

        var image = new MagickImage(Settings.Watermark.Path);
        image.ResetPage();
        image.FilterType = Settings.Other.ResizeFilter;
        image.Resize(Convert.ToUInt32(width), Convert.ToUInt32(height));
        return image;
    }

    /// <summary>
    /// Prepares an image for the PDF: colour space, compression by resizing, rotation, the
    /// effect of the current face and the watermark laid over each image.
    /// Original: <c>CargarImagenNuevaAPDF(MagickImage, …)</c>.
    /// </summary>
    public MagickImage PrepareImage(MagickImage image, bool isCmyk, RotationProperties rotation, IColorProfile? profile, int? cellIndex, bool pdfX)
    {
        ColorManagement.PrepareForPdf(image, isCmyk, profile, Settings.Other, pdfX);
        switch (Settings.Other.Compression)
        {
            case CompressionType.ImageToSize:
                ImageTransforms.ResizeToPrintedSize(image, Settings.Grid.CardSize, Settings.Other.ResizeFilter);
                break;
            case CompressionType.Percentage:
                ImageTransforms.ResizeByPercent(image, Settings.Other.CompressionPercent, Settings.Other.ResizeFilter);
                break;
        }

        image = ImageTransforms.Rotate(image, rotation);
        if (_hasEffect)
        {
            image = ImageEffects.Apply(image, _effect, _effectSize, _currentColor, Settings, CurrentFace).Image;
        }

        if (Settings.Watermark.Type != WatermarkType.OnImage)
        {
            return image;
        }

        if (Settings.Group.ProjectType == ProjectType.AdvancedStyles)
        {
            AdvancedTemplateCard place = Template.Cards[(cellIndex ?? 0) % Template.Cards.Length];
            _watermark.Dispose();
            _watermark = LoadWatermark(place.Size.X.Point, place.Size.Y.Point);
        }

        return ImageTransforms.OverlayWatermark(image, _watermark, Settings.Other.ResizeFilter);
    }

    /// <summary>The image of a cell, prepared. A missing file gives the "not found" image. Original: <c>CargarImagenNuevaAPDF(int, bool)</c>.</summary>
    public MagickImage PrepareImage(int index, bool pdfX)
    {
        PdfImage cell = Images[index];
        var image = new MagickImage(StockImages.OrNotFound(cell.Path).Path);
        image.ResetPage();
        return PrepareImage(image, cell.IsCmyk, cell.Rotation, cell.ColorProfile, index, pdfX);
    }

    /// <summary>An image file that is not a cell, prepared with an extra rotation. Original: <c>CargarImagenNuevaAPDF(string, …)</c>.</summary>
    public MagickImage PrepareImage(string path, RotationProperties extraRotation, bool pdfX)
    {
        var image = new MagickImage(path);
        image.ResetPage();
        CardFace face = MagickImageInspector.GetImageProperties(path, null);
        face.Rotation.Combine(extraRotation);
        return PrepareImage(image, ImageFormats.IsCmyk(path), face.Rotation, face.ColorProfile, null, pdfX);
    }

    /// <summary>Original: <c>ImagenAguaDispose</c>.</summary>
    public void DisposeWatermark() => _watermark.Dispose();

    /// <summary>
    /// The size an image is drawn at: the face's size, or the whole cell when the mirror
    /// effect fills the difference. Original: <c>DevuelveTamanoImagen</c>.
    /// </summary>
    private UnitPoint ImageSize(ImageEffectType effect, UnitPoint faceSize)
    {
        UnitPoint source = effect == ImageEffectType.Mirror ? Settings.Grid.CardSize : faceSize;
        return new UnitPoint { X = source.X, Y = source.Y };
    }

    /// <summary>
    /// Sets the face and image size for the current sheet: with different backs every second
    /// sheet is a sheet of backs. Original: <c>EstableceTamanoImagen</c>.
    /// </summary>
    public void SetFaceForCurrentSheet()
    {
        if (Settings.PrintLayout.BackImage == BackImageMode.DifferentBack && (CurrentSheet + 1) % 2 == 0)
        {
            CurrentFace = FaceType.Back;
            TargetSize = new UnitPoint(Unit.FromMillimeter(BackImageSize.X.Millimeter), Unit.FromMillimeter(BackImageSize.Y.Millimeter));
        }
        else
        {
            CurrentFace = FaceType.Front;
            TargetSize = new UnitPoint(Unit.FromMillimeter(_frontImageSize.X.Millimeter), Unit.FromMillimeter(_frontImageSize.Y.Millimeter));
        }
    }

    /// <summary>Selects the effect, its size and the background colour of a face. Original: <c>ActualizaEfectoLocal</c>.</summary>
    public void SetEffectFor(FaceType face)
    {
        _hasEffect = false;
        FaceSettings? faceSettings = face switch
        {
            FaceType.Front => Settings.Front,
            FaceType.Back => Settings.Back,
            _ => null,
        };
        if (faceSettings == null || faceSettings.Effect == ImageEffectType.None)
        {
            return;
        }

        _hasEffect = true;
        _effect = faceSettings.Effect;
        _currentColor = face == FaceType.Front ? _frontColor : _backColor;
        if (ImageEffects.HasSize(_effect))
        {
            _effectSize = faceSettings.RoundedCornerSize;
        }
    }

    // ------------------------------------------------------------ positions

    /// <summary>Loads <see cref="Position"/> with the place of the current cell. Original: <c>EstablecePosicionPintarPDF</c>.</summary>
    public void SetPositionForCurrentCell()
    {
        if (CurrentFace == FaceType.Front || Settings.Group.ProjectType == ProjectType.Gutterfold)
        {
            int column = _cellInObject.X + (_cellsPerObject.X * Cell.X);
            int row = _cellInObject.Y + (Cell.Y * _cellsPerObject.Y);
            Position = new UnitPoint(FrontPositions[column, row]);
        }
        else
        {
            Position = new UnitPoint(BackPositions[Cell.X, Cell.Y]);
        }
    }

    /// <summary>Centres the grid on the sheet. Original: <c>EstablecePosicionInicialPintarPDF</c>.</summary>
    private UnitPoint ComputeGridOrigin()
    {
        PrintLayout print = Settings.PrintLayout;
        Unit y = ((double)print.Paper.Size.Y - ((double)ObjectSize.Y * print.Rows) - ((double)Settings.Grid.Spacing.Y * (print.Rows - 1))) / 2.0;
        Unit x = ((double)print.Paper.Size.X - ((double)ObjectSize.X * print.Columns) - ((double)Settings.Grid.Spacing.X * (print.Columns - 1))) / 2.0;
        return new UnitPoint(x, y);
    }

    /// <summary>In a gutterfold, odd rows hold backs. Original: <c>DevuelveCaraActualSegunFila</c>.</summary>
    public FaceType FaceForCurrentRow() => Cell.Y % 2 != 0 ? FaceType.Back : FaceType.Front;

    /// <summary>Fills the table of positions of a face. Original: <c>DevuelvePosicionesPDF_V2</c>.</summary>
    public void ComputePositions(FaceType face)
    {
        if (Settings.Group.ProjectType != ProjectType.AdvancedStyles)
        {
            ComputeGridPositions(face);
        }
        else
        {
            ComputeTemplatePositions(face);
        }
    }

    /// <summary>
    /// Positions from an advanced template: its places as drawn for fronts; for backs mirrored
    /// across the sheet and moved by the back offset. Original: <c>DevuelvePosicionesPDF_V2_EstiloAvanzado</c>.
    /// </summary>
    private void ComputeTemplatePositions(FaceType face)
    {
        var table = new UnitPoint[Template.Cards.Length, 1];
        for (int i = 0; i < Template.Cards.Length; i++)
        {
            AdvancedTemplateCard place = Template.Cards[i];
            if (face == FaceType.Front)
            {
                table[i, 0] = new UnitPoint(place.Location);
                continue;
            }

            table[i, 0] = new UnitPoint
            {
                X = (double)Settings.PrintLayout.Paper.Size.X - (double)place.Location.X - (double)place.Size.X,
                Y = place.Location.Y,
            };
            if (face == FaceType.Back)
            {
                table[i, 0].X = (double)table[i, 0].X + (double)Settings.BackOffset.X;
                table[i, 0].Y = (double)table[i, 0].Y + (double)Settings.BackOffset.Y;
            }
        }

        if (face == FaceType.Front)
        {
            FrontPositions = table;
        }
        else
        {
            BackPositions = table;
        }
    }

    /// <summary>Original: <c>DevuelvePosicionesPDF_V2_Glutter</c>.</summary>
    private void ComputeGutterfoldPositions()
    {
        int rows = Settings.PrintLayout.Rows * _cellsPerObject.Y;
        int columns = Settings.PrintLayout.Columns * _cellsPerObject.X;
        int rowsDone = 0;
        FaceType face = FaceType.Front;
        var table = new UnitPoint[columns, rows];
        while (rowsDone < rows)
        {
            int column = _cellInObject.X + (_cellsPerObject.X * Cell.X);
            int row = _cellInObject.Y + (Cell.Y * _cellsPerObject.Y);
            UnitPoint position = PositionOfCurrentCell(face, centreInCell: false);
            table[column, row] = new UnitPoint { X = position.X, Y = position.Y };
            if (Advance().RowChanged)
            {
                rowsDone++;
            }

            face = FaceForCurrentRow();
        }

        FrontPositions = table;
    }

    /// <summary>Original: <c>DevuelvePosicionesPDF_V2_Normal</c>.</summary>
    private void ComputeGridPositions(FaceType face)
    {
        int rows = Settings.PrintLayout.Rows * _cellsPerObject.Y;
        int columns = Settings.PrintLayout.Columns * _cellsPerObject.X;
        bool sheetDone = false;
        var table = new UnitPoint[columns, rows];
        while (!sheetDone)
        {
            int column = _cellInObject.X + (_cellsPerObject.X * Cell.X);
            int row = _cellInObject.Y + (Cell.Y * _cellsPerObject.Y);
            UnitPoint position = PositionOfCurrentCell(face, centreInCell: true);
            table[column, row] = new UnitPoint { X = position.X, Y = position.Y };
            sheetDone = Advance().SheetDone;
        }

        if (face == FaceType.Front)
        {
            FrontPositions = table;
        }
        else
        {
            BackPositions = table;
        }
    }

    /// <summary>
    /// Moves to the next cell: across the cells of an object, then to the next object of the
    /// row, then to the next row. Reports whether a row of objects was completed and whether
    /// that was the last one of the sheet, in which case the next cell is the first of a new
    /// sheet. Original: <c>ActualizaFilaColumna_V2</c>.
    /// </summary>
    public (bool RowChanged, bool SheetDone) Advance()
    {
        if (_cellInObject.X < _cellsPerObject.X - 1)
        {
            _cellInObject.X++;
            return (false, false);
        }

        _cellInObject.X = 0;
        if (_cellInObject.Y < _cellsPerObject.Y - 1)
        {
            _cellInObject.Y++;
            return (false, false);
        }

        _cellInObject.Y = 0;
        if (Cell.X < Settings.PrintLayout.Columns - 1)
        {
            Cell.X++;
            return (false, false);
        }

        Cell.X = 0;
        Cell.Y++;
        if (Cell.Y > Settings.PrintLayout.Rows - 1)
        {
            Cell.Y = 0;
            Cell.X = 0;
            return (true, true);
        }

        return (true, false);
    }

    /// <summary>
    /// In a gutterfold, switches image size and effect between fronts and backs when a new row
    /// starts. Original: <c>CambiaFila</c>.
    /// </summary>
    public void OnRowChanged(bool changed)
    {
        if (!changed)
        {
            return;
        }

        if (Cell.Y % 2 != 0)
        {
            TargetSize = new UnitPoint(BackImageSize.X.Point, BackImageSize.Y.Point);
            SetEffectFor(FaceType.Back);
        }
        else
        {
            TargetSize = new UnitPoint(_frontImageSize.X.Point, _frontImageSize.Y.Point);
            SetEffectFor(FaceType.Front);
        }
    }

    /// <summary>
    /// Where the current cell goes on a sheet of the given face. With
    /// <paramref name="centreInCell"/> an image smaller than its cell is centred in it.
    /// Original: <c>DevuelvePosicionPDF</c>.
    /// </summary>
    /// <remarks>
    /// As in the original, backs are moved by the horizontal back offset whatever its sign but
    /// by the vertical one only when it is positive.
    /// </remarks>
    public UnitPoint PositionOfCurrentCell(FaceType face, bool centreInCell)
    {
        var position = new UnitPoint();
        Grid grid = Settings.Grid;
        if (Cell.X == 0 && _cellInObject.X == 0)
        {
            position.X = GridOrigin.X;
        }
        else
        {
            position.X = (double)GridOrigin.X + (((double)ObjectSize.X + (double)grid.Spacing.X) * Cell.X) + ((double)grid.CardSize.X * _cellInObject.X);
        }

        if (face == FaceType.Back && Settings.BackOffset.X.Point != 0.0)
        {
            position.X = (double)position.X + (double)Settings.BackOffset.X;
        }

        if (Cell.Y == 0 && _cellInObject.Y == 0)
        {
            position.Y = GridOrigin.Y;
        }
        else
        {
            position.Y = (double)GridOrigin.Y + (((double)ObjectSize.Y + (double)grid.Spacing.Y) * Cell.Y) + ((double)grid.CardSize.Y * _cellInObject.Y);
        }

        if (face == FaceType.Back && Settings.BackOffset.Y.Point > 0.0)
        {
            position.Y = (double)position.Y + (double)Settings.BackOffset.Y;
        }

        return centreInCell ? CentreInCell(face, position, Settings) : position;
    }

    /// <summary>Position and size of a place of the advanced template. Original: <c>Posicion_ModificacionPorEscala_EstiloAvanzado</c>.</summary>
    public (UnitPoint Position, UnitPoint Size) TemplatePlace(int index, UnitPoint position) =>
        (position, Template.Cards[index % Template.Cards.Length].Size);

    /// <summary>
    /// Moves a position so that a face smaller (or larger) than the cell is centred in it. No
    /// change with the mirror effect, which fills the cell. Original: <c>Posicion_ModificacionPorEscala</c>.
    /// </summary>
    public static UnitPoint CentreInCell(FaceType face, UnitPoint position, ProjectSettings settings)
    {
        FaceSettings faceSettings = face == FaceType.Front ? settings.Front : settings.Back;
        UnitPoint cell = settings.Grid.CardSize;
        if (cell.X != faceSettings.Size.X && faceSettings.Effect != ImageEffectType.Mirror)
        {
            position.X = (double)position.X + (((double)cell.X - (double)faceSettings.Size.X) / 2.0);
        }

        if (cell.Y != faceSettings.Size.Y && faceSettings.Effect != ImageEffectType.Mirror)
        {
            position.Y = (double)position.Y + (((double)cell.Y - (double)faceSettings.Size.Y) / 2.0);
        }

        return position;
    }

    /// <summary>
    /// Turns the vertical positions of a table from "down from the top" into "up from the
    /// bottom", the bottom edge of each image, as PDF wants. Original: <c>InvierteArrayPosiciones_Y_IText</c>.
    /// </summary>
    public void FlipPositionsForPdf(PositionTable which)
    {
        UnitPoint[,] table = FrontPositions;
        FaceSettings face = Settings.Front;
        if (which == PositionTable.Backs)
        {
            table = BackPositions;
            face = Settings.Back;
        }

        Unit height = face.Effect == ImageEffectType.Mirror ? Settings.Grid.CardSize.Y : face.Size.Y;
        int rows = Settings.PrintLayout.Rows * _cellsPerObject.Y;
        int columns = Settings.PrintLayout.Columns * _cellsPerObject.X;
        bool template = Settings.Group.ProjectType == ProjectType.AdvancedStyles;
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                if (template)
                {
                    height = Template.Cards[column].Size.Y;
                }

                table[column, row].Y = Unit.FromMillimeter(Settings.PrintLayout.Paper.Size.Y.Millimeter - table[column, row].Y.Millimeter - height.Millimeter);
            }
        }
    }

    // ------------------------------------------------------------ footer and frame

    /// <summary>Signature and page number for the foot of a sheet, or empty. Original: <c>Hay_TextoAbajo</c>.</summary>
    public string FooterText(FaceType face, int pageNumber)
    {
        string text = string.Empty;
        if (!string.IsNullOrEmpty(Settings.HeaderFooter.SignatureText))
        {
            text = Settings.HeaderFooter.SignatureText;
        }

        if (Settings.HeaderFooter.ShowPageNumber)
        {
            if (text != string.Empty)
            {
                text += " ";
            }

            text += PageNumberText(face, pageNumber);
        }

        return text;
    }

    /// <summary>
    /// Cuts a footer text that would not fit between the margins. Original:
    /// <c>DibujaPiePaginaTextoAbajo_AjustaTexto</c>, whose loop was evidently meant to break the
    /// text into lines but keeps only its beginning and then fails on what is left; here the
    /// text is cut to the first line, which is what the original showed when it did not fail.
    /// </summary>
    public string FitFooterText(string text, UnitPoint paperSize)
    {
        double perCharacter = Settings.Font.Size / 120.0;
        double available = paperSize.X.Inch - (2.0 * SafetyMargin.X.Inch);
        if (text.Length * perCharacter > available)
        {
            int perLine = Math.Max(0, Convert.ToInt32(available / perCharacter));
            text = text[..Math.Min(perLine, text.Length)];
        }

        return text;
    }

    /// <summary>
    /// The number printed on a sheet: backs take the number of their front followed by "B", or
    /// "BACK" for a shared back; books bound on the right count down. Original:
    /// <c>DibujaPiePaginaCalculaNumeroPagina</c>.
    /// </summary>
    public string PageNumberText(FaceType face, int pageNumber)
    {
        PrintLayout print = Settings.PrintLayout;
        int first = Settings.HeaderFooter.FirstPageNumber;
        bool differentBacks = print.BackImage == BackImageMode.DifferentBack;
        if (face == FaceType.Back)
        {
            return differentBacks ? ((pageNumber / 2) + first) + "B" : "BACK";
        }

        int number;
        if (print.DoubleSided)
        {
            number = differentBacks ? (pageNumber / 2) + first : pageNumber + 1;
        }
        else if (Settings.Group.Group == SettingsGroup.Books && Settings.Book.Spine == BookSpineType.Right)
        {
            number = Images.Length - pageNumber + (first - 1);
        }
        else
        {
            number = pageNumber + first;
        }

        return number.ToString();
    }

    /// <summary>Original: <c>PintaMarcoExteriorQueTipo</c>.</summary>
    public OuterFrameEffect OuterFrameEffectFor(FaceType face) =>
        face != FaceType.Back ? Settings.Front.OuterFrameEffect : Settings.Back.OuterFrameEffect;

    /// <summary>Whether sheets of this face get an outer frame. Original: <c>PintaMarcoExterior(cara)</c>.</summary>
    public bool HasOuterFrame(FaceType face) =>
        (face == FaceType.Back && Settings.Back.ShowOuterFrame) || (face == FaceType.Front && Settings.Front.ShowOuterFrame);

    /// <summary>
    /// The same, moving the frame's position by the back offset for backs (in PDF coordinates,
    /// so the vertical offset is subtracted). Original: <c>PintaMarcoExterior(cara, ref posición)</c>.
    /// </summary>
    public bool HasOuterFrame(FaceType face, UnitPoint position)
    {
        if (face == FaceType.Back && Settings.Back.ShowOuterFrame)
        {
            position.X = (double)position.X + (double)Settings.BackOffset.X;
            position.Y = (double)position.Y - (double)Settings.BackOffset.Y;
            return true;
        }

        return face == FaceType.Front && Settings.Front.ShowOuterFrame;
    }

    // ------------------------------------------------------------ shared back

    /// <summary>
    /// When the parts of the document are joined with a shared back, puts the back's file
    /// after every part and marks the project as having no back, so it is not added again.
    /// Original: <c>TerminarCrearPDFTamanoMaximoUnirFrontalTrasera</c>.
    /// </summary>
    public string[] InterleaveSharedBack(string[] parts, string sharedBackFile)
    {
        if (!HasSharedBack)
        {
            return parts;
        }

        var interleaved = new string[parts.Length * 2];
        for (int i = 0; i < parts.Length; i++)
        {
            interleaved[i * 2] = parts[i];
            interleaved[(i * 2) + 1] = sharedBackFile;
        }

        Settings.PrintLayout.BackImage = BackImageMode.NoBack;
        return interleaved;
    }

    /// <summary>
    /// Replaces the cells by one sheet of the shared back. Original:
    /// <c>GeneraListadoImagenesTraseraUnica</c>, which indexed the cells by column × columns +
    /// row and so only filled the sheet correctly when rows and columns were equal; here every
    /// cell of the sheet is filled.
    /// </summary>
    public void UseSharedBackSheet()
    {
        int count = Settings.PrintLayout.Rows * Settings.PrintLayout.Columns;
        Images = new PdfImage[count];
        for (int i = 0; i < count; i++)
        {
            Images[i] = new PdfImage(Settings.SharedBack) { SameAsPrevious = i != 0 };
        }
    }
}
