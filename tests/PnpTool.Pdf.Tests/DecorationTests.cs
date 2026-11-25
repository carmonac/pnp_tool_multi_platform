using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Pdf.Layout;

namespace PnpTool.Pdf.Tests;

/// <summary>
/// Cut lines, frames, marks and texts, checked on generated PDFs. Sheet and grid are those of
/// the standard style: A4, 3 × 3 cards of 180 × 252 points, the grid 27.64 points from the
/// sides and 42.94 from top and bottom.
/// </summary>
[Collection(GlobalState.Name)]
public sealed class DecorationTests : IDisposable
{
    private const double PageWidth = 595.28;
    private const double PageHeight = 841.89;
    private const double Left = (PageWidth - 540) / 2;
    private const double Bottom = (PageHeight - 756) / 2;

    private readonly Workspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    private Project Standard(int cards, CutLineType lines)
    {
        Project project = _workspace.Project("Cartas_Standar.pnps", cards);
        project.Settings.Front.CutLine.LineType = lines;
        project.Settings.Back.CutLine.LineType = lines;
        return project;
    }

    private string Generate(Project project) => PdfGenerator.Generate(project, _workspace.WorkFolder).Files[0];

    private static void AssertNear(double expected, double actual) => Assert.Equal(expected, actual, 1);

    // ------------------------------------------------------------ cut-line positions

    [Fact]
    public void Lines_outside_the_grid_run_from_the_sheet_edge_to_a_gap_before_the_cards()
    {
        Project project = Standard(1, CutLineType.Normal);
        PdfGenerator.Prepare(project);
        var layout = new PageLayout(PdfGenerator.Prepare(project), "id", project.Settings, _workspace.WorkFolder, project.AdvancedTemplate);

        PointD[,] rows = CutLines.Outside(columns: false, gap: 3, layout);
        PointD[,] columns = CutLines.Outside(columns: true, gap: 3, layout);

        // Four boundaries for three rows; no spacing or bleed, so neighbours share a line.
        Assert.Equal(4, rows.GetLength(0));
        Assert.Equal(4, columns.GetLength(0));
        for (int i = 0; i < 4; i++)
        {
            double y = Bottom + (i * 252);
            AssertNear(0, rows[i, 0].X);
            AssertNear(Left - 3, rows[i, 1].X);
            AssertNear(Left + 540 + 3, rows[i, 2].X);
            AssertNear(PageWidth, rows[i, 3].X);
            Assert.All(Enumerable.Range(0, 4), point => AssertNear(y, rows[i, point].Y));

            double x = Left + (i * 180);
            AssertNear(0, columns[i, 0].Y);
            AssertNear(Bottom - 3, columns[i, 1].Y);
            AssertNear(Bottom + 756 + 3, columns[i, 2].Y);
            AssertNear(PageHeight, columns[i, 3].Y);
            Assert.All(Enumerable.Range(0, 4), point => AssertNear(x, columns[i, point].X));
        }
    }

    [Fact]
    public void Bleed_gives_every_card_its_own_lines_inside_the_bleed()
    {
        Project project = Standard(1, CutLineType.Normal);
        project.Settings.Grid.Bleed = Unit.FromPoint(9);
        var layout = new PageLayout(PdfGenerator.Prepare(project), "id", project.Settings, _workspace.WorkFolder, project.AdvancedTemplate);

        PointD[,] columns = CutLines.Outside(columns: true, gap: 3, layout);

        Assert.Equal(6, columns.GetLength(0));
        double[] expected = [Left + 9, Left + 171, Left + 189, Left + 351, Left + 369, Left + 531];
        for (int i = 0; i < 6; i++)
        {
            AssertNear(expected[i], columns[i, 0].X);
        }

        // The gap is measured from the cut, which is inside the bleed.
        AssertNear(Bottom + 9 - 3, columns[0, 1].Y);
        AssertNear(Bottom + 756 - 9 + 3, columns[0, 2].Y);
    }

    [Fact]
    public void Spacing_separates_the_lines_of_neighbouring_cards()
    {
        Project project = Standard(1, CutLineType.Normal);
        project.Settings.Grid.Spacing.X = Unit.FromPoint(10);
        var layout = new PageLayout(PdfGenerator.Prepare(project), "id", project.Settings, _workspace.WorkFolder, project.AdvancedTemplate);

        PointD[,] columns = CutLines.Outside(columns: true, gap: 0, layout);

        double left = (PageWidth - 540 - 20) / 2;
        double[] expected = [left, left + 180, left + 190, left + 370, left + 380, left + 560];
        Assert.Equal(6, columns.GetLength(0));
        for (int i = 0; i < 6; i++)
        {
            AssertNear(expected[i], columns[i, 0].X);
        }
    }

    [Fact]
    public void Crosses_sit_on_every_card_corner_and_can_reach_the_sheet_edge()
    {
        Project project = Standard(1, CutLineType.Cross);
        var layout = new PageLayout(PdfGenerator.Prepare(project), "id", project.Settings, _workspace.WorkFolder, project.AdvancedTemplate);
        double arm = Unit.FromMillimeter(3).Point;

        LineD[,,] crosses = CutLines.Crosses(toEdge: false, layout, halfLength: 3);

        Assert.Equal(6, crosses.GetLength(0));
        Assert.Equal(6, crosses.GetLength(1));

        // Corner columns 1 and 2 are the two sides of the first boundary and coincide without spacing.
        AssertNear(Left + 180 - arm, crosses[1, 0, 0].From.X);
        AssertNear(Left + 180 + arm, crosses[1, 0, 0].To.X);
        AssertNear(Bottom, crosses[1, 0, 0].From.Y);
        AssertNear(crosses[1, 0, 0].From.X, crosses[2, 0, 0].From.X);
        AssertNear(Left + 180, crosses[1, 0, 1].From.X);
        AssertNear(Bottom - arm, crosses[1, 0, 1].From.Y);
        AssertNear(Bottom + arm, crosses[1, 0, 1].To.Y);

        LineD[,,] toEdge = CutLines.Crosses(toEdge: true, layout, halfLength: 3);
        AssertNear(0, toEdge[0, 2, 0].From.X);
        AssertNear(PageWidth, toEdge[5, 2, 0].To.X);
        AssertNear(0, toEdge[2, 0, 1].From.Y);
        AssertNear(PageHeight, toEdge[2, 5, 1].To.Y);
        AssertNear(Left + 180 - arm, toEdge[1, 2, 0].From.X);
    }

    // ------------------------------------------------------------ cut lines in the PDF

    [Theory]
    [InlineData(CutLineType.None, 0)]
    [InlineData(CutLineType.Normal, 16)]   // two stubs for each of 4 row and 4 column boundaries
    [InlineData(CutLineType.Full, 8)]
    [InlineData(CutLineType.Cross, 72)]    // 36 corners, two arms each
    [InlineData(CutLineType.Dot, 72)]
    [InlineData(CutLineType.Mixed, 48)]    // the 16 stubs plus crosses at the 16 inner corners
    public void Each_style_draws_its_number_of_lines(CutLineType type, int expected)
    {
        string file = Generate(Standard(9, type));

        List<DrawnPath> lines = [.. PdfInspection.PathsOn(file, 1).Where(path => !path.IsCurve)];

        Assert.Equal(expected, lines.Count);
    }

    [Fact]
    public void Normal_lines_keep_clear_of_the_cards_with_the_thickness_and_colour_set()
    {
        Project project = Standard(9, CutLineType.Normal);
        project.Settings.Front.CutLine.Thickness = Unit.FromPoint(0.8);
        project.Settings.Front.CutLine.Gap = 5;
        project.Settings.Front.CutLine.Color = "255_255_0_0";

        string file = Generate(project);

        List<DrawnPath> lines = PdfInspection.PathsOn(file, 1);
        Assert.All(lines, line => Assert.Equal(0.8, line.LineWidth, 2));
        List<DrawnPath> horizontal = [.. lines.Where(line => line.IsHorizontal)];
        Assert.Equal(8, horizontal.Count);
        Assert.All(horizontal, line => Assert.True(line.X2 <= Left - 5 + 0.1 || line.X1 >= Left + 540 + 5 - 0.1, line.ToString()));
        Assert.Contains(horizontal, line => Math.Abs(line.Y1 - Bottom) < 0.1 && Math.Abs(line.X1) < 0.1 && Math.Abs(line.X2 - (Left - 5)) < 0.1);

        // Red, in the margin left of the top row boundary.
        string color = PdfInspection.ColorAt(file, 1, 10, PageHeight - Bottom);
        Assert.True(PdfInspection.Close("#FF0000", color, 140), color);
    }

    [Fact]
    public void Full_lines_cross_the_whole_sheet()
    {
        string file = Generate(Standard(9, CutLineType.Full));

        List<DrawnPath> lines = PdfInspection.PathsOn(file, 1);
        Assert.Equal(4, lines.Count(line => line.IsHorizontal && line.Length > PageWidth - 1));
        Assert.Equal(4, lines.Count(line => line.IsVertical && line.Length > PageHeight - 1));
    }

    [Fact]
    public void Dots_are_crosses_half_a_millimetre_across()
    {
        string file = Generate(Standard(9, CutLineType.Dot));

        Assert.All(PdfInspection.PathsOn(file, 1), line => Assert.Equal(Unit.FromMillimeter(0.5).Point, line.Length, 1));
    }

    [Fact]
    public void Cut_lines_of_backs_follow_the_back_offset()
    {
        Project project = _workspace.Project("Cartas_Standar.pnps", 1, withBacks: true);
        project.Settings.PrintLayout.DoubleSided = true;
        project.Settings.PrintLayout.BackImage = BackImageMode.DifferentBack;
        project.Settings.Front.CutLine.LineType = CutLineType.Full;
        project.Settings.Back.CutLine.LineType = CutLineType.Full;
        project.Settings.BackOffset.X = Unit.FromPoint(5);

        string file = Generate(project);

        double[] fronts = [.. PdfInspection.PathsOn(file, 1).Where(line => line.IsVertical).Select(line => line.X1).Order()];
        double[] backs = [.. PdfInspection.PathsOn(file, 2).Where(line => line.IsVertical).Select(line => line.X1).Order()];
        Assert.Equal(4, backs.Length);
        for (int i = 0; i < 4; i++)
        {
            AssertNear(fronts[i] + 5, backs[i]);
        }

        // The offset is taken back afterwards: a second run gives the same fronts.
        double[] again = [.. PdfInspection.PathsOn(Generate(project), 1).Where(line => line.IsVertical).Select(line => line.X1).Order()];
        Assert.Equal(fronts, again);
    }

    // ------------------------------------------------------------ footer, ID, marks

    [Fact]
    public void Footer_shows_signature_and_page_numbers()
    {
        Project project = Standard(10, CutLineType.None);
        project.Settings.HeaderFooter.SignatureText = "Made with PNP Tool";
        project.Settings.HeaderFooter.ShowPageNumber = true;
        project.Settings.HeaderFooter.FirstPageNumber = 1;

        string file = Generate(project);

        Assert.Contains("Made with PNP Tool 1", PdfInspection.TextOn(file, 1));
        Assert.Contains("Made with PNP Tool 2", PdfInspection.TextOn(file, 2));
    }

    [Fact]
    public void Sheets_of_backs_are_numbered_after_their_fronts()
    {
        Project project = _workspace.Project("Cartas_Standar.pnps", 10, withBacks: true);
        project.Settings.PrintLayout.DoubleSided = true;
        project.Settings.PrintLayout.BackImage = BackImageMode.DifferentBack;
        project.Settings.HeaderFooter.ShowPageNumber = true;
        project.Settings.HeaderFooter.FirstPageNumber = 1;

        string file = Generate(project);

        Assert.Equal(["1", "1B", "2", "2B"], Enumerable.Range(1, 4).Select(page => PdfInspection.TextOn(file, page).Trim()));
    }

    [Fact]
    public void Sheet_without_footer_settings_has_no_text()
    {
        Project project = Standard(2, CutLineType.None);
        project.Settings.HeaderFooter.SignatureText = string.Empty;
        project.Settings.HeaderFooter.ShowPageNumber = false;

        Assert.Equal(string.Empty, PdfInspection.TextOn(Generate(project), 1).Trim());
    }

    [Fact]
    public void File_id_is_printed_on_every_sheet()
    {
        Project project = Standard(10, CutLineType.None);
        project.Settings.Other.GenerateFileId = true;

        string file = Generate(project);

        Assert.Contains("deck.pdf", PdfInspection.TextOn(file, 1));
        Assert.Contains("deck.pdf", PdfInspection.TextOn(file, 2));
    }

    [Fact]
    public void Perforation_marks_alternate_sides_on_consecutive_sheets()
    {
        Project project = Standard(10, CutLineType.None);
        project.Settings.Book.PerforationMarks.Type = PerforationMarkType.Inside;
        project.Settings.Book.PerforationMarks.Count = 4;
        project.Settings.Book.PerforationMarks.Margin = Unit.FromPoint(20);
        project.Settings.Book.PerforationMarks.Size = Unit.FromPoint(3);

        string file = Generate(project);

        List<DrawnPath> first = [.. PdfInspection.PathsOn(file, 1).Where(path => path.IsCurve)];
        List<DrawnPath> second = [.. PdfInspection.PathsOn(file, 2).Where(path => path.IsCurve)];
        Assert.Equal(4, first.Count);
        Assert.Equal(4, second.Count);

        // The first sheet is finished as sheet 1 (odd): "inside" marks at the near edge; the next at the far one.
        Assert.All(first, mark => Assert.InRange(mark.X1, 15, 25));
        Assert.All(second, mark => Assert.InRange(mark.X1, PageWidth - 25, PageWidth - 15));
        double[] heights = [.. first.Select(mark => mark.Y1).OrderDescending()];
        AssertNear(PageHeight * 4 / 5, heights[0]);
        AssertNear(PageHeight / 5, heights[3]);
    }

    [Fact]
    public void Calibration_marks_go_near_the_four_corners_and_the_strip_on_the_first_sheet_only()
    {
        Project project = Standard(10, CutLineType.None);
        HeaderFooter header = project.Settings.HeaderFooter;
        header.ShowCalibrationMark = true;
        header.CalibrationMarkSize = Unit.FromPoint(20);
        header.CalibrationMarkPosition = new UnitPoint { X = Unit.FromPoint(4), Y = Unit.FromPoint(6) };
        header.ShowCalibrationImage = true;
        header.CalibrationImageSize = new UnitPoint { X = Unit.FromPoint(200), Y = Unit.FromPoint(15) };

        string file = Generate(project);

        List<PlacedImage> first = PdfInspection.ImagesOn(file, 1);
        List<PlacedImage> marks = [.. first.Where(image => Math.Abs(image.Width - 20) < 0.5 && Math.Abs(image.Height - 20) < 0.5)];
        Assert.Equal(4, marks.Count);
        Assert.Equal(2, marks.Count(mark => mark.X < 40));
        Assert.Equal(2, marks.Count(mark => mark.X > PageWidth - 40));
        Assert.Equal(2, marks.Count(mark => mark.Y < 40));
        Assert.Equal(2, marks.Count(mark => mark.Y > PageHeight - 40));

        PlacedImage strip = Assert.Single(first, image => Math.Abs(image.Width - 200) < 0.5);
        AssertNear((PageWidth - 200) / 2, strip.X);
        Assert.True(strip.Y > PageHeight - 30);
        Assert.Equal(9 + 4 + 1, first.Count);

        List<PlacedImage> second = PdfInspection.ImagesOn(file, 2);
        Assert.Equal(1 + 4, second.Count);
    }

    // ------------------------------------------------------------ frames and watermark

    [Fact]
    public void Solid_outer_frame_shows_around_the_cards()
    {
        Project project = Standard(2, CutLineType.None);
        project.Settings.Front.ShowOuterFrame = true;
        project.Settings.Front.OuterFrameEffect = OuterFrameEffect.SolidColor;
        project.Settings.Front.OuterFrameColor = "255_10_200_30";
        project.Settings.Front.OuterFrameSize = Unit.FromPoint(8);

        string file = Generate(project);

        // A frame rectangle per card, drawn before the cards and 8 points larger all round.
        List<PlacedImage> images = PdfInspection.ImagesOn(file, 1);
        Assert.Equal(4, images.Count);
        AssertNear(Left - 8, images[0].X);
        AssertNear(196, images[0].Width);
        AssertNear(268, images[0].Height);
        AssertNear(180, images[2].Width);

        double top = PageHeight - Bottom;
        Assert.True(PdfInspection.Close("#0AC81E", PdfInspection.ColorAt(file, 1, Left - 4, top - 100)));
        Assert.True(PdfInspection.Close("#0AC81E", PdfInspection.ColorAt(file, 1, Left + 90, top + 4)));
        Assert.True(PdfInspection.Close(Workspace.Colors[0], PdfInspection.ColorAt(file, 1, Left + 90, top - 100)));
        Assert.True(PdfInspection.Close("#FFFFFF", PdfInspection.ColorAt(file, 1, Left - 14, top - 100)));

        // The third cell of the row is empty: no frame there.
        Assert.True(PdfInspection.Close("#FFFFFF", PdfInspection.ColorAt(file, 1, Left + 540 + 4, top - 100)));
    }

    [Fact]
    public void Mirror_outer_frame_continues_the_edge_cards_outwards()
    {
        Project project = Standard(9, CutLineType.None);
        project.Settings.Front.ShowOuterFrame = true;
        project.Settings.Front.OuterFrameEffect = OuterFrameEffect.Mirror;
        project.Settings.Front.OuterFrameSize = Unit.FromPoint(10);

        string file = Generate(project);

        List<PlacedImage> images = PdfInspection.ImagesOn(file, 1);
        Assert.Equal(10, images.Count);
        AssertNear(Left - 10, images[0].X);
        AssertNear(Bottom - 10, images[0].Y);
        AssertNear(560, images[0].Width);
        AssertNear(776, images[0].Height);

        double top = PageHeight - Bottom;
        Assert.True(PdfInspection.Close(Workspace.Colors[0], PdfInspection.ColorAt(file, 1, Left - 5, top - 100), 30));
        Assert.True(PdfInspection.Close(Workspace.Colors[2], PdfInspection.ColorAt(file, 1, Left + 540 + 5, top - 100), 30));
        Assert.True(PdfInspection.Close(Workspace.Colors[7], PdfInspection.ColorAt(file, 1, Left + 270, Bottom - 5), 30));
        Assert.True(PdfInspection.Close("#FFFFFF", PdfInspection.ColorAt(file, 1, Left - 16, top - 100)));
    }

    [Fact]
    public void Page_watermark_covers_the_sheet_on_top_of_the_cards()
    {
        Project project = Standard(1, CutLineType.None);
        string watermark = _workspace.File("mark.png");
        using (var image = new ImageMagick.MagickImage(new ImageMagick.MagickColor("#000000"), 60, 85))
        {
            image.Write(watermark);
        }

        project.Settings.Watermark.Type = WatermarkType.OnPage;
        project.Settings.Watermark.Path = watermark;

        string file = Generate(project);

        PlacedImage last = PdfInspection.ImagesOn(file, 1)[^1];
        AssertNear(0, last.X);
        AssertNear(PageWidth, last.Width);
        AssertNear(PageHeight, last.Height);
        Assert.True(PdfInspection.Close("#000000", PdfInspection.ColorAt(file, 1, Left + 90, PageHeight - Bottom - 100)));
    }

    [Fact]
    public void Cut_line_styles_are_listed_with_their_texts()
    {
        CutLineTypeOption[] all = CutLineTypeOption.All();

        Assert.Equal(
            [CutLineType.None, CutLineType.Normal, CutLineType.Cross, CutLineType.Full, CutLineType.Mixed, CutLineType.Dot],
            all.Select(option => option.Value));
        Assert.Equal("Mix", all[4].Text);
        Assert.DoesNotContain(all, option => option.Text.StartsWith("Need Translation", StringComparison.Ordinal));
        Assert.Equal([CutLineType.None, CutLineType.Cross], CutLineTypeOption.Simple().Select(option => option.Value));
    }
}
