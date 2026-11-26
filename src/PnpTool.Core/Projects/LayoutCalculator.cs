using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.Core.Projects;

/// <summary>
/// How many cards fit on a sheet and how much room a table of cards takes.
/// Original: <c>FormSetupModelo.CalculaFilasColumnas</c> and <c>EstableceTamanoTotal</c>.
/// </summary>
/// <remarks>The margins and rules of the printing company's group are left out.</remarks>
public static class LayoutCalculator
{
    /// <summary>
    /// The paper as it is printed: with width and height swapped when it is marked as
    /// landscape. Original: the swap repeated in the layout controls and before the PDF.
    /// </summary>
    public static UnitPoint Oriented(Paper.Paper paper) =>
        paper.Landscape ? new UnitPoint { X = paper.Size.Y, Y = paper.Size.X } : new UnitPoint(paper.Size);

    /// <summary>
    /// The most columns and rows of cards that fit the sheet, leaving the printer's margin on
    /// every side. A gutterfold layout gets an even number of rows. Cards without size give
    /// 1000 of each, as in the original. Original: <c>CalculaFilasColumnas</c>.
    /// </summary>
    public static (int Columns, int Rows) Fit(Grid grid, PrintLayout layout, SettingsGroup group)
    {
        if (grid.CardSize.X.EqualsInMillimeters(Unit.Zero) || grid.CardSize.Y.EqualsInMillimeters(Unit.Zero))
        {
            return (1000, 1000);
        }

        UnitPoint paper = Oriented(layout.Paper);
        int columns = (int)Math.Floor(paper.X.Millimeter / grid.CardSize.X.Millimeter);
        int rows = (int)Math.Floor(paper.Y.Millimeter / grid.CardSize.Y.Millimeter);
        double width = (columns * grid.CardSize.X.Millimeter) + ((columns - 1) * grid.Spacing.X.Millimeter)
            + (2.0 * AppSettings.PrinterMargin.X.Millimeter);
        double height = (rows * grid.CardSize.Y.Millimeter) + ((rows - 1) * grid.Spacing.Y.Millimeter)
            + (2.0 * AppSettings.PrinterMargin.Y.Millimeter);
        if (paper.X.Millimeter < width)
        {
            columns--;
        }

        if (paper.Y.Millimeter < height)
        {
            rows--;
        }

        if (IsGutterfold(layout) && rows % 2 != 0)
        {
            rows--;
        }

        if (group == SettingsGroup.Dice)
        {
            // The net of a die takes three columns and five rows of cells. (The total size below
            // counts four rows, as in the original.)
            columns /= 3;
            rows /= 5;
        }

        return (columns, rows);
    }

    /// <summary>
    /// The room the table of cards takes, with the printer's margin on every side.
    /// Original: <c>EstableceTamanoTotal</c>.
    /// </summary>
    public static UnitPoint TotalSize(Grid grid, PrintLayout layout, SettingsGroup group)
    {
        double cardWidth = grid.CardSize.X;
        double cardHeight = grid.CardSize.Y;
        if (group == SettingsGroup.Dice)
        {
            cardWidth *= 3.0;
            cardHeight *= 4.0;
        }

        return new UnitPoint
        {
            X = Unit.FromPoint((cardWidth * layout.Columns) + ((double)grid.Spacing.X * (layout.Columns - 1)) + (2.0 * (double)AppSettings.PrinterMargin.X)),
            Y = Unit.FromPoint((cardHeight * layout.Rows) + ((double)grid.Spacing.Y * (layout.Rows - 1)) + (2.0 * (double)AppSettings.PrinterMargin.Y)),
        };
    }

    /// <summary>Fronts and backs on the same side of the sheet, to fold. Original: <c>SetupEsGlutter</c>.</summary>
    public static bool IsGutterfold(PrintLayout layout) => !layout.DoubleSided && layout.BackImage != BackImageMode.NoBack;
}
