using PnpTool.Core.Projects;

namespace PnpTool.Core.Cards;

/// <summary>
/// Orders the cards for sheets that will be piled up and cut together, so that the cards
/// come out of the cut pile already in order. Original: <c>GenerarMazoDesdePaneles_Apilar</c>
/// and <c>GenerarMazoDesdePaneles_ApilarPorEje</c> in <c>PanelImagenes</c>.
/// </summary>
/// <remarks>
/// The sheets are taken in blocks of <see cref="StackedLayout.BlockSize"/>. A deck is a list
/// of places, sheet after sheet, each sheet row by row; this fills those places in another
/// order and leaves the places without card empty.
/// <list type="bullet">
/// <item><see cref="LayoutType.Stacked"/>: one card on each sheet of the block at the same
/// place, then the next place. Each small pile cut from the block is in order, and so are the
/// piles, row by row.</item>
/// <item><see cref="LayoutType.ByRows"/>: a column of the first sheet from top to bottom, the
/// same column of the next sheet, and so on through the block, then the next column.</item>
/// </list>
/// When the cards do not fill one block, the block is made of only the sheets needed, and
/// the sheets of the last block that get no card are left out.
/// </remarks>
public static class DeckStacking
{
    /// <summary>
    /// The deck to lay out: the cards in their own order for a standard layout, and placed
    /// for the piles otherwise, with every copy as a card of its own.
    /// Original: <c>GenerarMazoDesdePaneles(TiposProyectos, MaquetacionApilada, PuntoXY, …)</c>.
    /// </summary>
    public static Deck Arrange(IEnumerable<Card> cards, StackedLayout layout, int columns, int rows)
    {
        if (layout.LayoutType == LayoutType.Standard || columns < 1 || rows < 1)
        {
            var plain = new Deck();
            foreach (Card card in cards)
            {
                plain.Add(card);
            }

            return plain;
        }

        // The original changed the copies of the cards of the panel to 1 here, for good;
        // these are copies of them.
        var singles = new List<Card>();
        foreach (Card card in cards)
        {
            var single = new Card();
            single.CopyFrom(card);
            single.Copies = 1;
            singles.AddRange(Enumerable.Repeat(single, card.Copies));
        }

        int perSheet = columns * rows;
        int sheets = (singles.Count + perSheet - 1) / perSheet;
        int block = Math.Clamp(layout.BlockSize, 1, Math.Max(sheets, 1));
        int perBlock = block * perSheet;
        int blocks = (singles.Count + perBlock - 1) / perBlock;

        // The original laid out every sheet of the last block, also those left without cards.
        int lastBlock = singles.Count - ((blocks - 1) * perBlock);
        int lastSheets = layout.LayoutType == LayoutType.ByRows ? (lastBlock + rows - 1) / rows : lastBlock;
        int sheetsUsed = ((blocks - 1) * block) + Math.Min(block, lastSheets);
        var deck = new Deck(sheetsUsed * perSheet);
        for (int i = 0; i < singles.Count; i++)
        {
            deck.Insert(singles[i], Place(i, layout.LayoutType, block, columns, rows));
        }

        var empty = new Card { Front = new CardFace { ImageKind = ImageKind.Empty }, Copies = 1 };
        for (int i = 0; i < deck.Cards.Length; i++)
        {
            if (deck.Cards[i] == null)
            {
                deck.Insert(empty, i);
            }
        }

        deck.RecountCopies();
        return deck;
    }

    /// <summary>
    /// The place in the deck of the card that is number <paramref name="index"/> (from 0)
    /// in the order of the cards.
    /// </summary>
    public static int Place(int index, LayoutType type, int block, int columns, int rows)
    {
        int perSheet = columns * rows;
        int perBlock = block * perSheet;
        int inBlock = index % perBlock;
        int sheet;
        int row;
        int column;
        if (type == LayoutType.ByRows)
        {
            row = inBlock % rows;
            sheet = inBlock / rows % block;
            column = inBlock / (rows * block);
        }
        else
        {
            sheet = inBlock % block;
            column = inBlock / block % columns;
            row = inBlock / (block * columns);
        }

        return (index / perBlock * perBlock) + (sheet * perSheet) + (row * columns) + column;
    }
}
