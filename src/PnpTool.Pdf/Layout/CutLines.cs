using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Core.Templates;

namespace PnpTool.Pdf.Layout;

/// <summary>
/// Where the cut lines of a sheet go. Original: <c>LineasCorteNormal</c> and <c>LineasCorteCruz</c>.
/// </summary>
/// <remarks>
/// Coordinates are in points. The grid is centred on the sheet, so its distance from the top
/// equals its distance from the bottom, and the original uses positions measured from the top
/// directly as PDF coordinates, which are measured from the bottom. That is kept.
/// With bleed, the lines move in by the bleed on each side of a card, so each card has its own
/// pair of lines instead of sharing one with its neighbour.
/// </remarks>
public static class CutLines
{
    /// <summary>
    /// Lines that stop short of the cards. Each line of the grid is given as four points: from
    /// the edge of the sheet (0) to just before the grid (1), and from just after the grid (2)
    /// to the opposite edge (3); <paramref name="gap"/> is the distance kept from the grid.
    /// With <paramref name="columns"/> the lines are vertical, one or two per column
    /// boundary; otherwise horizontal. Original: <c>DevuelvePosiciones_LineasCorte_Normal</c>.
    /// </summary>
    public static PointD[,] Outside(bool columns, int gap, PageLayout layout) =>
        columns ? OutsideColumns(gap, layout) : OutsideRows(gap, layout);

    private static int LineSlots(bool separated, int count) => separated ? (2 * count) - 1 : count;

    private static double FarEnd(Unit bleed, int gap, Unit objectSize, int count, Unit spacing) =>
        0.0 - bleed.Point + gap + (objectSize.Point * count) + (spacing.Point * (count - 1));

    /// <summary>How far to move on from the previous line. Original: <c>ModificaPosInicial</c>.</summary>
    private static Unit Step(int index, bool separated, Unit objectSize, bool even, Unit spacing, bool hasSpacing)
    {
        if (index <= 0)
        {
            return Unit.Zero;
        }

        if (!separated || !even)
        {
            return objectSize;
        }

        return hasSpacing && index > 1 ? spacing : Unit.Zero;
    }

    private static Unit BleedShift(bool hasBleed, bool even, Unit bleed) =>
        !hasBleed ? Unit.Zero : even ? bleed : 0.0 - (double)bleed;

    private static PointD[,] OutsideColumns(int gap, PageLayout layout)
    {
        var position = new UnitPoint(layout.GridOrigin);
        Grid grid = layout.Settings.Grid;
        PrintLayout print = layout.Settings.PrintLayout;
        Unit bleed = grid.Bleed;
        double farEnd = FarEnd(bleed, gap, layout.ObjectSize.Y, print.Rows, grid.Spacing.Y);
        Unit paper = print.Paper.Size.Y;
        bool hasSpacing = grid.Spacing.X.Point > 0.0;
        bool hasBleed = bleed.Point > 0.0;
        bool separated = hasSpacing | hasBleed;
        int slots = LineSlots(separated, print.Columns);
        var lines = new PointD[slots + 1, 4];
        for (int i = 0; i < slots + 1; i++)
        {
            bool even = i % 2 == 0;
            position.X = (double)position.X + (double)Step(i, separated, layout.ObjectSize.X, even, grid.Spacing.X, hasSpacing);
            double x = (double)position.X + (double)BleedShift(hasBleed, even, bleed);
            lines[i, 0] = new PointD(x, 0.0);
            lines[i, 1] = new PointD(x, position.Y.Point + bleed.Point - gap);
            lines[i, 2] = new PointD(x, position.Y.Point + farEnd);
            lines[i, 3] = new PointD(x, paper);
        }

        return lines;
    }

    private static PointD[,] OutsideRows(int gap, PageLayout layout)
    {
        var position = new UnitPoint(layout.GridOrigin);
        Grid grid = layout.Settings.Grid;
        PrintLayout print = layout.Settings.PrintLayout;
        Unit bleed = grid.Bleed;
        Unit paper = print.Paper.Size.X;
        double farEnd = FarEnd(bleed, gap, layout.ObjectSize.X, print.Columns, grid.Spacing.X);
        bool hasSpacing = grid.Spacing.Y.Point > 0.0;
        bool hasBleed = bleed.Point > 0.0;
        bool separated = hasSpacing | hasBleed;
        int slots = LineSlots(separated, print.Rows);
        var lines = new PointD[slots + 1, 4];
        for (int i = 0; i < slots + 1; i++)
        {
            bool even = i % 2 == 0;
            position.Y = (double)position.Y + (double)Step(i, separated, layout.ObjectSize.Y, even, grid.Spacing.Y, hasSpacing);
            double y = (double)position.Y + (double)BleedShift(hasBleed, even, bleed);
            lines[i, 0] = new PointD(0.0, y);
            lines[i, 1] = new PointD(position.X.Point + bleed.Point - gap, y);
            lines[i, 2] = new PointD(position.X.Point + farEnd, y);
            lines[i, 3] = new PointD(paper, y);
        }

        return lines;
    }

    /// <summary>
    /// The two ends of one arm of a cross centred at <paramref name="centre"/>, half
    /// <paramref name="halfLength"/> millimetres long each way. With <paramref name="toEdge"/>
    /// the first cross of an axis starts at the edge of the sheet and the last one ends at the
    /// opposite edge. Returned as (start, end) in points. Original: <c>DevuelvePosicion</c>.
    /// </summary>
    public static (double Start, double End) CrossArm(Unit centre, bool toEdge, int index, int count, Unit paperSize, double halfLength)
    {
        Unit start = Unit.FromMillimeter(centre.Millimeter - halfLength);
        Unit end = Unit.FromMillimeter(centre.Millimeter + halfLength);
        if (toEdge)
        {
            if (index == 0)
            {
                start = 0;
            }

            if (index == count - 1)
            {
                end = paperSize;
            }
        }

        return (start.Point, end.Point);
    }

    /// <summary>
    /// A cross at every corner of every card: indexed by corner column (two per card column),
    /// corner row, and arm (0 horizontal, 1 vertical). Original: <c>DevuelvePosicionesLineasCorteCruzV3</c>.
    /// </summary>
    public static LineD[,,] Crosses(bool toEdge, PageLayout layout, double halfLength)
    {
        ProjectSettings settings = layout.Settings;
        int columns = settings.PrintLayout.Columns * 2;
        int rows = settings.PrintLayout.Rows * 2;
        UnitPoint paper = settings.PrintLayout.Paper.Size;
        UnitPoint spacing = settings.Grid.Spacing;
        Unit bleed = settings.Grid.Bleed;
        var crosses = new LineD[columns, rows, 2];
        int cardX = 0;
        for (int column = 0; column < columns; column++)
        {
            int cardY = 0;
            double baseX = (double)layout.GridOrigin.X + (cardX * (double)layout.ObjectSize.X);
            Unit x;
            if (column % 2 == 1)
            {
                x = baseX + ((double)spacing.X * (cardX - 1)) - (double)bleed;
            }
            else
            {
                x = baseX + ((double)spacing.X * cardX) + (double)bleed;
                cardX++;
            }

            for (int row = 0; row < rows; row++)
            {
                double baseY = (double)layout.GridOrigin.Y + (cardY * (double)layout.ObjectSize.Y);
                Unit y;
                if (row % 2 == 1)
                {
                    y = baseY + ((double)spacing.Y * (cardY - 1)) - (double)bleed;
                }
                else
                {
                    y = baseY + ((double)spacing.Y * cardY) + (double)bleed;
                    cardY++;
                }

                (double startX, double endX) = CrossArm(x, toEdge, column, columns, paper.X, halfLength);
                (double startY, double endY) = CrossArm(y, toEdge, row, rows, paper.Y, halfLength);
                crosses[column, row, 0] = new LineD(startX, y.Point, endX, y.Point);
                crosses[column, row, 1] = new LineD(x.Point, startY, x.Point, endY);
            }
        }

        return crosses;
    }

    /// <summary>
    /// Crosses at the four corners of every place of an advanced template, 2 mm each way,
    /// inside the template's bleed: indexed by place, corner and arm.
    /// Original: <c>DevuelvePosicionesLineasCorteCruzEstiloAvanzadoV2</c>.
    /// </summary>
    public static LineD[,,] TemplateCrosses(AdvancedTemplate template, UnitPoint[,] positions)
    {
        var crosses = new LineD[template.Cards.Length, 4, 2];
        double two = Unit.FromMillimeter(2.0);
        double four = Unit.FromMillimeter(4.0);
        double bleed = template.Bleed;
        for (int place = 0; place < template.Cards.Length; place++)
        {
            UnitPoint size = template.Cards[place].Size;
            double originX = (double)positions[place, 0].X + bleed;
            double originY = (double)positions[place, 0].Y + bleed;

            // First corner: horizontal arm, then vertical arm.
            double fromX = originX - two;
            double fromY = originY;
            double toX = fromX + four;
            double toY = fromY;
            crosses[place, 0, 0] = new LineD(fromX, fromY, toX, toY);
            fromX += two;
            fromY = originY - two;
            toX = fromX;
            toY = fromY + four;
            crosses[place, 0, 1] = new LineD(fromX, fromY, toX, toY);

            // Second corner, across the width.
            fromX = fromX + (double)size.X - (2.0 * bleed);
            toX = fromX;
            crosses[place, 1, 0] = new LineD(fromX, fromY, toX, toY);
            fromX -= two;
            fromY += two;
            toX = fromX + four;
            toY = fromY;
            crosses[place, 1, 1] = new LineD(fromX, fromY, toX, toY);

            // Third corner, across the height.
            fromY = fromY + (double)size.Y - (2.0 * bleed);
            toY = fromY;
            crosses[place, 2, 0] = new LineD(fromX, fromY, toX, toY);
            fromX += two;
            fromY -= two;
            toX = fromX;
            toY = fromY + four;
            crosses[place, 2, 1] = new LineD(fromX, fromY, toX, toY);

            // Fourth corner, back at the first side.
            fromX = originX;
            toX = fromX;
            crosses[place, 3, 0] = new LineD(fromX, fromY, toX, toY);
            fromX = originX - two;
            fromY += two;
            toX = originX + two;
            toY = fromY;
            crosses[place, 3, 1] = new LineD(fromX, fromY, toX, toY);
        }

        return crosses;
    }
}
