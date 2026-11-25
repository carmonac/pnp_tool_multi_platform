using iText.Kernel.Pdf;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Files;
using PnpTool.Core.Projects;
using PnpTool.Pdf.Layout;

namespace PnpTool.Pdf.Tests;

/// <summary>
/// Generates real PDFs with no user interface and checks page size, where each card lands and
/// which card it is. The standard style is A4 with 3 × 3 cards of 63.5 × 88.9 mm (180 × 252
/// points) and no spacing, so the grid starts 27.64 points from the left and 42.94 from the top.
/// </summary>
[Collection(GlobalState.Name)]
public sealed class PdfGenerationTests : IDisposable
{
    private const double PageWidth = 595.28;
    private const double PageHeight = 841.89;
    private const double CardWidth = 180;
    private const double CardHeight = 252;
    private const double Left = (PageWidth - (3 * CardWidth)) / 2;
    private const double Top = (PageHeight - (3 * CardHeight)) / 2;

    private readonly Workspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    /// <summary>Bottom-left corner of the cell at a column and row (row 0 is the top one), in PDF coordinates.</summary>
    private static (double X, double Y) Cell(int column, int row) =>
        (Left + (column * CardWidth), PageHeight - Top - ((row + 1) * CardHeight));

    private static (double X, double Y) Centre(int column, int row)
    {
        (double x, double y) = Cell(column, row);
        return (x + (CardWidth / 2), y + (CardHeight / 2));
    }

    private static void AssertBox(PlacedImage image, int column, int row)
    {
        (double x, double y) = Cell(column, row);
        Assert.Equal(x, image.X, 1);
        Assert.Equal(y, image.Y, 1);
        Assert.Equal(CardWidth, image.Width, 1);
        Assert.Equal(CardHeight, image.Height, 1);
    }

    private void AssertColor(string file, int page, int column, int row, string expected)
    {
        (double x, double y) = Centre(column, row);
        string actual = PdfInspection.ColorAt(file, page, x, y);
        Assert.True(PdfInspection.Close(expected, actual), $"page {page}, column {column}, row {row}: expected {expected}, found {actual}");
    }

    [Fact]
    public void Standard_cards_fill_a4_sheets_three_by_three()
    {
        Project project = _workspace.Project("Cartas_Standar.pnps", cards: 10);

        PdfOutput output = PdfGenerator.Generate(project, _workspace.WorkFolder);

        string file = Assert.Single(output.Files);
        Assert.True(output.SingleFile);
        Assert.Equal(_workspace.File("out", "deck.pdf"), file);
        Assert.Equal(2, PdfInspection.PageCount(file));
        (double width, double height) = PdfInspection.PageSize(file, 1);
        Assert.Equal(PageWidth, width, 1);
        Assert.Equal(PageHeight, height, 1);

        List<PlacedImage> first = PdfInspection.ImagesOn(file, 1);
        Assert.Equal(9, first.Count);
        for (int i = 0; i < 9; i++)
        {
            AssertBox(first[i], column: i % 3, row: i / 3);
        }

        // The tenth card is alone at the top left of the second sheet.
        PlacedImage tenth = Assert.Single(PdfInspection.ImagesOn(file, 2));
        AssertBox(tenth, 0, 0);

        // Cards are placed in reading order: left to right, top to bottom.
        AssertColor(file, 1, 0, 0, Workspace.Colors[0]);
        AssertColor(file, 1, 2, 0, Workspace.Colors[2]);
        AssertColor(file, 1, 0, 1, Workspace.Colors[3]);
        AssertColor(file, 1, 2, 2, Workspace.Colors[8]);
        AssertColor(file, 2, 0, 0, Workspace.Colors[9]);
        AssertColor(file, 2, 1, 0, "#FFFFFF");
    }

    [Fact]
    public void Work_folder_is_left_without_sheet_files()
    {
        Project project = _workspace.Project("Cartas_Standar.pnps", cards: 10);

        PdfGenerator.Generate(project, _workspace.WorkFolder);

        Assert.Empty(Directory.GetFiles(_workspace.WorkFolder, "*.pdf"));
    }

    [Fact]
    public void Copies_repeat_a_card_and_spacing_moves_the_cells()
    {
        Project project = _workspace.Project("Cartas_Standar.pnps", cards: 2);
        project.Deck.Cards[0].Copies = 3;
        project.Deck.RecountCopies();
        project.Settings.Grid.Spacing.X = PnpTool.Core.Primitives.Unit.FromPoint(9);
        project.Settings.Grid.Spacing.Y = PnpTool.Core.Primitives.Unit.FromPoint(6);

        string file = PdfGenerator.Generate(project, _workspace.WorkFolder).Files[0];

        List<PlacedImage> images = PdfInspection.ImagesOn(file, 1);
        Assert.Equal(4, images.Count);
        double left = (PageWidth - (3 * CardWidth) - 18) / 2;
        double top = (PageHeight - (3 * CardHeight) - 12) / 2;
        Assert.Equal(left, images[0].X, 1);
        Assert.Equal(left + CardWidth + 9, images[1].X, 1);
        Assert.Equal(PageHeight - top - CardHeight, images[0].Y, 1);
        Assert.Equal(PageHeight - top - (2 * CardHeight) - 6, images[3].Y, 1);

        // Three copies of the first card, then the second card on the next row.
        string red = PdfInspection.ColorAt(file, 1, left + 90, PageHeight - top - 126);
        string third = PdfInspection.ColorAt(file, 1, left + (2 * (CardWidth + 9)) + 90, PageHeight - top - 126);
        string fourth = PdfInspection.ColorAt(file, 1, left + 90, PageHeight - top - CardHeight - 6 - 126);
        Assert.True(PdfInspection.Close(Workspace.Colors[0], red), red);
        Assert.True(PdfInspection.Close(Workspace.Colors[0], third), third);
        Assert.True(PdfInspection.Close(Workspace.Colors[1], fourth), fourth);
    }

    [Fact]
    public void Smaller_face_is_centred_in_its_cell()
    {
        Project project = _workspace.Project("Cartas_Standar.pnps", cards: 1);
        project.Settings.Front.Size.X = PnpTool.Core.Primitives.Unit.FromPoint(160);
        project.Settings.Front.Size.Y = PnpTool.Core.Primitives.Unit.FromPoint(232);

        string file = PdfGenerator.Generate(project, _workspace.WorkFolder).Files[0];

        PlacedImage image = Assert.Single(PdfInspection.ImagesOn(file, 1));
        (double x, double y) = Cell(0, 0);
        Assert.Equal(x + 10, image.X, 1);
        Assert.Equal(y + 10, image.Y, 1);
        Assert.Equal(160, image.Width, 1);
        Assert.Equal(232, image.Height, 1);
    }

    [Fact]
    public void Different_backs_alternate_with_fronts_and_are_mirrored_for_turning_the_sheet()
    {
        Project project = _workspace.Project("Cartas_Standar.pnps", cards: 10, withBacks: true);
        project.Settings.PrintLayout.DoubleSided = true;
        project.Settings.PrintLayout.BackImage = BackImageMode.DifferentBack;
        project.Settings.DoublePanel = true;

        string file = PdfGenerator.Generate(project, _workspace.WorkFolder).Files[0];

        // Sheet of fronts, sheet of backs, sheet of fronts, sheet of backs.
        Assert.Equal(4, PdfInspection.PageCount(file));
        Assert.Equal(9, PdfInspection.ImagesOn(file, 1).Count);
        Assert.Equal(9, PdfInspection.ImagesOn(file, 2).Count);

        // The back of the card at the left of a row is at the right of the same row.
        AssertColor(file, 1, 0, 0, Workspace.Colors[0]);
        AssertColor(file, 2, 2, 0, Workspace.BackColor(0));
        AssertColor(file, 2, 0, 0, Workspace.BackColor(2));
        AssertColor(file, 2, 1, 1, Workspace.BackColor(4));
        AssertColor(file, 2, 2, 2, Workspace.BackColor(6));

        // The tenth card: top left on its sheet of fronts, top right on its sheet of backs.
        AssertColor(file, 3, 0, 0, Workspace.Colors[9]);
        AssertColor(file, 4, 2, 0, Workspace.BackColor(9));
        AssertColor(file, 4, 0, 0, "#FFFFFF");
    }

    [Fact]
    public void Back_offset_moves_only_the_sheets_of_backs()
    {
        Project project = _workspace.Project("Cartas_Standar.pnps", cards: 1, withBacks: true);
        project.Settings.PrintLayout.DoubleSided = true;
        project.Settings.PrintLayout.BackImage = BackImageMode.DifferentBack;
        project.Settings.BackOffset.X = PnpTool.Core.Primitives.Unit.FromPoint(5);
        project.Settings.BackOffset.Y = PnpTool.Core.Primitives.Unit.FromPoint(7);

        string file = PdfGenerator.Generate(project, _workspace.WorkFolder).Files[0];

        AssertBox(Assert.Single(PdfInspection.ImagesOn(file, 1)), 0, 0);
        PlacedImage back = Assert.Single(PdfInspection.ImagesOn(file, 2));
        (double x, double y) = Cell(2, 0);
        Assert.Equal(x + 5, back.X, 1);
        Assert.Equal(y - 7, back.Y, 1);   // down the page is a smaller PDF coordinate
    }

    [Fact]
    public void Shared_back_follows_every_sheet_of_fronts()
    {
        Project project = _workspace.Project("Cartas_Standar.pnps", cards: 10);
        project.Settings.PrintLayout.DoubleSided = true;
        project.Settings.PrintLayout.BackImage = BackImageMode.SameBack;
        project.Settings.SharedBack = _workspace.Face("shared", "#102030", FaceType.Back);

        string file = PdfGenerator.Generate(project, _workspace.WorkFolder).Files[0];

        Assert.Equal(4, PdfInspection.PageCount(file));
        AssertColor(file, 1, 1, 1, Workspace.Colors[4]);

        // Behind a full sheet: the back in all nine cells.
        Assert.Equal(9, PdfInspection.ImagesOn(file, 2).Count);
        AssertColor(file, 2, 0, 0, "#102030");
        AssertColor(file, 2, 2, 2, "#102030");

        // Behind the last sheet, which has one card at the top left: one back, at the top right.
        AssertColor(file, 3, 0, 0, Workspace.Colors[9]);
        AssertBox(Assert.Single(PdfInspection.ImagesOn(file, 4)), 2, 0);
        AssertColor(file, 4, 2, 0, "#102030");
        AssertColor(file, 4, 0, 0, "#FFFFFF");
    }

    [Fact]
    public void Single_back_image_covers_the_whole_sheet()
    {
        Project project = _workspace.Project("Cartas_Standar.pnps", cards: 2);
        project.Settings.PrintLayout.DoubleSided = true;
        project.Settings.PrintLayout.BackImage = BackImageMode.OnlyOne;
        project.Settings.SharedBack = _workspace.Face("shared", "#102030", FaceType.Back);

        string file = PdfGenerator.Generate(project, _workspace.WorkFolder).Files[0];

        Assert.Equal(2, PdfInspection.PageCount(file));
        PlacedImage back = Assert.Single(PdfInspection.ImagesOn(file, 2));
        Assert.True(back.Height > 800 || back.Width > 590, back.ToString());
        Assert.True(PdfInspection.Close("#102030", PdfInspection.ColorAt(file, 2, PageWidth / 2, PageHeight / 2)));
    }

    [Fact]
    public void Gutterfold_puts_backs_under_their_fronts_on_the_same_sheet()
    {
        Project project = _workspace.Project("Cartas_Gutterfold.pnps", cards: 3, withBacks: true);
        PrintLayout print = project.Settings.PrintLayout;
        Assert.False(print.DoubleSided);
        Assert.NotEqual(BackImageMode.NoBack, print.BackImage);

        string file = PdfGenerator.Generate(project, _workspace.WorkFolder).Files[0];

        Assert.Equal(1, PdfInspection.PageCount(file));
        List<PlacedImage> images = PdfInspection.ImagesOn(file, 1);
        Assert.Equal(6, images.Count);

        // Fronts on one row, backs on the next, each back in the column of its front.
        PlacedImage front = images[0];
        PlacedImage back = images[3];   // drawn after the three fronts; empty cells draw nothing
        Assert.Equal(front.X, back.X, 1);
        Assert.True(back.Y < front.Y);
        string frontColor = PdfInspection.ColorAt(file, 1, front.X + (front.Width / 2), front.Y + (front.Height / 2));
        string backColor = PdfInspection.ColorAt(file, 1, back.X + (back.Width / 2), back.Y + (back.Height / 2));
        Assert.True(PdfInspection.Close(Workspace.Colors[0], frontColor), frontColor);
        Assert.True(PdfInspection.Close(Workspace.BackColor(0), backColor), backColor);
    }

    [Fact]
    public void Separate_output_writes_one_file_per_sheet()
    {
        Project project = _workspace.Project("Cartas_Standar.pnps", cards: 10);
        project.Settings.Other.PdfOutput = PdfOutputMode.Separate;

        PdfOutput output = PdfGenerator.Generate(project, _workspace.WorkFolder);

        Assert.False(output.SingleFile);
        Assert.Equal([_workspace.File("out", "deck_0.pdf"), _workspace.File("out", "deck_1.pdf")], output.Files);
        Assert.All(output.Files, file => Assert.Equal(1, PdfInspection.PageCount(file)));
        Assert.Single(PdfInspection.ImagesOn(output.Files[1], 1));
    }

    [Fact]
    public void Maximum_size_output_splits_into_numbered_files()
    {
        Project project = _workspace.Project("Cartas_Standar.pnps", cards: 20);
        project.Settings.Other.PdfOutput = PdfOutputMode.MaximumSize;
        project.Settings.Other.PdfMaximumSize = 0;   // every sheet is over the limit: one file each

        PdfOutput output = PdfGenerator.Generate(project, _workspace.WorkFolder);

        Assert.False(output.SingleFile);
        Assert.Equal(
            [_workspace.File("out", "deck.pdf"), _workspace.File("out", "deck_1.pdf"), _workspace.File("out", "deck_2.pdf")],
            output.Files);
        Assert.All(output.Files, file => Assert.Equal(1, PdfInspection.PageCount(file)));
        Assert.Equal(9, PdfInspection.ImagesOn(output.Files[0], 1).Count);
        Assert.Equal(2, PdfInspection.ImagesOn(output.Files[2], 1).Count);

        project.Settings.Other.PdfMaximumSize = 500;
        output = PdfGenerator.Generate(project, _workspace.WorkFolder);
        Assert.True(output.SingleFile);
        Assert.Equal(3, PdfInspection.PageCount(Assert.Single(output.Files)));
    }

    [Fact]
    public void Preview_writes_only_the_first_sheet()
    {
        Project project = _workspace.Project("Cartas_Standar.pnps", cards: 20);

        string file = PdfGenerator.Generate(project, _workspace.WorkFolder, preview: true).Files[0];

        Assert.Equal(1, PdfInspection.PageCount(file));
        Assert.Equal(9, PdfInspection.ImagesOn(file, 1).Count);
    }

    [Fact]
    public void Progress_runs_from_start_to_the_end_marker()
    {
        Project project = _workspace.Project("Cartas_Standar.pnps", cards: 4);
        var reports = new List<(int Percent, string Message)>();

        PdfGenerator.Generate(project, _workspace.WorkFolder, (percent, message) => reports.Add((percent, message)));

        Assert.Equal(0, reports[0].Percent);
        Assert.Equal((100, "End"), reports[^1]);
        Assert.Contains(reports, report => report.Percent is > 0 and < 100);
        Assert.DoesNotContain(reports, report => report.Message.StartsWith("Need Translation", StringComparison.Ordinal));
    }

    [Fact]
    public void Document_carries_the_program_properties()
    {
        Project project = _workspace.Project("Cartas_Standar.pnps", cards: 10);

        string file = PdfGenerator.Generate(project, _workspace.WorkFolder).Files[0];

        using var document = new PdfDocument(new PdfReader(file));
        PdfDocumentInfo info = document.GetDocumentInfo();
        Assert.Equal("deck", info.GetTitle());
        Assert.Equal(file, info.GetMoreInfo(PdfMetadataKeys.PdfId));
        Assert.Equal("210", info.GetMoreInfo(PdfMetadataKeys.SheetWidth));
        Assert.Equal("297", info.GetMoreInfo(PdfMetadataKeys.SheetHeight));
        Assert.Equal("63.5", info.GetMoreInfo(PdfMetadataKeys.CardWidth));
        Assert.Equal("False", info.GetMoreInfo(PdfMetadataKeys.DoubleSided));
        Assert.Equal("2", info.GetMoreInfo(PdfMetadataKeys.SheetCount));
        Assert.Equal("18", info.GetMoreInfo(PdfMetadataKeys.CardCount));
        Assert.Equal(PdfLayoutWriter.ContentHash(2, 18), info.GetMoreInfo(PdfMetadataKeys.Hash));
        Assert.StartsWith("RGB", info.GetMoreInfo("ICC"));
        Assert.Equal(PdfVersion.PDF_1_3, document.GetPdfVersion());
    }

    [Fact]
    public void Pdf_x_document_declares_its_version_and_output_intent()
    {
        Project project = _workspace.Project("Cartas_Standar.pnps", cards: 2);
        project.Settings.Group.PDFX = true;
        project.Settings.Other.ColorProfilePath = PnpTool.Core.Infrastructure.AppSettings.RgbIccPath;

        string file = PdfGenerator.Generate(project, _workspace.WorkFolder).Files[0];

        using var document = new PdfDocument(new PdfReader(file));
        PdfDictionary catalog = document.GetCatalog().GetPdfObject();
        Assert.Equal("PDF/X-3:2002", catalog.GetAsString(new PdfName("GTS_PDFXVersion")).GetValue());
        PdfArray intents = catalog.GetAsArray(PdfName.OutputIntents);
        Assert.NotNull(intents);
        Assert.Equal("GTS_PDFX", intents.GetAsDictionary(0).GetAsName(PdfName.S).GetValue());
        Assert.Equal("PDF/X-3:2002", document.GetDocumentInfo().GetMoreInfo("GTS_PDFXVersion"));
        Assert.Contains("PDF/X-3:2002", System.Text.Encoding.UTF8.GetString(document.GetXmpMetadataBytes()));
    }

    [Fact]
    public void Missing_image_becomes_a_white_cell_and_generation_goes_on()
    {
        Project project = _workspace.Project("Cartas_Standar.pnps", cards: 3);
        File.Delete(project.Deck.Cards[1].Front.Path);

        string file = PdfGenerator.Generate(project, _workspace.WorkFolder).Files[0];

        Assert.Equal(3, PdfInspection.ImagesOn(file, 1).Count);
        AssertColor(file, 1, 0, 0, Workspace.Colors[0]);
        AssertColor(file, 1, 1, 0, "#FFFFFF");
        AssertColor(file, 1, 2, 0, Workspace.Colors[2]);
    }

    [Fact]
    public void Rotated_card_is_drawn_turned()
    {
        // A card whose left half is red and right half blue, turned half round: blue on the left.
        Project project = _workspace.Project("Cartas_Standar.pnps", cards: 1);
        string path = project.Deck.Cards[0].Front.Path;
        using (var image = new ImageMagick.MagickImage(ImageMagick.MagickColors.Red, 180, 252))
        {
            using (var right = new ImageMagick.MagickImage(ImageMagick.MagickColors.Blue, 90, 252))
            {
                image.Composite(right, 90, 0);
            }

            image.Write(path);
        }

        project.Deck.Cards[0].Front.Rotation.Angle = 180;

        string file = PdfGenerator.Generate(project, _workspace.WorkFolder).Files[0];

        (double x, double y) = Cell(0, 0);
        Assert.True(PdfInspection.Close("#0000FF", PdfInspection.ColorAt(file, 1, x + 40, y + 126)));
        Assert.True(PdfInspection.Close("#FF0000", PdfInspection.ColorAt(file, 1, x + 140, y + 126)));
    }
}
