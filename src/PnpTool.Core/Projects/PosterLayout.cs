using PnpTool.Core.Primitives;

namespace PnpTool.Core.Projects;

/// <summary>How the size of a poster is given.</summary>
public enum PosterSizeMode
{
    /// <summary>As a number of sheets across; it need not be whole.</summary>
    Sheets,

    /// <summary>As the width of the finished poster.</summary>
    Width,

    /// <summary>As a percentage of the size the image has at its own resolution.</summary>
    Percent,
}

/// <summary>Where the poster goes when the sheets cover more than it needs.</summary>
public enum PosterAlignment
{
    /// <summary>Left, or top.</summary>
    Start,
    Center,

    /// <summary>Right, or bottom.</summary>
    End,
}

/// <summary>
/// What the poster tool is asked to do. The Windows program has no such settings: it starts
/// the separate program PosteRazor, which this replaces.
/// </summary>
public sealed class PosterSettings
{
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>The sheet as it is printed, already turned if it is used on its side.</summary>
    public UnitPoint Paper { get; set; } = new();

    /// <summary>The border of every sheet the printer leaves blank.</summary>
    public Unit Margin { get; set; }

    /// <summary>How much of the picture neighbouring sheets repeat, to glue one over the other.</summary>
    public Unit Overlap { get; set; }

    public PosterSizeMode SizeMode { get; set; } = PosterSizeMode.Sheets;

    /// <summary>Sheets across, width in the unit of <see cref="Width"/>, or percentage, by <see cref="SizeMode"/>.</summary>
    public double Sheets { get; set; } = 2.0;

    public Unit Width { get; set; }

    public double Percent { get; set; } = 100.0;

    public PosterAlignment Horizontal { get; set; } = PosterAlignment.Center;

    public PosterAlignment Vertical { get; set; } = PosterAlignment.Center;

    /// <summary>Marks at the corners of the printed area of every sheet, to trim the margins.</summary>
    public bool CutMarks { get; set; } = true;
}

/// <summary>One sheet of a poster: where it is and where the whole picture goes on it.</summary>
/// <param name="Row">From the top, starting at 0.</param>
/// <param name="Column">From the left, starting at 0.</param>
/// <param name="ImageLeft">From the left edge of the sheet to the left edge of the picture, in points; negative when the picture starts on an earlier sheet.</param>
/// <param name="ImageTop">The same from the top edge of the sheet to the top edge of the picture.</param>
public sealed record PosterSheet(int Row, int Column, double ImageLeft, double ImageTop);

/// <summary>A poster laid out over its sheets. Lengths are in points.</summary>
public sealed class PosterPlan
{
    public required double PosterWidth { get; init; }

    public required double PosterHeight { get; init; }

    public required double PaperWidth { get; init; }

    public required double PaperHeight { get; init; }

    public required double Margin { get; init; }

    public required double Overlap { get; init; }

    public required int Columns { get; init; }

    public required int Rows { get; init; }

    /// <summary>The sheets in printing order: row by row from the top, left to right.</summary>
    public required IReadOnlyList<PosterSheet> Sheets { get; init; }

    /// <summary>The printed area of a sheet.</summary>
    public double PrintableWidth => PaperWidth - (2.0 * Margin);

    public double PrintableHeight => PaperHeight - (2.0 * Margin);

    /// <summary>What the sheets cover once glued, overlaps counted once.</summary>
    public double CoveredWidth => Covered(Columns, PrintableWidth, Overlap);

    public double CoveredHeight => Covered(Rows, PrintableHeight, Overlap);

    /// <summary>From the left of what the sheets cover to the left of the poster.</summary>
    public required double OffsetX { get; init; }

    public required double OffsetY { get; init; }

    internal static double Covered(int count, double printable, double overlap) =>
        (count * printable) - ((count - 1) * overlap);
}

/// <summary>
/// Splits a large picture over several sheets that are trimmed and glued into a poster.
/// Replaces the program PosteRazor, which the Windows version starts from its tools tab.
/// </summary>
/// <remarks>
/// Every sheet prints its whole printable area. A sheet starts one printable area minus the
/// overlap after the one before, so the strip of the overlap is printed on both: one is
/// trimmed at its margin and glued over the strip of the other.
/// </remarks>
public static class PosterLayout
{
    /// <summary>More sheets than this is taken for a mistake in the sizes.</summary>
    public const int MaximumSheets = 1000;

    private const double Tolerance = 0.01;

    /// <summary>What stops a poster from being made with these settings, leaving the image aside.</summary>
    public static AppError Check(PosterSettings settings)
    {
        var error = new AppError();
        double printableWidth = settings.Paper.X.Point - (2.0 * settings.Margin.Point);
        double printableHeight = settings.Paper.Y.Point - (2.0 * settings.Margin.Point);
        if (settings.Paper.X.Point <= 0.0 || settings.Paper.Y.Point <= 0.0)
        {
            Fail(error, "The paper has no size");
        }
        else if (settings.Margin.Point < 0.0 || printableWidth <= 0.0 || printableHeight <= 0.0)
        {
            Fail(error, "The margins leave no room on the sheet");
        }
        else if (settings.Overlap.Point < 0.0 || settings.Overlap.Point >= Math.Min(printableWidth, printableHeight))
        {
            Fail(error, "The overlap must be smaller than the printed area of a sheet");
        }

        bool sized = settings.SizeMode switch
        {
            PosterSizeMode.Sheets => settings.Sheets > 0.0,
            PosterSizeMode.Width => settings.Width.Point > 0.0,
            _ => settings.Percent > 0.0,
        };
        if (!sized)
        {
            Fail(error, "The poster has no size");
        }

        return error;
    }

    /// <summary>The width of the poster, in points, for an image of the given printed width.</summary>
    public static double PosterWidth(PosterSettings settings, double imageWidth)
    {
        double printable = settings.Paper.X.Point - (2.0 * settings.Margin.Point);
        double overlap = settings.Overlap.Point;
        return settings.SizeMode switch
        {
            // Two and a half sheets: two whole ones with their overlap and half of a third.
            PosterSizeMode.Sheets => settings.Sheets <= 1.0
                ? settings.Sheets * printable
                : printable + ((settings.Sheets - 1.0) * (printable - overlap)),
            PosterSizeMode.Width => settings.Width.Point,
            _ => imageWidth * settings.Percent / 100.0,
        };
    }

    /// <summary>
    /// Lays the poster out. The settings must have passed <see cref="Check"/>.
    /// </summary>
    /// <param name="imageSize">The size the image has at its own resolution; its proportions are kept.</param>
    public static (AppError Error, PosterPlan? Plan) Plan(PosterSettings settings, UnitPoint imageSize)
    {
        AppError error = Check(settings);
        if (!error.HasError && (imageSize.X.Point <= 0.0 || imageSize.Y.Point <= 0.0))
        {
            Fail(error, "The image has no size");
        }

        if (error.HasError)
        {
            return (error, null);
        }

        double margin = settings.Margin.Point;
        double overlap = settings.Overlap.Point;
        double printableWidth = settings.Paper.X.Point - (2.0 * margin);
        double printableHeight = settings.Paper.Y.Point - (2.0 * margin);
        double width = PosterWidth(settings, imageSize.X.Point);
        double height = width * imageSize.Y.Point / imageSize.X.Point;
        int columns = Count(width, printableWidth, overlap);
        int rows = Count(height, printableHeight, overlap);
        if ((long)columns * rows > MaximumSheets)
        {
            Fail(error, "The poster needs " + ((long)columns * rows) + " sheets; the most is " + MaximumSheets);
            return (error, null);
        }

        double offsetX = Share(settings.Horizontal) * (PosterPlan.Covered(columns, printableWidth, overlap) - width);
        double offsetY = Share(settings.Vertical) * (PosterPlan.Covered(rows, printableHeight, overlap) - height);
        var sheets = new List<PosterSheet>(columns * rows);
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                sheets.Add(new PosterSheet(
                    row,
                    column,
                    margin + offsetX - (column * (printableWidth - overlap)),
                    margin + offsetY - (row * (printableHeight - overlap))));
            }
        }

        return (error, new PosterPlan
        {
            PosterWidth = width,
            PosterHeight = height,
            PaperWidth = settings.Paper.X.Point,
            PaperHeight = settings.Paper.Y.Point,
            Margin = margin,
            Overlap = overlap,
            Columns = columns,
            Rows = rows,
            OffsetX = offsetX,
            OffsetY = offsetY,
            Sheets = sheets,
        });
    }

    /// <summary>How many sheets a length needs.</summary>
    private static int Count(double length, double printable, double overlap) =>
        length <= printable + Tolerance
            ? 1
            : (int)Math.Min(int.MaxValue / 2, Math.Ceiling((length - overlap - Tolerance) / (printable - overlap)));

    private static double Share(PosterAlignment alignment) => alignment switch
    {
        PosterAlignment.Start => 0.0,
        PosterAlignment.Center => 0.5,
        _ => 1.0,
    };

    private static void Fail(AppError error, string message)
    {
        error.Kind = ErrorKind.Error;
        error.Message = message;
    }
}
