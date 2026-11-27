using ImageMagick;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Pdf.Layout;
using PnpTool.Pdf.Tabletop;

namespace PnpTool.Pdf.Tests;

/// <summary>The sheets of cards written for Tabletop Simulator.</summary>
[Collection(GlobalState.Name)]
public sealed class TabletopSheetsTests : IDisposable
{
    private readonly Workspace _workspace = new();
    private readonly int _dpi = AppSettings.TargetDpi;

    public TabletopSheetsTests() => AppSettings.TargetDpi = 254;     // 10 pixels a millimetre

    public void Dispose()
    {
        AppSettings.TargetDpi = _dpi;
        _workspace.Dispose();
    }

    /// <summary>Settings for cards of the given size in millimetres.</summary>
    private static ProjectSettings Cards(double width = 4, double height = 6)
    {
        var settings = new ProjectSettings();
        settings.Grid.CardSize = new UnitPoint { X = Unit.FromMillimeter(width), Y = Unit.FromMillimeter(height) };
        return settings;
    }

    private string Picture(string name, string color, uint width = 80, uint height = 120)
    {
        string path = _workspace.File("out", name);
        using var image = new MagickImage(new MagickColor(color), width, height);
        image.Write(path);
        return path;
    }

    private PdfImage Image(string name, string color) => new(ImageKind.Normal, Picture(name, color), 0, false, false, false);

    private static PdfImage Blank(string color) => new(ImageKind.Blank, color, 0, false, false, false);

    private static PdfImage Empty() => new(ImageKind.Empty, string.Empty, 0, false, false, false);

    /// <summary>The colour in the middle of a cell of a sheet, as "#RRGGBB".</summary>
    private static string Cell(string file, int column, int row, PointI cell)
    {
        using var image = new MagickImage(file);
        IMagickColor<ushort> color = image.GetPixels().GetPixel((column * cell.X) + (cell.X / 2), (row * cell.Y) + (cell.Y / 2)).ToColor()!;
        return "#" + (color.R >> 8).ToString("X2") + (color.G >> 8).ToString("X2") + (color.B >> 8).ToString("X2");
    }

    [Fact]
    public void Table_is_kept_within_what_tabletop_simulator_takes()
    {
        Assert.Equal(2, TabletopSheets.RestrictColumns(1, restrict: true));
        Assert.Equal(10, TabletopSheets.RestrictColumns(14, restrict: true));
        Assert.Equal(6, TabletopSheets.RestrictColumns(6, restrict: true));
        Assert.Equal(2, TabletopSheets.RestrictRows(0, restrict: true));
        Assert.Equal(7, TabletopSheets.RestrictRows(9, restrict: true));
        Assert.Equal(14, TabletopSheets.RestrictColumns(14, restrict: false));
        Assert.Equal(1, TabletopSheets.RestrictRows(1, restrict: false));
    }

    [Fact]
    public void Sheet_too_large_is_made_smaller_keeping_the_shape_of_the_cards()
    {
        AppSettings.TargetDpi = 300;
        var sheets = new TabletopSheets([], false, Cards(63.5, 88.9)) { Columns = 10, Rows = 7 };

        // 7500 x 7350 pixels at full size: too tall.
        PointI cell = sheets.CellSize();
        PointI sheet = sheets.SheetSize();
        Assert.True(sheet.X <= TabletopSheets.MaximumWidth && sheet.Y <= TabletopSheets.MaximumHeight, sheet.X + " x " + sheet.Y);
        Assert.InRange(sheet.Y, TabletopSheets.MaximumHeight - 7, TabletopSheets.MaximumHeight);
        Assert.Equal(750.0 / 1050.0, (double)cell.X / cell.Y, 2);

        // Only too wide: the Windows program narrowed the cards and left their height.
        sheets.Columns = 14;
        sheets.Rows = 2;
        sheets.ApplyRestrictions = true;
        cell = sheets.CellSize();
        Assert.True(cell.X * 14 <= TabletopSheets.MaximumWidth);
        Assert.Equal(750.0 / 1050.0, (double)cell.X / cell.Y, 2);

        sheets.ApplyRestrictions = false;
        Assert.Equal((750, 1050), (sheets.CellSize().X, sheets.CellSize().Y));
        Assert.Equal((10500, 2100), (sheets.SheetSize().X, sheets.SheetSize().Y));
    }

    [Fact]
    public void Cards_fill_the_table_row_by_row_and_go_on_in_another_sheet()
    {
        PdfImage[] images =
        [
            Image("a.png", "#FF0000"), Image("b.png", "#00FF00"), Image("c.png", "#0000FF"), Image("d.png", "#FFFF00"),
            Image("e.png", "#FF00FF"),
        ];
        var sheets = new TabletopSheets(images, false, Cards()) { Columns = 2, Rows = 2 };
        Assert.Equal(5, sheets.CardCount);
        Assert.Equal(2, sheets.SheetCount);
        var progress = new List<int>();

        List<string> files = sheets.Write(_workspace.File("out", "deck"), progress.Add);

        Assert.Equal([_workspace.File("out", "deck_0_front.png"), _workspace.File("out", "deck_1_front.png")], files);
        Assert.Equal([1, 2, 3, 4, 5], progress);
        PointI cell = sheets.CellSize();
        Assert.Equal((40, 60), (cell.X, cell.Y));
        using (var first = new MagickImage(files[0]))
        {
            Assert.Equal(80u, first.Width);
            Assert.Equal(120u, first.Height);
        }

        Assert.Equal("#FF0000", Cell(files[0], 0, 0, cell));
        Assert.Equal("#00FF00", Cell(files[0], 1, 0, cell));
        Assert.Equal("#0000FF", Cell(files[0], 0, 1, cell));
        Assert.Equal("#FFFF00", Cell(files[0], 1, 1, cell));
        Assert.Equal("#FF00FF", Cell(files[1], 0, 0, cell));

        // The places left over are white, also on the second sheet, which the Windows program left black.
        Assert.Equal("#FFFFFF", Cell(files[1], 1, 0, cell));
        Assert.Equal("#FFFFFF", Cell(files[1], 1, 1, cell));
    }

    [Fact]
    public void Cards_that_fill_the_last_sheet_leave_no_empty_one()
    {
        PdfImage[] images = [Image("a.png", "#FF0000"), Image("b.png", "#00FF00"), Image("c.png", "#0000FF"), Image("d.png", "#FFFF00")];
        var sheets = new TabletopSheets(images, false, Cards()) { Columns = 2, Rows = 2 };

        List<string> files = sheets.Write(_workspace.File("out", "full"));

        Assert.Single(files);
        Assert.Equal(1, sheets.SheetCount);
    }

    [Fact]
    public void Fronts_and_backs_go_to_sheets_of_their_own_at_the_same_places()
    {
        PdfImage[] images =
        [
            Image("f1.png", "#FF0000"), Image("b1.png", "#800000"),
            Image("f2.png", "#00FF00"), Blank("255_0_0_128"),
            Image("f3.png", "#0000FF"), Empty(),
        ];
        var sheets = new TabletopSheets(images, true, Cards()) { Columns = 2, Rows = 2 };
        Assert.Equal(3, sheets.CardCount);

        List<string> files = sheets.Write(_workspace.File("out", "two"));

        Assert.Equal([_workspace.File("out", "two_0_back.png"), _workspace.File("out", "two_0_front.png")], files);
        PointI cell = sheets.CellSize();
        Assert.Equal("#FF0000", Cell(files[1], 0, 0, cell));
        Assert.Equal("#00FF00", Cell(files[1], 1, 0, cell));
        Assert.Equal("#0000FF", Cell(files[1], 0, 1, cell));
        Assert.Equal("#800000", Cell(files[0], 0, 0, cell));
        Assert.Equal("#000080", Cell(files[0], 1, 0, cell));     // a blank card is its colour
        Assert.Equal("#FFFFFF", Cell(files[0], 0, 1, cell));     // an empty one is white
    }

    [Fact]
    public void Images_are_turned_as_the_cards_are_and_stretched_to_the_card()
    {
        // Red on the left half, blue on the right; turned a quarter it is red above blue.
        string path = _workspace.File("out", "halves.png");
        using (var halves = new MagickImage(new MagickColor("#0000FF"), 120, 80))
        {
            using var left = new MagickImage(new MagickColor("#FF0000"), 60, 80);
            halves.Composite(left, 0, 0, CompositeOperator.Over);
            halves.Write(path);
        }

        PdfImage[] images = [new(ImageKind.Normal, path, 90, false, false, false), Image("b.png", "#00FF00")];
        var sheets = new TabletopSheets(images, false, Cards()) { Columns = 2, Rows = 2 };

        string file = sheets.Write(_workspace.File("out", "turned")).Single();

        using var sheet = new MagickImage(file);
        IPixelCollection<ushort> pixels = sheet.GetPixels();
        Assert.True(pixels.GetPixel(20, 10).ToColor()!.R > 60000 && pixels.GetPixel(20, 10).ToColor()!.B < 5000);
        Assert.True(pixels.GetPixel(20, 50).ToColor()!.B > 60000 && pixels.GetPixel(20, 50).ToColor()!.R < 5000);
    }

    [Fact]
    public void Files_are_of_the_kind_of_the_first_image_or_png_without_one()
    {
        string jpeg = Picture("first.jpg", "#FF0000");
        PdfImage[] images = [Blank("255_10_10_10"), new(ImageKind.Normal, jpeg, 0, false, false, false), Image("other.png", "#00FF00")];
        var sheets = new TabletopSheets(images, false, Cards()) { Columns = 2, Rows = 2 };

        Assert.EndsWith("jpg_0_front.jpg", sheets.Write(_workspace.File("out", "jpg")).Single());

        // The Windows program took the kind from the first entry, which here is a colour.
        var colours = new TabletopSheets([Blank("255_10_10_10"), Blank("255_200_0_0")], false, Cards()) { Columns = 2, Rows = 2 };
        string file = colours.Write(_workspace.File("out", "colours")).Single();
        Assert.EndsWith("colours_0_front.png", file);
        Assert.Equal("#C80000", Cell(file, 1, 0, colours.CellSize()));
    }

    [Fact]
    public void Image_that_is_not_there_leaves_its_place_white()
    {
        PdfImage[] images = [Image("a.png", "#FF0000"), new(ImageKind.Normal, _workspace.File("out", "gone.png"), 0, false, false, false)];
        var sheets = new TabletopSheets(images, false, Cards()) { Columns = 2, Rows = 2 };

        string file = sheets.Write(_workspace.File("out", "gone")).Single();

        Assert.Equal("#FF0000", Cell(file, 0, 0, sheets.CellSize()));
        Assert.Equal("#FFFFFF", Cell(file, 1, 0, sheets.CellSize()));
    }

    [Fact]
    public void Nothing_to_write_writes_nothing_and_stopping_stops()
    {
        Assert.Empty(new TabletopSheets([], false, Cards()).Write(_workspace.File("out", "none")));
        Assert.Empty(new TabletopSheets([Blank("255_0_0_0")], false, Cards()) { Columns = 0 }.Write(_workspace.File("out", "none")));

        using var stop = new CancellationTokenSource();
        stop.Cancel();
        var sheets = new TabletopSheets([Blank("255_0_0_0")], false, Cards()) { Columns = 2, Rows = 2 };
        Assert.Throws<OperationCanceledException>(() => sheets.Write(_workspace.File("out", "stopped"), null, stop.Token));
        Assert.False(File.Exists(_workspace.File("out", "stopped_0_front.png")));
    }
}
