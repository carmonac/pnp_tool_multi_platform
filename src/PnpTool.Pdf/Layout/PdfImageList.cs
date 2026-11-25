using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Imaging;

namespace PnpTool.Pdf.Layout;

/// <summary>
/// Turns the deck of a project into the list of cells of its PDF, in the order they are laid
/// on the sheets: fronts and backs interleaved sheet by sheet, copies repeated, pages of a
/// booklet imposed, and so on for each kind of project. Original: <c>GenerarListadoImagenes</c>.
/// </summary>
/// <remarks>
/// The original also built the list for the "Dice 7 pixels" generator, which lives with that
/// tool and is not ported here; such a project gives an empty list.
/// </remarks>
public static class PdfImageList
{
    /// <summary>
    /// How to read the EXIF orientation of an image file. Replaceable so the arrangement can
    /// be tested without image files.
    /// </summary>
    public static Func<string, RotationProperties> ExifRotation { get; set; } = MagickImageInspector.ExifRotation;

    /// <summary>Original: <c>Generar_Listado</c>.</summary>
    public static PdfImage[] Generate(Project project)
    {
        ProjectSettings settings = project.Settings;
        PdfImage?[] cells = [];
        switch (settings.Group.ProjectType)
        {
            case ProjectType.Card:
            case ProjectType.Book:
            case ProjectType.SpanishDeck:
            case ProjectType.Papermini:
                cells = Normal(project);
                break;
            case ProjectType.Booklet:
                cells = Booklet(project);
                break;
            case ProjectType.AdvancedStyles:
                cells = Normal(project);
                AddTemplateRotation(cells, project);
                CheckSameAsPreviousForTemplate(cells, project);
                break;
            case ProjectType.Tabletop:
                bool doublePanel = settings.DoublePanel;
                if (settings.PrintLayout.BackImage == BackImageMode.SameBack)
                {
                    foreach (Card card in project.Deck.Cards)
                    {
                        card.Back.Path = settings.SharedBack.Path;
                        card.Back.ImageKind = ImageKind.Normal;
                    }

                    doublePanel = true;
                }

                cells = Tabletop(doublePanel, project.Deck);
                break;
            case ProjectType.Gutterfold:
                cells = Gutterfold(settings, project.Deck);
                break;
            case ProjectType.Pocketmod:
                cells = Pocketmod(project.Deck);
                break;
            case ProjectType.Pocketfold:
                cells = Pocketfold(project.Deck);
                break;
        }

        PdfImage[] filled = FillGaps(cells);
        MarkRepeats(filled);
        return filled;
    }

    /// <summary>
    /// Empty cells for the gaps, and the EXIF orientation of each image file added to its
    /// rotation. Original: <c>RellenaNull</c>.
    /// </summary>
    private static PdfImage[] FillGaps(PdfImage?[] cells)
    {
        var filled = new PdfImage[cells.Length];
        for (int i = 0; i < cells.Length; i++)
        {
            PdfImage? cell = cells[i];
            if (cell == null)
            {
                filled[i] = PdfImage.Empty();
                continue;
            }

            if (cell.Kind == ImageKind.Normal)
            {
                cell.Rotation.Combine(ExifRotation(cell.Path));
            }

            filled[i] = cell;
        }

        return filled;
    }

    /// <summary>Original: <c>UltimoRepasoBuscandoIguales</c>.</summary>
    private static void MarkRepeats(PdfImage[] cells)
    {
        for (int i = 1; i < cells.Length; i++)
        {
            if (!cells[i].SameAsPrevious)
            {
                cells[i].SameAsPrevious = cells[i].IsSameAs(cells[i - 1]);
            }
        }
    }

    /// <summary>Front and back of every copy, one after the other. Original: <c>Listado_Tabletop</c>.</summary>
    private static PdfImage?[] Tabletop(bool doublePanel, Deck deck)
    {
        var cells = new PdfImage?[deck.TotalCopies * (doublePanel ? 2 : 1)];
        int next = 0;
        foreach (Card card in deck.Cards)
        {
            for (int copy = 0; copy < card.Copies; copy++)
            {
                cells[next++] = new PdfImage(card.Front);
                if (doublePanel)
                {
                    cells[next++] = new PdfImage(card.Back);
                }
            }
        }

        return cells;
    }

    /// <summary>
    /// Pages of a booklet arranged so that the folded sheets read in order: last with first,
    /// second with last but one, and so on, the pair swapped on every other sheet side. When
    /// the sheets are flipped on the long edge the pages of those sides are turned half round.
    /// Original: <c>Listado_Libreto</c>.
    /// </summary>
    private static PdfImage?[] Booklet(Project project)
    {
        PdfImage?[] pages = Normal(project);
        var imposed = new PdfImage?[pages.Length];
        int middle = pages.Length / 2;
        int high = pages.Length - 1;
        int next = 0;
        bool even = true;
        for (int low = 0; low < middle; low++)
        {
            if (!even)
            {
                imposed[next] = pages[low];
                imposed[next + 1] = pages[high];
                if (project.Settings.Book.FlipEdge == PrintFlipEdge.Long)
                {
                    // As in the original, the half turn is added without wrapping at 360,
                    // and a page missing from an incomplete last sheet is not expected here.
                    imposed[next]!.Rotation.Angle += 180;
                    imposed[next + 1]!.Rotation.Angle += 180;
                }
            }
            else
            {
                imposed[next] = pages[high];
                imposed[next + 1] = pages[low];
            }

            high--;
            next += 2;
            even = !even;
        }

        return imposed;
    }

    /// <summary>Original: <c>ValidaIgualdad_EstiloAvanzado</c>.</summary>
    private static void CheckSameAsPreviousForTemplate(PdfImage?[] cells, Project project)
    {
        var places = project.AdvancedTemplate.Cards;
        for (int i = 1; i < cells.Length; i++)
        {
            PdfImage? cell = cells[i];
            if (cell is { SameAsPrevious: true })
            {
                int place = i % places.Length;

                // The original read the place before the first one, which does not exist, when
                // a repeated image fell on the first place of a sheet. That place starts a
                // new sheet, where nothing is reused.
                if (place == 0 || places[place].Rotation != places[place - 1].Rotation)
                {
                    cell.SameAsPrevious = false;
                }
            }
        }
    }

    /// <summary>Adds the rotation of each place of the template to the image put there. Original: <c>ModificaRotacion_EstiloAvanzado</c>.</summary>
    private static void AddTemplateRotation(PdfImage?[] cells, Project project)
    {
        var places = project.AdvancedTemplate.Cards;
        if (places.Length == 0)
        {
            return;
        }

        int place = 0;
        foreach (PdfImage? cell in cells)
        {
            if (cell != null)
            {
                cell.Rotation.Angle += places[place].Rotation;
                if (cell.Rotation.Angle > 360)
                {
                    cell.Rotation.Angle %= 360;
                }
            }

            place = (place + 1) % places.Length;
        }
    }

    /// <summary>Original: <c>Listado_Normal</c>.</summary>
    private static PdfImage?[] Normal(Project project)
    {
        ProjectSettings settings = project.Settings;
        PdfImage?[] cells;
        if (settings.PrintLayout.BackImage != BackImageMode.DifferentBack)
        {
            if (settings.Group.Group != SettingsGroup.Dice)
            {
                cells = FrontsOnly(project);
                if (settings.Book.Spine == BookSpineType.Right)
                {
                    Array.Reverse(cells);
                }
            }
            else
            {
                cells = Dice(project);
            }
        }
        else
        {
            cells = FrontsAndBacks(project);
        }

        if (cells.Length == 0)
        {
            cells = [new PdfImage(new CardFace(AppSettings.ColorWhite, FaceType.Front, ImageKind.Blank))];
        }

        return cells;
    }

    /// <summary>
    /// The front of every copy, padded to whole sheets. Original: <c>Listado_Normal_SinTrasera</c>.
    /// </summary>
    private static PdfImage?[] FrontsOnly(Project project)
    {
        Log.Enter(nameof(FrontsOnly));
        int perSheet = project.Settings.PrintLayout.CardsPerSheet;
        int sheets = project.Deck.TotalCopies / perSheet;
        if (project.Deck.TotalCopies % perSheet != 0 || sheets == 0)
        {
            sheets++;
        }

        var cells = new PdfImage?[perSheet * sheets];
        int next = 0;
        for (int i = 0; i < project.Deck.Cards.Length; i++)
        {
            Log.Write(i.ToString());
            for (int copy = 0; copy < project.Deck.Cards[i].Copies; copy++)
            {
                cells[next] = new PdfImage(project.Deck.Cards[i].Front) { SameAsPrevious = copy > 0 };
                next++;
            }
        }

        Log.Exit(nameof(FrontsOnly));
        return cells;
    }

    /// <summary>
    /// Fronts and backs sheet by sheet: a sheet of fronts followed by a sheet of their backs
    /// with each row reversed, so that after the sheet is turned over each back lies behind
    /// its front. With an advanced template the back takes the same place as its front.
    /// Original: <c>Listado_Normal_ConTrasera</c>.
    /// </summary>
    private static PdfImage?[] FrontsAndBacks(Project project)
    {
        Log.Enter(nameof(FrontsAndBacks));
        ProjectSettings settings = project.Settings;
        int columns = settings.PrintLayout.Columns;
        int perSheet = settings.PrintLayout.CardsPerSheet;
        bool template = settings.Group.ProjectType == ProjectType.AdvancedStyles;
        int sheets = project.Deck.TotalCopies / perSheet;
        if (project.Deck.TotalCopies % perSheet != 0)
        {
            sheets++;
        }

        var cells = new PdfImage?[sheets * perSheet * 2];
        int sheetStart = 0;
        int sheet = 0;
        int inSheet = 0;
        int column = columns - 1;
        int row = 0;
        for (int i = 0; i < project.Deck.Cards.Length; i++)
        {
            Card card = project.Deck.Cards[i];
            int backAngle = card.Back.Rotation.Angle;
            Log.Write(i.ToString());
            for (int copy = 0; copy < card.Copies; copy++)
            {
                int front = sheetStart + inSheet;
                int back = template ? front + perSheet : sheetStart + perSheet + column + (row * columns);
                if (template && project.AdvancedTemplate.Cards[back % perSheet].Rotation == 90)
                {
                    card.Back.Rotation.Angle = backAngle + 180;
                }

                cells[front] = new PdfImage(card.Front);
                cells[back] = new PdfImage(card.Back);
                if (inSheet < perSheet - 1)
                {
                    inSheet++;
                    column--;
                    if (column < 0)
                    {
                        column = columns - 1;
                        row++;
                    }
                }
                else
                {
                    inSheet = 0;
                    row = 0;
                    column = columns - 1;
                    sheet++;
                    sheetStart = perSheet * 2 * sheet;
                }
            }
        }

        for (int i = 1; i < cells.Length; i++)
        {
            PdfImage? cell = cells[i];
            if (cell != null)
            {
                cell.SameAsPrevious = cell.IsSameAs(cells[i - 1]);
            }
        }

        Log.Exit(nameof(FrontsAndBacks));
        return cells;
    }

    /// <summary>
    /// Nets of six-sided dice: fifteen cells per die, six of them faces and the rest glue tabs
    /// (the hexagon image, some mirrored or turned) and white. Original: <c>Listado_Normal_SinTrasera_Dados</c>.
    /// </summary>
    private static PdfImage?[] Dice(Project project)
    {
        Log.Enter(nameof(Dice));
        Deck deck = project.Deck;
        int dice = deck.TotalCopies / 6;
        if (deck.TotalCopies % 6 != 0)
        {
            dice++;
        }

        var cells = new PdfImage?[dice * 15];
        int cell = 0;
        int card = 0;
        int die = 0;
        int copies = 0;
        int dieStart = 0;
        int position = 0;
        while (die < dice)
        {
            (PdfImage image, bool isFace) = DiceCell(cell, card, project);
            cells[position] = image;
            if (isFace && card < deck.Cards.Length)
            {
                copies++;
                if (copies == deck.Cards[card].Copies)
                {
                    card++;
                    copies = 0;
                }
            }

            cell++;
            position = dieStart + cell;
            if (cell == 15)
            {
                cell = 0;
                die++;
                dieStart = die * 15;
            }
        }

        Log.Exit(nameof(Dice));
        return cells;
    }

    /// <summary>Original: <c>Listado_Normal_SinTrasera_Dados_Switch</c>.</summary>
    private static (PdfImage Image, bool IsFace) DiceCell(int cell, int card, Project project)
    {
        Log.Write(cell.ToString());
        string hexagon = StockImages.HexagonPath;
        switch (cell)
        {
            case 0:
            case 6:
            case 9:
                return (new PdfImage(ImageKind.Normal, hexagon, 0, flipHorizontal: false, flipVertical: false, isCmyk: false), false);
            case 2:
            case 8:
            case 11:
                return (new PdfImage(ImageKind.Normal, hexagon, 0, flipHorizontal: true, flipVertical: false, isCmyk: false), false);
            case 12:
            case 14:
                return (new PdfImage(ImageKind.Blank, AppSettings.ColorWhite, 0, flipHorizontal: false, flipVertical: false, isCmyk: false), false);
            case 13:
                return (new PdfImage(ImageKind.Normal, hexagon, 270, flipHorizontal: false, flipVertical: false, isCmyk: false), false);
            default:
                CardFace face = card < project.Deck.Cards.Length ? project.Deck.Cards[card].Front : project.Settings.BlankFace;
                return (new PdfImage(face), true);
        }
    }

    /// <summary>
    /// A row of fronts above a row of their backs, to be folded along the gutter between
    /// them. The row on the far side of the fold is turned half round. Original: <c>Listado_Gutterfold</c>.
    /// </summary>
    private static PdfImage?[] Gutterfold(ProjectSettings settings, Deck deck)
    {
        Log.Enter(nameof(Gutterfold));
        int columns = settings.PrintLayout.Columns;
        int rows = deck.TotalCopies / columns;
        if (deck.TotalCopies % columns != 0)
        {
            rows++;
        }

        var cells = new PdfImage?[rows * columns * 2];
        int column = 0;
        int rowStart = 0;
        bool sharedBackRepeats = false;
        for (int i = 0; i < deck.Cards.Length; i++)
        {
            Log.Write(i.ToString());
            Card card = deck.Cards[i];
            for (int copy = 0; copy < card.Copies; copy++)
            {
                bool repeats = copy > 0 && column != 0;
                int upper = rowStart + column;
                int lower = upper + columns;
                cells[upper] = new PdfImage(card.Front) { SameAsPrevious = repeats };
                if (settings.PrintLayout.BackImage == BackImageMode.SameBack)
                {
                    cells[lower] = new PdfImage(settings.SharedBack) { SameAsPrevious = sharedBackRepeats };
                    sharedBackRepeats = true;
                }
                else
                {
                    cells[lower] = new PdfImage(card.Back) { SameAsPrevious = repeats };
                }

                cells[settings.PrintLayout.FoldBottom ? lower : upper]!.Rotation.AddAngle(180);
                column++;
                if (column == columns)
                {
                    sharedBackRepeats = false;
                    column = 0;
                    rowStart += columns * 2;
                }
            }
        }

        Log.Exit(nameof(Gutterfold));
        return cells;
    }

    /// <summary>
    /// Eight pages per sheet for a pocketfold: the first four reversed and turned half round
    /// on the top row, the other four in order below. Copies are not repeated.
    /// Original: <c>Listado_Pocketfold</c>.
    /// </summary>
    private static PdfImage?[] Pocketfold(Deck deck)
    {
        Log.Enter(nameof(Pocketfold));
        int total = deck.TotalCopies;
        int sheets = (total + 8 - 1) / 8;
        var cells = new PdfImage?[sheets * 8];
        (int Cell, int Page, int Angle)[] map =
        [
            (0, 3, 180), (1, 2, 180), (2, 1, 180), (3, 0, 180),
            (4, 4, 0), (5, 5, 0), (6, 6, 0), (7, 7, 0),
        ];
        for (int sheet = 0; sheet < sheets; sheet++)
        {
            int start = sheet * 8;
            foreach ((int cell, int page, int angle) in map)
            {
                int index = start + page;

                // The original counted copies but indexed cards; with copies above 1 it read
                // past the end of the deck. Cards beyond the deck are left empty.
                if (index < total && index < deck.Cards.Length)
                {
                    CardFace face = deck.Cards[index].Front;
                    var image = new PdfImage(face);
                    image.Rotation.Angle = face.Rotation.Angle + angle;
                    cells[start + cell] = image;
                }
            }

            Log.Write($"Block {sheet} processed");
        }

        Log.Exit(nameof(Pocketfold));
        return cells;
    }

    /// <summary>Which page goes in each cell of a pocketmod sheet. Original: <c>DevuelvePocketMod_IndiceCarta</c>.</summary>
    private static readonly int[] PocketmodPages = [0, 1, 7, 2, 6, 3, 5, 4];

    /// <summary>
    /// Eight pages per sheet for a pocketmod, in two columns: the left column turned a quarter
    /// one way and the right column the other. Copies are not repeated. Original: <c>Listado_Pocketmod</c>.
    /// </summary>
    private static PdfImage?[] Pocketmod(Deck deck)
    {
        Log.Enter(nameof(Pocketmod));
        int total = deck.TotalCopies;
        int sheets = total / 8;
        if (total % 8 != 0)
        {
            sheets++;
        }

        var cells = new PdfImage?[sheets * 8];
        for (int sheet = 0; sheet < sheets; sheet++)
        {
            Log.Write(sheet.ToString());
            int start = sheet * 8;
            for (int cell = 0; cell < 8; cell++)
            {
                int page = PocketmodPages[cell] + start;
                if (page >= deck.Cards.Length)
                {
                    continue;
                }

                CardFace face = deck.Cards[page].Front;
                var rotation = new RotationProperties();
                rotation.CopyFrom(face.Rotation);
                rotation.AddAngle(cell % 2 == 0 ? 270 : 90);
                var image = new PdfImage(face);
                image.Rotation.Angle = rotation.Angle;
                cells[cell + start] = image;
            }
        }

        Log.Exit(nameof(Pocketmod));
        return cells;
    }
}
