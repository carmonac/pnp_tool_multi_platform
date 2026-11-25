using PnpTool.Core.Cards;
using PnpTool.Core.Primitives;

namespace PnpTool.Core.Tests.Cards;

[Collection(GlobalState.Name)]
public class DeckTests
{
    private static Deck DeckOf(params string[] names)
    {
        var deck = new Deck();
        foreach (string name in names)
        {
            deck.Add(name, ImageKind.Normal);
        }

        return deck;
    }

    private static string[] Fronts(Deck deck) => [.. deck.Cards.Select(card => card.Front.Path)];

    [Theory]
    [InlineData("dragon x3.png", 3)]
    [InlineData("dragonX12.jpg", 12)]
    [InlineData("folder/knight_x2.png", 2)]
    [InlineData("dragon.png", 1)]
    [InlineData("card3.png", 1)]     // digits not preceded by x
    [InlineData("x5.png", 5)]
    [InlineData("7.png", 1)]
    [InlineData("box.png", 1)]       // ends in x but no number
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void Copies_come_from_the_file_name(string? fileName, int expected)
    {
        Assert.Equal(expected, Card.CopiesFromFileName(fileName));
    }

    [Fact]
    public void Adding_cards_counts_copies()
    {
        Deck deck = DeckOf("a x2.png", "b.png");
        deck.Add(string.Empty, ImageKind.Empty);

        Assert.Equal(3, deck.Cards.Length);
        Assert.Equal(4, deck.TotalCopies);
        Assert.Equal(FaceType.Front, deck.Cards[0].Front.FaceType);
        Assert.Equal(FaceType.Back, deck.Cards[0].Back.FaceType);
        Assert.Equal(ImageKind.Empty, deck.Cards[0].Back.ImageKind);

        deck.Cards[0].Copies = 5;
        deck.RecountCopies();
        Assert.Equal(7, deck.TotalCopies);
    }

    [Fact]
    public void Added_cards_are_copies_of_the_given_card()
    {
        var card = new Card { Copies = 2, Front = new CardFace("a.png", FaceType.Front, ImageKind.Normal) };
        var deck = new Deck();
        deck.Add(card);
        card.Front.Path = "changed.png";

        Assert.Equal("a.png", deck.Cards[0].Front.Path);
        Assert.Equal(2, deck.TotalCopies);
    }

    [Theory]
    [InlineData(0, 2, new[] { "b", "c", "a", "d" })]
    [InlineData(3, 1, new[] { "a", "d", "b", "c" })]
    [InlineData(2, -1, new[] { "c", "a", "b", "d" })]
    [InlineData(1, 1, new[] { "a", "b", "c", "d" })]
    public void Move_shifts_the_cards_in_between(int from, int to, string[] expected)
    {
        Deck deck = DeckOf("a", "b", "c", "d");
        deck.Move(from, to);

        Assert.Equal(expected, Fronts(deck));
    }

    [Fact]
    public void RemoveAt_updates_counts_and_selection_counters()
    {
        Deck deck = DeckOf("a x2.png", "b x3.png", "c.png");
        deck.Cards[1].Selected = true;
        deck.Cards[1].Front.Selected = true;
        int faces = 1;
        int panels = 1;

        Assert.True(deck.RemoveAt(1, ref faces, ref panels));
        Assert.Equal(["a x2.png", "c.png"], Fronts(deck));
        Assert.Equal(3, deck.TotalCopies);
        Assert.Equal(0, faces);
        Assert.Equal(0, panels);
        Assert.False(deck.RemoveAt(9, ref faces, ref panels));
        Assert.Equal(2, deck.Cards.Length);
    }

    [Fact]
    public void Clear_and_find_selected()
    {
        Deck deck = DeckOf("a", "b", "c");
        deck.Cards[0].Selected = true;
        deck.Cards[2].Selected = true;
        Assert.Equal([0, 2], deck.FindSelectedCards());

        deck.Clear();
        Assert.Empty(deck.Cards);
        Assert.Equal(0, deck.TotalCopies);
        Assert.Empty(deck.FindSelectedCards());
    }

    [Fact]
    public void Grouping_single_panels_pairs_them_as_front_and_back()
    {
        Deck singles = DeckOf("f1 x2.png", "b1.png", "f2.png");
        var blank = new CardFace("255_255_255_255", FaceType.Back, ImageKind.Blank);
        var doubles = new Deck();

        doubles.ConvertToDoublePanel(FaceChangeChoice.Group, singles, "255_255_255_255", hasSharedBack: false, new CardFace(), blank, fromUndo: false);

        Assert.Equal(2, doubles.Cards.Length);
        Assert.Equal("f1 x2.png", doubles.Cards[0].Front.Path);
        Assert.Equal("b1.png", doubles.Cards[0].Back.Path);
        Assert.Equal(FaceType.Back, doubles.Cards[0].Back.FaceType);
        Assert.Equal(2, doubles.Cards[0].Copies);
        Assert.Equal("f2.png", doubles.Cards[1].Front.Path);
        Assert.Equal(ImageKind.Blank, doubles.Cards[1].Back.ImageKind);
    }

    [Fact]
    public void Single_panels_as_backs_move_the_image_to_the_back()
    {
        Deck singles = DeckOf("art.png");
        singles.Cards[0].Front.Size = new UnitPoint(Unit.FromMillimeter(60), Unit.FromMillimeter(90));
        var doubles = new Deck();

        doubles.ConvertToDoublePanel(FaceChangeChoice.Backs, singles, "255_1_2_3", false, new CardFace(), new CardFace(), fromUndo: false);

        Card card = doubles.Cards.Single();
        Assert.Equal("art.png", card.Back.Path);
        Assert.Equal(FaceType.Back, card.Back.FaceType);
        Assert.Equal(60.0, card.Back.Size.X.Millimeter, 6);
        Assert.Equal("255_1_2_3", card.Front.Path);
        Assert.Equal(ImageKind.Blank, card.Front.ImageKind);
        Assert.Equal(0.0, card.Front.Size.X.Point);
    }

    [Fact]
    public void Ungrouping_double_panels_gives_one_single_panel_per_face()
    {
        var doubles = new Deck();
        doubles.Add("f.png", ImageKind.Normal, "b.png", ImageKind.Normal);
        var blank = new CardFace("255_255_255_255", FaceType.Back, ImageKind.Blank);
        var singles = new Deck();

        singles.ConvertToSinglePanel(FaceChangeChoice.Group, doubles, blank, hasSharedBack: false, fromUndo: false);
        Assert.Equal(["f.png", "b.png"], Fronts(singles));

        var frontsOnly = new Deck();
        frontsOnly.ConvertToSinglePanel(FaceChangeChoice.Group, doubles, blank, hasSharedBack: true, fromUndo: false);
        Assert.Equal(["f.png"], Fronts(frontsOnly));
    }

    [Fact]
    public void Swapping_selected_faces_exchanges_their_images_but_not_their_sides()
    {
        var deck = new Deck();
        deck.Add("f1.png", ImageKind.Normal, "b1.png", ImageKind.Normal);
        deck.Add("f2.png", ImageKind.Normal, "b2.png", ImageKind.Normal);
        deck.Cards[0].Front.Selected = true;
        deck.Cards[1].Back.Selected = true;

        deck.SwapSelectedFaces(new CardFace[2], new int[2], doublePanel: true);

        Assert.Equal("b2.png", deck.Cards[0].Front.Path);
        Assert.Equal(FaceType.Front, deck.Cards[0].Front.FaceType);
        Assert.Equal("f1.png", deck.Cards[1].Back.Path);
        Assert.Equal(FaceType.Back, deck.Cards[1].Back.FaceType);
    }

    [Fact]
    public void Rotating_a_face_by_a_quarter_turn_swaps_its_size()
    {
        var face = new CardFace("a.png", FaceType.Front, ImageKind.Normal)
        {
            Size = new UnitPoint { X = Unit.FromMillimeter(60), Y = Unit.FromMillimeter(90) },
        };

        face.ApplyRotation(new RotationProperties(90, flipHorizontal: true, flipVertical: false));
        Assert.Equal(90.0, face.Size.X.Millimeter, 6);
        Assert.Equal(60.0, face.Size.Y.Millimeter, 6);
        Assert.Equal(90, face.Rotation.Angle);
        Assert.True(face.Rotation.FlipHorizontal);

        face.ApplyRotation(new RotationProperties(180, false, false));
        Assert.Equal(90.0, face.Size.X.Millimeter, 6);
        Assert.Equal(270, face.Rotation.Angle);

        var blank = new CardFace("255_255_255_255", FaceType.Front, ImageKind.Blank);
        blank.ApplyRotation(new RotationProperties(90, false, false));
        Assert.Equal(0, blank.Rotation.Angle);
    }
}
