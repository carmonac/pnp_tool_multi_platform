using PnpTool.Core.Cards;
using PnpTool.Core.Projects;

namespace PnpTool.Core.Tests.Cards;

/// <summary>The order of the cards on sheets that are piled up and cut together.</summary>
public class DeckStackingTests
{
    private static Card[] Cards(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new Card { Front = new CardFace { Path = "c" + i, ImageKind = ImageKind.Normal }, Copies = 1 })];

    /// <summary>The fronts of the deck in order, with "-" for the places left empty.</summary>
    private static string[] Order(Deck deck) =>
        [.. deck.Cards.Select(card => card.Front.ImageKind == ImageKind.Empty ? "-" : card.Front.Path)];

    private static Deck Arrange(Card[] cards, LayoutType type, int block, int columns, int rows) =>
        DeckStacking.Arrange(cards, new StackedLayout(block, type), columns, rows);

    [Fact]
    public void Standard_layout_keeps_the_cards_and_their_copies()
    {
        Card[] cards = Cards(3);
        cards[1].Copies = 4;

        Deck deck = Arrange(cards, LayoutType.Standard, 2, 2, 2);

        Assert.Equal(["c0", "c1", "c2"], Order(deck));
        Assert.Equal(4, deck.Cards[1].Copies);
        Assert.Equal(6, deck.TotalCopies);
    }

    [Fact]
    public void Stacked_puts_consecutive_cards_at_the_same_place_of_consecutive_sheets()
    {
        // Two sheets of 2 x 2: piled up and cut, the four small piles are c0 c1, c2 c3, ...
        Deck deck = Arrange(Cards(8), LayoutType.Stacked, 2, 2, 2);

        Assert.Equal(["c0", "c2", "c4", "c6", "c1", "c3", "c5", "c7"], Order(deck));
        Assert.Equal(8, deck.TotalCopies);
    }

    [Fact]
    public void By_rows_fills_a_column_through_the_sheets_of_the_block_before_the_next_column()
    {
        Deck deck = Arrange(Cards(8), LayoutType.ByRows, 2, 2, 2);

        Assert.Equal(["c0", "c4", "c1", "c5", "c2", "c6", "c3", "c7"], Order(deck));
    }

    [Fact]
    public void Block_shrinks_to_the_sheets_needed_when_the_cards_do_not_fill_it()
    {
        Assert.Equal(["c0", "c1", "c2", "-"], Order(Arrange(Cards(3), LayoutType.Stacked, 5, 2, 2)));
        Assert.Equal(["c0", "c2", "c4", "-", "c1", "c3", "-", "-"], Order(Arrange(Cards(5), LayoutType.Stacked, 5, 2, 2)));
        Assert.Equal(["c0", "c2", "c1", "-"], Order(Arrange(Cards(3), LayoutType.ByRows, 5, 2, 2)));
    }

    [Fact]
    public void Cards_past_one_block_start_another()
    {
        Deck stacked = Arrange(Cards(10), LayoutType.Stacked, 2, 2, 2);
        Assert.Equal(16, stacked.Cards.Length);
        Assert.Equal(["c8", "-", "-", "-", "c9", "-", "-", "-"], Order(stacked)[8..]);

        // The original wrote these over cards of the first block.
        Deck byRows = Arrange(Cards(10), LayoutType.ByRows, 2, 2, 2);
        Assert.Equal(["c0", "c4", "c1", "c5", "c2", "c6", "c3", "c7"], Order(byRows)[..8]);
        Assert.Equal(["c8", "-", "c9", "-"], Order(byRows)[8..]);
    }

    [Fact]
    public void Sheets_of_the_last_block_without_cards_are_left_out()
    {
        // The original made a sheet of nothing but empty places here.
        Assert.Equal(["c8", "-", "-", "-"], Order(Arrange(Cards(9), LayoutType.Stacked, 2, 2, 2))[8..]);
        Assert.Equal(["c12", "-", "-", "-", "c13", "-", "-", "-"], Order(Arrange(Cards(14), LayoutType.Stacked, 3, 2, 2))[12..]);
    }

    [Fact]
    public void Copies_become_cards_and_the_cards_given_keep_theirs()
    {
        Card[] cards = Cards(2);
        cards[0].Copies = 3;

        // One card with more copies than places made the original fail.
        Deck deck = Arrange(cards, LayoutType.Stacked, 1, 2, 1);

        Assert.Equal(["c0", "c0", "c0", "c1"], Order(deck));
        Assert.All(deck.Cards, card => Assert.Equal(1, card.Copies));
        Assert.Equal(4, deck.TotalCopies);
        Assert.Equal(3, cards[0].Copies);
    }

    [Fact]
    public void Nothing_to_lay_out_gives_an_empty_deck()
    {
        Assert.Empty(Arrange([], LayoutType.Stacked, 2, 3, 3).Cards);
        Assert.Empty(Arrange([], LayoutType.ByRows, 2, 3, 3).Cards);
    }

    /// <summary>
    /// The places the Windows program gives, from its own loops
    /// (<c>GenerarMazoDesdePaneles_Apilar</c> and <c>AvanzarPosicion…</c>), for decks of whole
    /// blocks, where it works.
    /// </summary>
    private static int[] WindowsStacked(int cards, int block, int columns, int rows)
    {
        int perSheet = columns * rows;
        var places = new int[cards];
        int sheet = 0, column = 0, row = 0, whole = 0;
        for (int i = 0; i < cards; i++)
        {
            places[i] = (whole * perSheet * block) + (sheet * perSheet) + column + (row * columns);
            if (++sheet < block)
            {
                continue;
            }

            sheet = 0;
            if (++column < columns)
            {
                continue;
            }

            column = 0;
            if (++row >= rows)
            {
                row = 0;
                whole++;
            }
        }

        return places;
    }

    /// <summary>The same for <c>GenerarMazoDesdePaneles_ApilarPorEje</c>, for decks of one block at most.</summary>
    private static int[] WindowsByRows(int cards, int block, int columns, int rows)
    {
        int perSheet = columns * rows;
        var places = new int[cards];
        int sheet = 0, column = 0, row = 0;
        for (int i = 0; i < cards; i++)
        {
            places[i] = (row * columns) + (sheet * perSheet) + column;
            if (++row == rows)
            {
                row = 0;
                if (++sheet == block)
                {
                    sheet = 0;
                    column++;
                }
            }
        }

        return places;
    }

    [Theory]
    [InlineData(1, 3, 3)]
    [InlineData(2, 3, 3)]
    [InlineData(3, 2, 4)]
    [InlineData(5, 4, 2)]
    [InlineData(4, 1, 3)]
    public void Places_are_those_of_the_windows_program(int block, int columns, int rows)
    {
        int perBlock = block * columns * rows;
        foreach (int cards in new[] { perBlock, perBlock * 2, (perBlock * 2) + 1, (perBlock * 3) - 1 })
        {
            int[] expected = WindowsStacked(cards, block, columns, rows);
            Assert.Equal(expected, Enumerable.Range(0, cards).Select(i => DeckStacking.Place(i, LayoutType.Stacked, block, columns, rows)).ToArray());
        }

        foreach (int cards in new[] { perBlock, perBlock - 1, perBlock - rows })
        {
            int[] expected = WindowsByRows(cards, block, columns, rows);
            Assert.Equal(expected, Enumerable.Range(0, cards).Select(i => DeckStacking.Place(i, LayoutType.ByRows, block, columns, rows)).ToArray());
        }
    }
}
