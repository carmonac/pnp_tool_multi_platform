using ImageMagick;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Imaging;
using PnpTool.Pdf.Layout;

namespace PnpTool.Pdf.Tabletop;

/// <summary>
/// Writes the cards of a project as the sheets Tabletop Simulator makes decks from: images
/// with the cards side by side in a table, all of the same size, the fronts in one image and
/// the backs in another. Original: <c>FormTabletopModelo</c>.
/// </summary>
/// <remarks>
/// <para>
/// The original also used this to make the picture behind a sheet with mirrored outer
/// frames (<c>CrearTabletop(RellenaImagenVacia: true, …)</c>); that is done elsewhere in the
/// port (task 13), so the parts that only served it are not here.
/// </para>
/// <para>
/// Each card takes the size of the project's card at the target resolution of the program.
/// With the restrictions of Tabletop Simulator the table has 2 to 10 columns and 2 to 7
/// rows, and a sheet larger than 8980 × 6286 pixels is made smaller to fit.
/// </para>
/// </remarks>
public sealed class TabletopSheets
{
    /// <summary>The widest sheet Tabletop Simulator is given. Original: <c>ConstX</c>.</summary>
    public const int MaximumWidth = 8980;

    /// <summary>The tallest sheet Tabletop Simulator is given. Original: <c>ConstY</c>.</summary>
    public const int MaximumHeight = 6286;

    private readonly IReadOnlyList<PdfImage?> _images;
    private readonly bool _twoFaces;
    private readonly UnitPoint _cardSize;
    private readonly OtherSettings _other;

    /// <param name="images">The faces to lay out, in order: fronts only, or front and back in turn. Original: <c>Listado_Imagenes</c>.</param>
    /// <param name="twoFaces">Whether the images are fronts and backs in turn.</param>
    /// <param name="settings">The settings of the project, for the size of the card, the filter and the colour space.</param>
    public TabletopSheets(IReadOnlyList<PdfImage?> images, bool twoFaces, ProjectSettings settings)
    {
        _images = images;
        _twoFaces = twoFaces;
        _cardSize = new UnitPoint(settings.Grid.CardSize);
        _other = settings.Other;
    }

    public int Columns { get; set; } = 10;

    public int Rows { get; set; } = 7;

    /// <summary>Whether the limits of Tabletop Simulator are kept. Original: <c>AplicarRestricciones</c>.</summary>
    public bool ApplyRestrictions { get; set; } = true;

    /// <summary>How many cards there are: half the images when they are fronts and backs.</summary>
    public int CardCount => _twoFaces ? _images.Count / 2 : _images.Count;

    /// <summary>How many sheets of fronts will be written.</summary>
    public int SheetCount => Columns < 1 || Rows < 1 ? 0 : (CardCount + (Columns * Rows) - 1) / (Columns * Rows);

    /// <summary>A number of columns as the restrictions allow it: 2 to 10. Original: <c>ValidaRestriccion_X</c>.</summary>
    public static int RestrictColumns(int columns, bool restrict) => restrict ? Math.Clamp(columns, 2, 10) : Math.Max(columns, 0);

    /// <summary>A number of rows as the restrictions allow it: 2 to 7. Original: <c>ValidaRestriccion_Y</c>.</summary>
    public static int RestrictRows(int rows, bool restrict) => restrict ? Math.Clamp(rows, 2, 7) : Math.Max(rows, 0);

    /// <summary>
    /// The size in pixels each card gets on the sheet.
    /// Original: the start of <c>CrearTabletop</c> with <c>AplicaReduccion</c>.
    /// </summary>
    /// <remarks>
    /// The original made a sheet that was only too wide narrower without making it lower, so
    /// its cards came out squeezed; here both sides shrink by the same amount.
    /// </remarks>
    public PointI CellSize()
    {
        double width = AppSettings.TargetDpi * _cardSize.X.Inch;
        double height = AppSettings.TargetDpi * _cardSize.Y.Inch;
        double scale = 1.0;
        if (ApplyRestrictions)
        {
            double sheetWidth = Math.Ceiling(Columns * width);
            double sheetHeight = Math.Ceiling(Rows * height);
            scale = Math.Min(1.0, Math.Min(MaximumWidth / sheetWidth, MaximumHeight / sheetHeight));
        }

        return new PointI((int)Math.Floor(width * scale), (int)Math.Floor(height * scale));
    }

    /// <summary>The size in pixels of a sheet.</summary>
    public PointI SheetSize()
    {
        PointI cell = CellSize();
        return new PointI(cell.X * Columns, cell.Y * Rows);
    }

    /// <summary>
    /// Writes the sheets as <c>base_0_front.ext</c>, <c>base_0_back.ext</c>, <c>base_1_front.ext</c>…
    /// and gives their paths. The kind of file is that of the first image of the cards, or
    /// PNG when there is none. Original: <c>CrearTabletop</c> and <c>SalvarImagenes</c>.
    /// </summary>
    /// <param name="basePath">Folder and beginning of the names of the files.</param>
    /// <param name="report">Told how many images are done.</param>
    public List<string> Write(string basePath, Action<int>? report = null, CancellationToken cancellation = default)
    {
        Log.Enter(nameof(Write));
        var written = new List<string>();
        PointI cell = CellSize();
        if (Columns < 1 || Rows < 1 || cell.X < 1 || cell.Y < 1 || _images.Count == 0)
        {
            Log.Exit(nameof(Write));
            return written;
        }

        // The original named the files after the first entry of the list, which for a blank
        // card is a colour and has no extension.
        string? firstFile = _images.FirstOrDefault(image => image is { Kind: ImageKind.Normal } && File.Exists(image.Path))?.Path;
        string extension = firstFile == null ? ".png" : Path.GetExtension(firstFile);
        IColorProfile? profile = null;
        if (firstFile != null)
        {
            using var first = new MagickImage();
            first.Ping(firstFile);
            profile = first.GetColorProfile();
        }

        profile ??= IccProfiles.TryLoad(AppSettings.RgbIccPath);

        uint sheetWidth = (uint)(cell.X * Columns);
        uint sheetHeight = (uint)(cell.Y * Rows);
        MagickImage? fronts = null;
        MagickImage? backs = null;
        try
        {
            fronts = NewSheet(sheetWidth, sheetHeight);
            backs = _twoFaces ? NewSheet(sheetWidth, sheetHeight) : null;
            int column = 0;
            int row = 0;
            int sheet = 0;
            bool pending = false;
            for (int i = 0; i < _images.Count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                bool isFront = !_twoFaces || i % 2 == 0;
                MagickImage target = isFront ? fronts : backs!;
                PdfImage? image = _images[i];
                if (image != null)
                {
                    Draw(target, image, column * cell.X, row * cell.Y, cell);
                }

                pending = true;

                // A card is done after its front, or after its back when it has both.
                if (!_twoFaces || !isFront)
                {
                    column++;
                    if (column == Columns)
                    {
                        column = 0;
                        row++;
                    }

                    if (row == Rows)
                    {
                        Save(fronts, backs, basePath, sheet++, extension, profile, written);
                        pending = false;
                        row = 0;

                        // The original painted the next sheets black where it had begun with
                        // white; they all begin white here.
                        fronts.Dispose();
                        backs?.Dispose();
                        fronts = NewSheet(sheetWidth, sheetHeight);
                        backs = _twoFaces ? NewSheet(sheetWidth, sheetHeight) : null;
                    }
                }

                report?.Invoke(i + 1);
            }

            if (pending)
            {
                Save(fronts, backs, basePath, sheet, extension, profile, written);
            }
        }
        finally
        {
            fronts?.Dispose();
            backs?.Dispose();
        }

        Log.Exit(nameof(Write));
        return written;
    }

    private static MagickImage NewSheet(uint width, uint height)
    {
        var sheet = new MagickImage(MagickColors.White, width, height);
        sheet.Density = new Density(AppSettings.TargetDpi, AppSettings.TargetDpi, DensityUnit.PixelsPerInch);
        return sheet;
    }

    /// <summary>Original: <c>CrearTabletopSwitch</c>, <c>InsertaImagenNormal</c> and <c>InsertaImagenBlanca</c>.</summary>
    private void Draw(MagickImage sheet, PdfImage image, int x, int y, PointI cell)
    {
        if (image.Kind == ImageKind.Normal && File.Exists(image.Path))
        {
            using var card = new MagickImage(image.Path);
            if (card.ColorSpace == ColorSpace.CMYK)
            {
                ColorManagement.CmykToRgb(card);
            }

            ImageTransforms.Rotate(card, image.Rotation);
            ImageDensity.RaiseDpi(card, AppSettings.TargetDpi, _cardSize);
            card.FilterType = _other.ResizeFilter;
            card.Resize(new MagickGeometry((uint)cell.X, (uint)cell.Y) { IgnoreAspectRatio = true });
            sheet.Composite(card, x, y, CompositeOperator.Over);
            return;
        }

        // A blank card is its colour; an empty one, and an image that is not there, white.
        MagickColor color = image.Kind == ImageKind.Blank ? ArgbColor.Parse(image.Path).ToMagick() : MagickColors.White;
        using var patch = new MagickImage(color, (uint)cell.X, (uint)cell.Y);
        sheet.Composite(patch, x, y, CompositeOperator.Over);
    }

    private void Save(MagickImage fronts, MagickImage? backs, string basePath, int number, string extension, IColorProfile? profile, List<string> written)
    {
        foreach ((MagickImage? sheet, string side) in new[] { (backs, "_back"), (fronts, "_front") })
        {
            if (sheet == null)
            {
                continue;
            }

            string path = basePath + "_" + number + side + extension;
            sheet.Quality = 100;
            if (profile != null)
            {
                sheet.SetProfile(profile);
            }

            sheet.Write(path);
            written.Add(path);
        }
    }
}
