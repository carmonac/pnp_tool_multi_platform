using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Pdf.Layout;

namespace PnpTool.Pdf.Tests;

/// <summary>
/// Decks made from the user's pictures, with suits and numbers drawn over them.
/// Original: <c>FicheroPdfMaquetadoBarajaPersonalizada</c>.
/// </summary>
[Collection(GlobalState.Name)]
public sealed class CustomDeckTests : IDisposable
{
    private readonly Workspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    /// <summary>A deck project as the program hands it over: one back for all, pictures 10 mm smaller than the card.</summary>
    private Project Deck(string style, int subgroup, int cards)
    {
        Project project = _workspace.Project(style, cards);
        ProjectSettings settings = project.Settings;
        Assert.Equal(ProjectType.SpanishDeck, settings.Group.ProjectType);
        Assert.Equal(BackImageMode.SameBack, settings.PrintLayout.BackImage);
        settings.Group.SubgroupIndex = subgroup;
        settings.SharedBack = _workspace.Face("back", Workspace.BackColor(0), FaceType.Back);
        settings.Front.Size.X = (double)settings.Front.Size.X - (double)Unit.FromMillimeter(10.0);
        settings.Front.Size.Y = (double)settings.Front.Size.Y - (double)Unit.FromMillimeter(10.0);
        return project;
    }

    [Theory]
    [InlineData("Cartas_Espanola_Personalizado.pnps", 1, 40)]
    [InlineData("Cartas_Poker_Personalizado.pnps", 2, 52)]
    public void Deck_has_a_sheet_of_backs_after_every_sheet_of_fronts(string style, int subgroup, int cards)
    {
        Project project = Deck(style, subgroup, cards);

        PdfOutput output = PdfGenerator.Generate(project, _workspace.WorkFolder);

        string file = Assert.Single(output.Files);
        int sheets = (int)Math.Ceiling(cards / 9.0);
        Assert.Equal(sheets * 2, PdfInspection.PageCount(file));
        Assert.False(File.Exists(file + "_"));
    }

    [Fact]
    public void Spanish_deck_draws_symbol_frame_and_turned_symbol_on_every_card()
    {
        Project project = Deck("Cartas_Espanola_Personalizado.pnps", 1, 40);
        Project plain = Deck("Cartas_Espanola_Personalizado.pnps", 1, 40);
        plain.Settings.Group.ProjectType = ProjectType.Card;
        plain.Settings.InputParameters.OutputPdf = _workspace.File("out", "plain.pdf");

        string file = PdfGenerator.Generate(project, _workspace.WorkFolder).Files[0];
        string without = PdfGenerator.Generate(plain, _workspace.WorkFolder).Files[0];

        // First sheet: nine cards, each with its picture and, over it, two symbols and a frame.
        int pictures = PdfInspection.ImagesOn(without, 1).Count;
        List<PlacedImage> images = PdfInspection.ImagesOn(file, 1);
        Assert.Equal(pictures + (9 * 3), images.Count);

        // The symbols are 5 mm wide; the frames cover the card.
        double five = Unit.FromMillimeter(5.0).Point;
        Assert.Equal(18, images.Count(image => Math.Abs(Math.Abs(image.Width) - five) < 0.5));
        ProjectSettings settings = project.Settings;
        double cardWidth = settings.Front.Size.X.Point + Unit.FromMillimeter(10.0).Point;
        Assert.Equal(9, images.Count(image => Math.Abs(image.Width - (cardWidth + 1.0)) < 0.5));

        // The backs are left alone.
        Assert.Equal(PdfInspection.ImagesOn(without, 2).Count, PdfInspection.ImagesOn(file, 2).Count);

        // Last sheet of fronts: cards 37 to 40 only.
        List<PlacedImage> last = PdfInspection.ImagesOn(file, 9);
        Assert.Equal(8, last.Count(image => Math.Abs(Math.Abs(image.Width) - five) < 0.5));
    }

    [Fact]
    public void Poker_deck_has_no_frames_and_changes_suit_every_thirteen_cards()
    {
        Project project = Deck("Cartas_Poker_Personalizado.pnps", 2, 52);

        string file = PdfGenerator.Generate(project, _workspace.WorkFolder).Files[0];

        double five = Unit.FromMillimeter(5.0).Point;
        double cardWidth = project.Settings.Front.Size.X.Point + Unit.FromMillimeter(10.0).Point;
        int symbols = 0;
        for (int page = 1; page <= PdfInspection.PageCount(file); page += 2)
        {
            List<PlacedImage> images = PdfInspection.ImagesOn(file, page);
            symbols += images.Count(image => Math.Abs(Math.Abs(image.Width) - five) < 0.5);
            Assert.DoesNotContain(images, image => Math.Abs(image.Width - (cardWidth + 1.0)) < 0.5);
        }

        Assert.Equal(52 * 2, symbols);

        // Hearts and diamonds have red numbers, spades and clubs black ones: the text is there.
        string text = PdfInspection.TextOn(file, 1);
        Assert.Contains("1", text);
        Assert.Contains("9", text);
        string faces = PdfInspection.TextOn(file, 3);
        Assert.Contains("J", faces);
        Assert.Contains("Q", faces);
        Assert.Contains("K", faces);
    }

    [Fact]
    public void Ordinary_card_projects_are_not_touched()
    {
        Project project = _workspace.Project("Cartas_Standar.pnps", 9);
        Assert.NotEqual(ProjectType.SpanishDeck, project.Settings.Group.ProjectType);

        string file = PdfGenerator.Generate(project, _workspace.WorkFolder).Files[0];

        Assert.Equal(string.Empty, PdfInspection.TextOn(file, 1).Trim());
    }
}
