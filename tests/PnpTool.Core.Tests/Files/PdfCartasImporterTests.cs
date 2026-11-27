using PnpTool.Core.Files;
using PnpTool.Core.Paper;
using PnpTool.Core.Projects;
using Bsk = PnpTool.Core.Files.PdfCartasImporter.Bsk;

namespace PnpTool.Core.Tests.Files;

public sealed class PdfCartasImporterTests
{
    [Fact]
    public void Automatic_mode_is_eight_cards_on_a_landscape_sheet()
    {
        PrintLayout layout = PdfCartasImporter.LayoutOf(new Bsk { Mode = 1, Rows = 5, Columns = 5 });

        Assert.Equal((2, 4, 8), (layout.Rows, layout.Columns, layout.CardsPerSheet));
        Assert.True(layout.Paper.Landscape);
    }

    [Theory]
    [InlineData(0, PaperType.A4)]
    [InlineData(1, PaperType.A3)]
    [InlineData(2, PaperType.Custom)]
    public void Manual_mode_takes_paper_rows_columns_and_orientation(int choice, PaperType paper)
    {
        PrintLayout layout = PdfCartasImporter.LayoutOf(new Bsk
        {
            Mode = 2, PaperChoice = choice, PageWidth = 320, PageHeight = 450, Landscape = 1, Rows = 3, Columns = 5,
        });

        Assert.Equal(paper, layout.Paper.Type);
        Assert.Equal((3, 5, 15), (layout.Rows, layout.Columns, layout.CardsPerSheet));
        Assert.True(layout.Paper.Landscape);
        if (paper == PaperType.Custom)
        {
            Assert.Equal(320, layout.Paper.Size.X.Millimeter, 6);
            Assert.Equal(450, layout.Paper.Size.Y.Millimeter, 6);
        }
    }

    [Fact]
    public void Manual_mode_without_rows_keeps_the_layout_as_it_was()
    {
        var plain = new PrintLayout();
        PrintLayout layout = PdfCartasImporter.LayoutOf(new Bsk { Mode = 2, PaperChoice = 0 });

        Assert.Equal((plain.Rows, plain.Columns), (layout.Rows, layout.Columns));
        Assert.False(layout.Paper.Landscape);
    }

    [Fact]
    public void Card_size_and_spacing_come_in_millimetres()
    {
        Grid apart = PdfCartasImporter.GridOf(new Bsk { Placement = 0, Margin = 3, CardWidth = 63, CardHeight = 88 });
        Assert.Equal((3, 3), (apart.Spacing.X.Millimeter, apart.Spacing.Y.Millimeter));
        Assert.Equal(63, apart.CardSize.X.Millimeter, 6);
        Assert.Equal(88, apart.CardSize.Y.Millimeter, 6);

        Grid together = PdfCartasImporter.GridOf(new Bsk { Placement = 1, Margin = 3 });
        Assert.Equal((0, 0), (together.Spacing.X.Point, together.Spacing.Y.Point));
        Assert.Equal((0, 0), (together.CardSize.X.Point, together.CardSize.Y.Point));
    }

    [Theory]
    [InlineData(0, CutLineType.None, 0)]
    [InlineData(1, CutLineType.Full, 2)]
    [InlineData(2, CutLineType.Cross, 2)]
    public void Cut_lines_are_none_full_or_crosses(int choice, CutLineType type, double points)
    {
        CutLineSettings lines = PdfCartasImporter.CutLinesOf(new Bsk { CutLines = choice, CutLineWidth = 2 });

        Assert.Equal(type, lines.LineType);
        Assert.Equal(points, lines.Thickness.Point, 6);
    }

    [Fact]
    public void A_file_that_is_missing_unreadable_or_of_another_version_is_refused()
    {
        using var folder = new TestFiles.TempFolder();
        Assert.EndsWith("not found", PdfCartasImporter.Import(folder.File("nada.bsk")).Message);

        string text = folder.File("texto.bsk");
        File.WriteAllText(text, "esto no es XML");
        AppError unreadable = PdfCartasImporter.Import(text);
        Assert.True(unreadable.HasError);
        Assert.Equal("Error al leer el fichero", unreadable.Message);

        string old = folder.File("viejo.bsk");
        File.WriteAllText(old, "<Bsk><Version>3</Version></Bsk>");
        AppError version = PdfCartasImporter.Import(old);
        Assert.True(version.HasError);
        Assert.Equal("Solo se soporta la V4 de PdfCartas", version.Message);

        Assert.Empty(Directory.GetFiles(folder.Root, "*.pnp"));
    }
}
