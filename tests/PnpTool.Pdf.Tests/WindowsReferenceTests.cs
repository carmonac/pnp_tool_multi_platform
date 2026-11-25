using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Pdf.Layout;

namespace PnpTool.Pdf.Tests;

/// <summary>
/// Compares the engine with a PDF made by PNP Tool for Windows. The numbers below were
/// measured from that PDF (two A4 sheets: nine cards and their shared back); the PDF itself
/// holds a published game's artwork and is not part of the repository.
/// </summary>
/// <remarks>
/// The reference was produced in 2023 by an older version of the program, which wrote PDFs
/// with another library, and reached us re-saved by macOS. Card boxes, cut lines and
/// calibration marks are plain geometry and are expected to match to a hundredth of a point.
/// Text positions depend on the PDF library and are compared loosely.
/// <para>
/// Settings read back from the geometry: cards of 65.5 × 90.9 mm (63.5 × 88.9 plus 1 mm of
/// bleed all round), 2 mm apart, 3 × 3 on A4, double-sided with the same back for every card;
/// cut lines of the "normal" style, 1 point thick, 3 points clear of the cards; calibration
/// marks of 10 points in the very corners (a printer margin of zero); calibration strip of
/// 200 × 15 points; page numbers in the centre.
/// </para>
/// Coordinates are in points from the top-left corner of the sheet, as the measuring tool gave them.
/// </remarks>
[Collection(GlobalState.Name)]
public sealed class WindowsReferenceTests : IDisposable
{
    private const double PageWidth = 595.276;
    private const double PageHeight = 841.89;

    /// <summary>Left and top of the three columns and rows of cards, and their size.</summary>
    private static readonly double[] ColumnLeft = [13.46, 204.80, 396.14];
    private static readonly double[] RowTop = [28.77, 292.11, 555.45];
    private const double CardWidth = 185.67;
    private const double CardHeight = 257.67;

    /// <summary>Horizontal cut lines: their height, running 0 → 13.30 and 581.98 → 595.28.</summary>
    private static readonly double[] HorizontalLines = [31.61, 283.61, 294.95, 546.95, 558.28, 810.28];

    /// <summary>Vertical cut lines: their position, running 0 → 28.61 and 813.28 → 841.89.</summary>
    private static readonly double[] VerticalLines = [16.30, 196.30, 207.64, 387.64, 398.98, 578.98];

    private readonly Workspace _workspace = new();
    private readonly UnitPoint _printerMargin = AppSettings.PrinterMargin;
    private readonly string _file;

    public WindowsReferenceTests()
    {
        AppSettings.PrinterMargin = new UnitPoint();
        Project project = _workspace.Project("Cartas_Standar.pnps", cards: 5);
        ProjectSettings settings = project.Settings;
        var card = new UnitPoint { X = Unit.FromMillimeter(65.5), Y = Unit.FromMillimeter(90.9) };
        settings.Grid.CardSize = new UnitPoint(card);
        settings.Front.Size = new UnitPoint(card);
        settings.Back.Size = new UnitPoint(card);
        settings.Grid.Spacing = new UnitPoint { X = Unit.FromMillimeter(2), Y = Unit.FromMillimeter(2) };
        settings.Grid.Bleed = Unit.FromMillimeter(1);
        settings.PrintLayout.Rows = 3;
        settings.PrintLayout.Columns = 3;
        settings.PrintLayout.DoubleSided = true;
        settings.PrintLayout.BackImage = BackImageMode.SameBack;
        settings.SharedBack = _workspace.Face("shared", "#102030", FaceType.Back);
        foreach (FaceSettings face in new[] { settings.Front, settings.Back })
        {
            face.CutLine.LineType = CutLineType.Normal;
            face.CutLine.Thickness = Unit.FromPoint(1);
            face.CutLine.Gap = 3;
            face.CutLine.Color = "255_0_0_0";
        }

        settings.HeaderFooter.ShowCalibrationMark = true;
        settings.HeaderFooter.CalibrationMarkSize = Unit.FromPoint(10);
        settings.HeaderFooter.CalibrationMarkPosition = new UnitPoint();
        settings.HeaderFooter.ShowCalibrationImage = true;
        settings.HeaderFooter.CalibrationImageSize = new UnitPoint { X = Unit.FromPoint(200), Y = Unit.FromPoint(15) };
        settings.HeaderFooter.ShowPageNumber = true;
        settings.HeaderFooter.FirstPageNumber = 1;
        settings.HeaderFooter.PageNumberPosition = PageNumberPosition.Center;
        settings.HeaderFooter.SignatureText = string.Empty;

        // Nine cards on the sheet: one, one, three of the same, three of the same, one.
        int[] copies = [1, 1, 3, 3, 1];
        for (int i = 0; i < copies.Length; i++)
        {
            project.Deck.Cards[i].Copies = copies[i];
        }

        project.Deck.RecountCopies();
        _file = PdfGenerator.Generate(project, _workspace.WorkFolder).Files[0];
    }

    public void Dispose()
    {
        AppSettings.PrinterMargin = _printerMargin;
        _workspace.Dispose();
    }

    /// <summary>An image of the generated PDF as left, top, width, height from the top-left corner.</summary>
    private static (double Left, double Top, double Width, double Height) FromTop(PlacedImage image) =>
        (image.X, PageHeight - image.Y - image.Height, image.Width, image.Height);

    private static void AssertNear(double expected, double actual, double tolerance = 0.02) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance, $"expected {expected}, found {actual}");

    [Fact]
    public void Document_has_the_two_a4_sheets_of_the_reference()
    {
        Assert.Equal(2, PdfInspection.PageCount(_file));
        (double width, double height) = PdfInspection.PageSize(_file, 1);
        AssertNear(PageWidth, width);
        AssertNear(PageHeight, height);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Cards_are_where_the_reference_has_them(int page)
    {
        List<(double Left, double Top, double Width, double Height)> cards =
        [
            .. PdfInspection.ImagesOn(_file, page).Select(FromTop).Where(image => Math.Abs(image.Width - CardWidth) < 1),
        ];

        Assert.Equal(9, cards.Count);
        foreach (double left in ColumnLeft)
        {
            foreach (double top in RowTop)
            {
                (double Left, double Top, double Width, double Height) card =
                    Assert.Single(cards, box => Math.Abs(box.Left - left) < 0.02 && Math.Abs(box.Top - top) < 0.02);
                AssertNear(CardWidth, card.Width);
                AssertNear(CardHeight, card.Height);
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Cut_lines_are_the_twenty_four_of_the_reference(int page)
    {
        List<DrawnPath> lines = [.. PdfInspection.PathsOn(_file, page).Where(path => !path.IsCurve)];

        Assert.Equal(24, lines.Count);
        Assert.All(lines, line => AssertNear(1.0, line.LineWidth, 0.001));

        foreach (double fromTop in HorizontalLines)
        {
            double y = PageHeight - fromTop;
            Assert.Single(lines, line => line.IsHorizontal && Near(line.Y1, y) && Near(Math.Min(line.X1, line.X2), 0) && Near(Math.Max(line.X1, line.X2), 13.30));
            Assert.Single(lines, line => line.IsHorizontal && Near(line.Y1, y) && Near(Math.Min(line.X1, line.X2), 581.98) && Near(Math.Max(line.X1, line.X2), 595.28));
        }

        foreach (double x in VerticalLines)
        {
            Assert.Single(lines, line => line.IsVertical && Near(line.X1, x) && Near(Math.Max(line.Y1, line.Y2), PageHeight) && Near(Math.Min(line.Y1, line.Y2), PageHeight - 28.61));
            Assert.Single(lines, line => line.IsVertical && Near(line.X1, x) && Near(Math.Max(line.Y1, line.Y2), PageHeight - 813.28) && Near(Math.Min(line.Y1, line.Y2), 0));
        }

        static bool Near(double a, double b) => Math.Abs(a - b) <= 0.02;
    }

    [Fact]
    public void Calibration_marks_are_in_the_four_corners()
    {
        List<(double Left, double Top, double Width, double Height)> marks =
        [
            .. PdfInspection.ImagesOn(_file, 1).Select(FromTop).Where(image => Math.Abs(image.Width - 10) < 0.02 && Math.Abs(image.Height - 10) < 0.02),
        ];

        Assert.Equal(4, marks.Count);
        foreach ((double left, double top) in new[] { (0.0, 0.0), (585.28, 0.0), (585.28, 831.89), (0.0, 831.89) })
        {
            Assert.Single(marks, mark => Math.Abs(mark.Left - left) < 0.02 && Math.Abs(mark.Top - top) < 0.02);
        }
    }

    [Fact]
    public void Calibration_strip_is_centred_at_the_top_of_the_first_sheet()
    {
        (double Left, double Top, double Width, double Height) strip =
            Assert.Single(PdfInspection.ImagesOn(_file, 1).Select(FromTop), image => Math.Abs(image.Width - 200) < 0.02);

        AssertNear(197.64, strip.Left);
        AssertNear(0.0, strip.Top);
        AssertNear(15.0, strip.Height);
        Assert.DoesNotContain(PdfInspection.ImagesOn(_file, 2), image => Math.Abs(image.Width - 200) < 0.02);
    }

    [Fact]
    public void Sheets_are_numbered_as_in_the_reference()
    {
        Assert.Equal("1", PdfInspection.TextOn(_file, 1).Trim());

        // The reference prints "1B" on the sheet of the shared back; the current Windows
        // version prints "BACK" there (PageLayout.PageNumberText), and so does the port.
        Assert.Contains("BACK", PdfInspection.TextOn(_file, 2));
    }

    [Fact]
    public void Repeated_cards_reuse_their_image_as_in_the_reference()
    {
        // The reference stores five card images for the nine cards of the first sheet.
        using var document = new iText.Kernel.Pdf.PdfDocument(new iText.Kernel.Pdf.PdfReader(_file));
        var images = document.GetPage(1).GetResources().GetResource(iText.Kernel.Pdf.PdfName.XObject);
        var cardImages = new HashSet<int>();
        foreach (iText.Kernel.Pdf.PdfName name in images.KeySet())
        {
            iText.Kernel.Pdf.PdfStream stream = images.GetAsStream(name);
            if (stream.GetAsNumber(iText.Kernel.Pdf.PdfName.Width)?.IntValue() == 180)
            {
                cardImages.Add(stream.GetIndirectReference().GetObjNumber());
            }
        }

        Assert.Equal(5, cardImages.Count);
    }
}
