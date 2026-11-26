using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Paper;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.Core.Tests.Projects;

[Collection(GlobalState.Name)]
public sealed class LayoutCalculatorTests : IDisposable
{
    private readonly GlobalStateScope _scope = new();

    public LayoutCalculatorTests()
    {
        AppPaths.Initialize(AppPaths.FindAssetsFolder(AppContext.BaseDirectory), Path.GetTempPath());
        AppLanguage.Change(new SupportedLanguage(LanguageId.Spanish));
        AppSettings.PrinterMargin = new UnitPoint { X = Unit.FromMillimeter(1.0), Y = Unit.FromMillimeter(1.0) };
    }

    public void Dispose() => _scope.Dispose();

    private static UnitPoint Millimeters(double x, double y) => new() { X = Unit.FromMillimeter(x), Y = Unit.FromMillimeter(y) };

    private static (Grid Grid, PrintLayout Layout) PokerOnA4()
    {
        var grid = new Grid { CardSize = Millimeters(63.5, 88.0), Spacing = Millimeters(0.0, 0.0) };
        var layout = new PrintLayout();
        layout.Paper.Type = PaperType.A4;
        layout.Paper.Size = Millimeters(210.0, 297.0);
        layout.Paper.Landscape = false;
        layout.DoubleSided = false;
        layout.BackImage = BackImageMode.NoBack;
        return (grid, layout);
    }

    [Fact]
    public void Poker_cards_fit_three_by_three_on_an_upright_A4_and_four_by_two_on_its_side()
    {
        (Grid grid, PrintLayout layout) = PokerOnA4();

        Assert.Equal((3, 3), LayoutCalculator.Fit(grid, layout, SettingsGroup.Cards));
        layout.Paper.Landscape = true;
        Assert.Equal((4, 2), LayoutCalculator.Fit(grid, layout, SettingsGroup.Cards));
    }

    [Fact]
    public void Space_between_cards_and_the_margin_of_the_printer_take_cards_away()
    {
        (Grid grid, PrintLayout layout) = PokerOnA4();
        grid.CardSize = Millimeters(70.0, 99.0);    // exactly three by three, without room for the margin

        Assert.Equal((2, 2), LayoutCalculator.Fit(grid, layout, SettingsGroup.Cards));

        grid.CardSize = Millimeters(63.5, 88.0);
        grid.Spacing = Millimeters(10.0, 20.0);     // 3 × 63.5 + 2 × 10 + 2 = 212.5 > 210
        Assert.Equal((2, 2), LayoutCalculator.Fit(grid, layout, SettingsGroup.Cards));
    }

    [Fact]
    public void A_gutterfold_gets_an_even_number_of_rows()
    {
        (Grid grid, PrintLayout layout) = PokerOnA4();
        layout.BackImage = BackImageMode.DifferentBack;

        Assert.True(LayoutCalculator.IsGutterfold(layout));
        Assert.Equal((3, 2), LayoutCalculator.Fit(grid, layout, SettingsGroup.Cards));

        layout.DoubleSided = true;
        Assert.False(LayoutCalculator.IsGutterfold(layout));
        Assert.Equal((3, 3), LayoutCalculator.Fit(grid, layout, SettingsGroup.Cards));
    }

    [Fact]
    public void Cards_without_size_and_dice_are_special_cases()
    {
        (Grid grid, PrintLayout layout) = PokerOnA4();
        grid.CardSize = Millimeters(0.0, 88.0);
        Assert.Equal((1000, 1000), LayoutCalculator.Fit(grid, layout, SettingsGroup.Cards));

        grid.CardSize = Millimeters(16.0, 16.0);    // 13 × 18 faces: four dice across, three down
        Assert.Equal((13, 18), LayoutCalculator.Fit(grid, layout, SettingsGroup.Cards));
        Assert.Equal((4, 3), LayoutCalculator.Fit(grid, layout, SettingsGroup.Dice));
    }

    [Fact]
    public void Total_size_adds_cards_spaces_and_margins()
    {
        (Grid grid, PrintLayout layout) = PokerOnA4();
        grid.Spacing = Millimeters(2.0, 3.0);
        layout.Columns = 3;
        layout.Rows = 2;

        UnitPoint total = LayoutCalculator.TotalSize(grid, layout, SettingsGroup.Cards);
        Assert.Equal((3 * 63.5) + (2 * 2.0) + 2.0, total.X.Millimeter, 3);
        Assert.Equal((2 * 88.0) + 3.0 + 2.0, total.Y.Millimeter, 3);

        UnitPoint dice = LayoutCalculator.TotalSize(grid, layout, SettingsGroup.Dice);
        Assert.Equal((9 * 63.5) + (2 * 2.0) + 2.0, dice.X.Millimeter, 3);
        Assert.Equal((8 * 88.0) + 3.0 + 2.0, dice.Y.Millimeter, 3);
    }

    [Fact]
    public void Paper_on_its_side_swaps_width_and_height()
    {
        (_, PrintLayout layout) = PokerOnA4();
        Assert.Equal(210.0, LayoutCalculator.Oriented(layout.Paper).X.Millimeter, 3);
        layout.Paper.Landscape = true;
        Assert.Equal(297.0, LayoutCalculator.Oriented(layout.Paper).X.Millimeter, 3);
        Assert.Equal(210.0, LayoutCalculator.Oriented(layout.Paper).Y.Millimeter, 3);
    }

    // ---- Ways of giving backs ----

    private static BackImageMode[] Modes(bool doubleSided, bool gutterfold, ProjectType type, SettingsGroup group) =>
        [.. BackImageOption.For(doubleSided, gutterfold, type, group).Select(option => option.Value)];

    [Fact]
    public void Backs_offered_depend_on_sides_and_kind_of_project()
    {
        Assert.Equal([BackImageMode.NoBack], Modes(false, false, ProjectType.Card, SettingsGroup.Cards));
        Assert.Equal([BackImageMode.SameBack, BackImageMode.DifferentBack], Modes(false, true, ProjectType.Card, SettingsGroup.Cards));
        Assert.Equal(
            [BackImageMode.SameBack, BackImageMode.DifferentBack, BackImageMode.OnlyOne, BackImageMode.Pattern],
            Modes(true, false, ProjectType.Card, SettingsGroup.Cards));
        Assert.Equal([BackImageMode.SameBack, BackImageMode.DifferentBack], Modes(true, false, ProjectType.Papermini, SettingsGroup.Cards));
        Assert.Equal([BackImageMode.NoBack], Modes(false, true, ProjectType.Card, SettingsGroup.AdvancedStyles));
        Assert.Equal(4, Modes(true, false, ProjectType.Card, SettingsGroup.AdvancedStyles).Length);
        Assert.All(BackImageOption.For(true, false, ProjectType.Card, SettingsGroup.Cards), option => Assert.NotEqual(string.Empty, option.Text));
    }

    // ---- Sizes of the images of a deck ----

    private static Card CardOf(double frontX, double frontY, double backX = 0.0, double backY = 0.0)
    {
        var card = new Card();
        card.Front.Size = Millimeters(frontX, frontY);
        card.Back.Size = Millimeters(backX, backY);
        return card;
    }

    [Fact]
    public void Statistics_measure_the_fronts_of_the_deck()
    {
        var deck = new Deck { Cards = [CardOf(60.0, 90.0), CardOf(40.0, 100.0), CardOf(50.0, 80.0), CardOf(70.0, 70.0)] };

        var statistics = new DeckStatistics(deck, BackImageMode.NoBack);

        Assert.Equal(70.0, statistics.Maximum.X.Millimeter, 3);
        Assert.Equal(100.0, statistics.Maximum.Y.Millimeter, 3);
        Assert.Equal(40.0, statistics.Minimum.X.Millimeter, 3);
        Assert.Equal(70.0, statistics.Minimum.Y.Millimeter, 3);
        Assert.Equal(55.0, statistics.Mean.X.Millimeter, 3);
        Assert.Equal(85.0, statistics.Mean.Y.Millimeter, 3);
        Assert.Equal(60.0, statistics.Median.X.Millimeter, 3);    // the upper of the two in the middle
        Assert.Equal(90.0, statistics.Median.Y.Millimeter, 3);
        Assert.Equal(8, statistics.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void Statistics_count_the_backs_only_when_every_card_has_its_own()
    {
        var deck = new Deck { Cards = [CardOf(60.0, 90.0, 100.0, 120.0), CardOf(40.0, 100.0, 20.0, 30.0)] };

        Assert.Equal(60.0, new DeckStatistics(deck, BackImageMode.SameBack).Maximum.X.Millimeter, 3);
        var both = new DeckStatistics(deck, BackImageMode.DifferentBack);
        Assert.Equal(100.0, both.Maximum.X.Millimeter, 3);
        Assert.Equal(30.0, both.Minimum.Y.Millimeter, 3);

        var empty = new DeckStatistics(new Deck(), BackImageMode.NoBack);
        Assert.Equal(0.0, empty.Maximum.X.Millimeter, 3);
    }
}
