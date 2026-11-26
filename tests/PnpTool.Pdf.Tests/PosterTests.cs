using ImageMagick;
using ImageMagick.Drawing;
using iText.Kernel.Pdf;
using PnpTool.Core;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Pdf.Tools;

namespace PnpTool.Pdf.Tests;

/// <summary>
/// Posters made of a picture of four colours, 400 points square: red and blue above, green
/// and yellow below. Sheets are 240 points square with 20 of margin and 20 of overlap.
/// </summary>
[Collection(GlobalState.Name)]
public sealed class PosterTests : IDisposable
{
    private const string Red = "#FF0000";
    private const string Blue = "#0000FF";
    private const string Green = "#00FF00";
    private const string Yellow = "#FFFF00";
    private const string White = "#FFFFFF";

    private readonly Workspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    private string Picture()
    {
        string path = _workspace.File("out", "cuatro.png");
        using var image = new MagickImage(new MagickColor(Red), 400, 400) { Format = MagickFormat.Png };
        new Drawables()
            .FillColor(new MagickColor(Blue)).Rectangle(200, 0, 399, 199)
            .FillColor(new MagickColor(Green)).Rectangle(0, 200, 199, 399)
            .FillColor(new MagickColor(Yellow)).Rectangle(200, 200, 399, 399)
            .Draw(image);
        image.Density = new Density(72, 72, DensityUnit.PixelsPerInch);
        image.Write(path);
        return path;
    }

    private PosterSettings Settings() => new()
    {
        ImagePath = Picture(),
        Paper = new UnitPoint { X = Unit.FromPoint(240), Y = Unit.FromPoint(240) },
        Margin = Unit.FromPoint(20),
        Overlap = Unit.FromPoint(20),
        SizeMode = PosterSizeMode.Width,
        Width = Unit.FromPoint(380),
    };

    /// <summary>The colour at a point of a page measured from its top-left corner.</summary>
    private static string At(string file, int page, double x, double y) => PdfInspection.ColorAt(file, page, x, 240 - y);

    private static void Same(string expected, string actual) => Assert.True(PdfInspection.Close(expected, actual), expected + " expected, found " + actual);

    [Fact]
    public void Four_sheets_show_the_four_parts_and_repeat_the_overlap()
    {
        string file = _workspace.File("out", "poster.pdf");
        var progress = new List<int>();

        AppError error = PosterWriter.Write(file, Settings(), progress.Add);

        Assert.False(error.HasError, error.Message);
        Assert.Equal(4, PdfInspection.PageCount(file));
        Assert.Equal((240.0, 240.0), PdfInspection.PageSize(file, 1));
        Assert.Equal([0, 25, 50, 75, 100], progress);

        // The poster is the picture reduced to 380: the colours change at 190.
        Same(Red, At(file, 1, 100, 100));
        Same(Blue, At(file, 2, 150, 100));
        Same(Green, At(file, 3, 100, 150));
        Same(Yellow, At(file, 4, 150, 150));

        // The strip from 180 to 200 of the poster is on both sheets of a row: at 200-220 of
        // the first and at 20-40 of the second, with the change of colour inside it.
        Same(Red, At(file, 1, 205, 100));
        Same(Red, At(file, 2, 25, 100));
        Same(Blue, At(file, 1, 215, 100));
        Same(Blue, At(file, 2, 35, 100));

        // And the same between rows.
        Same(Red, At(file, 1, 100, 205));
        Same(Red, At(file, 3, 100, 25));
        Same(Green, At(file, 1, 100, 215));
        Same(Green, At(file, 3, 100, 35));

        // Nothing is printed on the margins, however far the picture goes on.
        Same(White, At(file, 1, 230, 100));
        Same(White, At(file, 1, 100, 232));
        Same(White, At(file, 2, 8, 100));
        Same(White, At(file, 4, 100, 8));
    }

    [Fact]
    public void The_picture_is_stored_once_for_all_the_sheets()
    {
        string file = _workspace.File("out", "poster.pdf");
        PosterWriter.Write(file, Settings());

        using var pdf = new PdfDocument(new PdfReader(file));
        var pictures = new HashSet<int>();
        for (int page = 1; page <= pdf.GetNumberOfPages(); page++)
        {
            PdfDictionary objects = pdf.GetPage(page).GetResources().GetResource(PdfName.XObject);
            Assert.Single(objects.KeySet());
            pictures.Add(objects.GetAsStream(objects.KeySet().First()).GetIndirectReference().GetObjNumber());
            Assert.Single(PdfInspection.ImagesOn(file, page));
        }

        Assert.Single(pictures);
    }

    [Fact]
    public void Marks_are_drawn_in_the_margins_at_the_corners_of_the_printed_area()
    {
        string file = _workspace.File("out", "poster.pdf");
        PosterWriter.Write(file, Settings());

        List<DrawnPath> marks = PdfInspection.PathsOn(file, 1).Where(path => path.IsHorizontal || path.IsVertical).ToList();
        Assert.Equal(8, marks.Count);
        Assert.All(marks, mark => Assert.Equal(Unit.FromMillimeter(5.0).Point, mark.Length, 1));
        Assert.All(marks, mark => Assert.True(
            Math.Max(mark.X1, mark.X2) <= 20.01 || Math.Min(mark.X1, mark.X2) >= 219.99
            || Math.Max(mark.Y1, mark.Y2) <= 20.01 || Math.Min(mark.Y1, mark.Y2) >= 219.99));

        PosterSettings plain = Settings();
        plain.CutMarks = false;
        PosterWriter.Write(file, plain);
        Assert.DoesNotContain(PdfInspection.PathsOn(file, 1), path => path.IsHorizontal || path.IsVertical);
    }

    [Fact]
    public void A_poster_smaller_than_its_sheets_is_centred_on_white()
    {
        string file = _workspace.File("out", "poster.pdf");
        PosterSettings settings = Settings();
        settings.Width = Unit.FromPoint(300);       // two sheets cover 380: 40 free at each side

        Assert.False(PosterWriter.Write(file, settings).HasError);

        Assert.Equal(4, PdfInspection.PageCount(file));
        Same(White, At(file, 1, 40, 100));           // 20 of margin and 40 before the poster
        Same(White, At(file, 1, 100, 40));
        Same(Red, At(file, 1, 70, 70));
        Same(Yellow, At(file, 4, 150, 150));
        Same(White, At(file, 4, 200, 150));          // the poster ends at 20 + 40 + 300 - 180 = 180
    }

    [Fact]
    public void A_missing_image_or_wrong_settings_write_nothing()
    {
        string file = _workspace.File("out", "nada.pdf");
        PosterSettings settings = Settings();
        settings.ImagePath = _workspace.File("out", "no-existe.png");

        Assert.True(PosterWriter.Write(file, settings).HasError);
        Assert.False(File.Exists(file));

        settings = Settings();
        settings.Overlap = Unit.FromPoint(500);
        Assert.True(PosterWriter.Write(file, settings).HasError);
        Assert.False(File.Exists(file));
    }
}
