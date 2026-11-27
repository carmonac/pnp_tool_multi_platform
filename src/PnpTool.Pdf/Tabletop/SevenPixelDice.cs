using ImageMagick;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Imaging;

namespace PnpTool.Pdf.Tabletop;

/// <summary>The dice the "seven pixels" templates exist for. Original: <c>FormDice7PixelsModelo.TiposDadosD7Pixels</c>.</summary>
public enum SevenPixelDie
{
    D2,
    D2Flat,
    D3,
    D4,
    D6,
    D8,
    D10,
    D12,
    D16,
    D20,
    D24,
    D30,
}

/// <summary>
/// Where the faces of one kind of die go on its template.
/// Original: <c>FormDice7PixelsModelo.CaracteristicasDado</c>.
/// </summary>
/// <param name="ColumnWidth">Pixels from one column to the next.</param>
/// <param name="RowHeight">Pixels from one row to the next.</param>
/// <param name="Rows">Rows of faces.</param>
/// <param name="Columns">Columns of faces.</param>
/// <param name="ImageSize">Side of the square each image is made to fill.</param>
/// <param name="OffsetX">Pixels from the left edge to the first column.</param>
/// <param name="OffsetY">Pixels from the top edge to the first row.</param>
public sealed record DieLayout(int ColumnWidth, int RowHeight, int Rows, int Columns, int ImageSize, int OffsetX, int OffsetY);

/// <summary>
/// Writes images as the templates of custom dice of 1024 × 1024 pixels: one template for
/// each die, with an image on each of its faces. Original: <c>FormDice7PixelsModelo</c>.
/// </summary>
/// <remarks>
/// The original built a list of faces with a loop for each kind of die and then walked a
/// table of rows and columns over that list. The loops have been replaced by <see cref="Faces"/>,
/// which says for every place of the template which image of the die goes there; the places
/// themselves (<see cref="Place"/>) are the original's.
/// </remarks>
public sealed class SevenPixelDice
{
    /// <summary>Side of a template in pixels.</summary>
    public const int Side = 1024;

    private readonly IReadOnlyList<CardFace> _images;
    private readonly string _fillColor;

    /// <param name="cards">The cards of the project; every copy of a card is a face. Original: <c>SeparaMazo</c>.</param>
    /// <param name="die">The kind of die.</param>
    /// <param name="fillColor">The colour of the template where there is no image, as "A_R_G_B". Original: <c>ColorRelleno</c>.</param>
    public SevenPixelDice(IEnumerable<Card> cards, SevenPixelDie die, string fillColor)
    {
        _images = [.. cards.SelectMany(card => Enumerable.Repeat(card.Front, Math.Max(card.Copies, 0)))];
        Die = die;
        _fillColor = fillColor;
    }

    public SevenPixelDie Die { get; }

    /// <summary>How many images there are.</summary>
    public int ImageCount => _images.Count;

    /// <summary>How many templates will be written: one for each die the images fill, whole or in part.</summary>
    public int DiceCount => (_images.Count + FacesPerDie(Die) - 1) / FacesPerDie(Die);

    /// <summary>The kind of die a style of the "seven pixels" group stands for. Original: <c>DevuelveTipo</c>.</summary>
    public static SevenPixelDie FromStyleIndex(int index) =>
        Enum.IsDefined(typeof(SevenPixelDie), index) ? (SevenPixelDie)index : SevenPixelDie.D2;

    /// <summary>Original: <c>EstableceParametrosDado</c>.</summary>
    public static DieLayout LayoutOf(SevenPixelDie die) => die switch
    {
        SevenPixelDie.D2 => new DieLayout(341, 341, 3, 3, 250, 41, 51),
        SevenPixelDie.D3 => new DieLayout(341, 341, 3, 3, 346, 0, 0),
        SevenPixelDie.D6 => new DieLayout(341, 341, 3, 3, 341, 0, 0),
        SevenPixelDie.D2Flat => new DieLayout(413, 413, 1, 2, 346, 0, 0),
        SevenPixelDie.D4 => new DieLayout(512, 460, 2, 2, 230, 142, 230),
        SevenPixelDie.D8 => new DieLayout(337, 295, 3, 3, 155, 94, 140),
        SevenPixelDie.D10 => new DieLayout(246, 336, 3, 4, 135, 57, 160),
        SevenPixelDie.D12 => new DieLayout(242, 233, 3, 4, 140, 58, 166),
        SevenPixelDie.D16 => new DieLayout(161, 330, 3, 6, 105, 29, 192),
        SevenPixelDie.D20 => new DieLayout(215, 187, 5, 4, 95, 60, 83),
        SevenPixelDie.D24 => new DieLayout(228, 205, 7, 4, 95, 70, 47),
        _ => new DieLayout(282, 175, 10, 3, 90, 114, 43),
    };

    /// <summary>
    /// For every place of the template, in the order the places are walked, the number
    /// (from 0) of the image of the die that goes there, or -1 for none. The number of
    /// different images is that of the faces of the die. Original: the four
    /// <c>ListadoImagenes_Dice7Pixel…</c> methods.
    /// </summary>
    public static int[] Faces(SevenPixelDie die) => die switch
    {
        // Each side of the coin twice, one above the other.
        SevenPixelDie.D2 => [-1, -1, -1, -1, 0, 1, -1, 0, 1],
        SevenPixelDie.D3 => [-1, -1, -1, 0, 1, 2, 0, 1, 2],

        // The cross of a cube.
        SevenPixelDie.D6 => [5, -1, -1, 3, 4, -1, 0, 1, 2],
        SevenPixelDie.D2Flat => [0, 1],
        SevenPixelDie.D4 => [0, 1, 2, 3],
        SevenPixelDie.D8 => [0, 1, 2, 3, 4, 5, 6, 7, -1],
        SevenPixelDie.D10 => [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, -1, -1],
        SevenPixelDie.D12 => [.. Enumerable.Range(0, 12)],

        // The twelfth image goes to the place before the last.
        SevenPixelDie.D16 => [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, -1, 12, 13, 14, 15, 11, -1],
        SevenPixelDie.D20 => [.. Enumerable.Range(0, 20)],
        SevenPixelDie.D24 => [.. Enumerable.Range(0, 24)],
        _ => [.. Enumerable.Range(0, 30)],
    };

    /// <summary>How many images one die takes.</summary>
    public static int FacesPerDie(SevenPixelDie die) => Faces(die).Max() + 1;

    /// <summary>
    /// The top-left corner, in pixels, of each place of the template, in the order they are
    /// walked: row by row, except that every other row of the D24 has one place fewer.
    /// Original: the walk of <c>CrearDado</c> with <c>CoordenadasImagen</c>.
    /// </summary>
    public static PointI[] Places(SevenPixelDie die)
    {
        DieLayout layout = LayoutOf(die);
        int count = Faces(die).Length;
        var places = new PointI[count];
        int column = 0;
        int row = 0;
        for (int i = 0; i < count; i++)
        {
            places[i] = Place(die, column, row, i, layout);

            // The rows of the D24 are staggered: the odd ones hold three faces.
            if (die == SevenPixelDie.D24 && row % 2 == 1 && column == layout.Columns - 2)
            {
                column++;
            }

            column++;
            if (column == layout.Columns)
            {
                column = 0;
                row++;
            }
        }

        return places;
    }

    /// <summary>Original: <c>CoordenadasImagen</c>.</summary>
    private static PointI Place(SevenPixelDie die, int column, int row, int index, DieLayout layout)
    {
        int x = (column * layout.ColumnWidth) + layout.OffsetX;
        int y = (row * layout.RowHeight) + layout.OffsetY;
        return die switch
        {
            SevenPixelDie.D2 or SevenPixelDie.D3 => new PointI(x, y),
            SevenPixelDie.D2Flat => index % 2 == 0 ? new PointI(124, 124) : new PointI(553, 532),
            SevenPixelDie.D4 => new PointI(x, y + (row * 40)),
            SevenPixelDie.D6 => new PointI(column * layout.ColumnWidth, row * layout.RowHeight),
            SevenPixelDie.D8 => new PointI(x, y + 40),
            SevenPixelDie.D10 => new PointI(x + 13, y + 19),
            SevenPixelDie.D12 => new PointI(x + (10 * column), y + (20 * row)),
            SevenPixelDie.D16 => new PointI(x + 14, y + 36),
            SevenPixelDie.D20 => new PointI(x, y + 58),
            SevenPixelDie.D24 => new PointI(x + (row % 2 == 0 ? 57 : 175), y + 11 - (74 * row)),
            _ => new PointI(x + (row % 2 == 0 ? 0 : 140), y + 35 - (87 * row)),
        };
    }

    /// <summary>
    /// Writes the templates as <c>base_0.png</c>, <c>base_1.png</c>… and gives their paths.
    /// Original: <c>CrearDado</c>.
    /// </summary>
    /// <param name="basePath">Folder and beginning of the names of the files.</param>
    /// <param name="report">Told how many dice are done.</param>
    public List<string> Write(string basePath, Action<int>? report = null, CancellationToken cancellation = default)
    {
        Log.Enter(nameof(Write));
        var written = new List<string>();
        DieLayout layout = LayoutOf(Die);
        int[] faces = Faces(Die);
        PointI[] places = Places(Die);
        int perDie = FacesPerDie(Die);
        MagickColor fill = ArgbColor.Parse(_fillColor).ToMagick();
        for (int die = 0; die < DiceCount; die++)
        {
            cancellation.ThrowIfCancellationRequested();

            // The original began the first template white and the rest in the fill colour.
            using var template = new MagickImage(fill, Side, Side);
            template.Density = new Density(AppSettings.TargetDpi, AppSettings.TargetDpi, DensityUnit.PixelsPerInch);
            for (int place = 0; place < faces.Length; place++)
            {
                int image = faces[place] < 0 ? -1 : (die * perDie) + faces[place];
                if (image >= 0 && image < _images.Count)
                {
                    Draw(template, _images[image], places[place], layout);
                }
            }

            string path = basePath + "_" + die + ".png";
            template.Write(path, MagickFormat.Png);
            written.Add(path);
            report?.Invoke(die + 1);
        }

        Log.Exit(nameof(Write));
        return written;
    }

    /// <summary>Original: <c>InsertaImagenNormal</c> and the colour patch of <c>CrearDado</c>.</summary>
    private static void Draw(MagickImage template, CardFace face, PointI where, DieLayout layout)
    {
        if (face.ImageKind == ImageKind.Normal && File.Exists(face.Path))
        {
            using var image = new MagickImage(face.Path);
            if (image.ColorSpace == ColorSpace.CMYK)
            {
                ColorManagement.CmykToRgb(image);
            }

            ImageTransforms.Rotate(image, face.Rotation);
            image.Resize(new MagickGeometry((uint)layout.ImageSize, (uint)layout.ImageSize) { IgnoreAspectRatio = true });
            template.Composite(image, where.X, where.Y, CompositeOperator.Over);
        }
        else if (face.ImageKind == ImageKind.Blank)
        {
            // A blank card is a patch of its colour, as large as a place of the table.
            using var patch = new MagickImage(ArgbColor.Parse(face.Path).ToMagick(), (uint)layout.ColumnWidth, (uint)layout.RowHeight);
            template.Composite(patch, where.X, where.Y, CompositeOperator.Over);
        }
    }
}
