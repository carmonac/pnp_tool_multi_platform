using ImageMagick;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Pdf.Tabletop;

namespace PnpTool.Pdf.Tests;

/// <summary>The templates of "seven pixels" dice.</summary>
[Collection(GlobalState.Name)]
public sealed class SevenPixelDiceTests : IDisposable
{
    private readonly Workspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    private Card Card(int number, int copies = 1)
    {
        string path = _workspace.File("out", "face" + number + ".png");
        if (!File.Exists(path))
        {
            using var image = new MagickImage(new MagickColor(Workspace.Colors[number % Workspace.Colors.Length]), 60, 90);
            image.Write(path);
        }

        return new Card { Front = new CardFace { Path = path, ImageKind = ImageKind.Normal }, Copies = copies };
    }

    private Card[] Cards(int count) => [.. Enumerable.Range(0, count).Select(i => Card(i))];

    /// <summary>The colour of the template a little inside the given point, as "#RRGGBB".</summary>
    private static string At(string file, PointI where, int inside = 20)
    {
        using var image = new MagickImage(file);
        IMagickColor<ushort> color = image.GetPixels().GetPixel(where.X + inside, where.Y + inside).ToColor()!;
        return "#" + (color.R >> 8).ToString("X2") + (color.G >> 8).ToString("X2") + (color.B >> 8).ToString("X2");
    }

    public static TheoryData<SevenPixelDie, int> FacesOfEveryDie => new()
    {
        { SevenPixelDie.D2, 2 }, { SevenPixelDie.D2Flat, 2 }, { SevenPixelDie.D3, 3 }, { SevenPixelDie.D4, 4 },
        { SevenPixelDie.D6, 6 }, { SevenPixelDie.D8, 8 }, { SevenPixelDie.D10, 10 }, { SevenPixelDie.D12, 12 },
        { SevenPixelDie.D16, 16 }, { SevenPixelDie.D20, 20 }, { SevenPixelDie.D24, 24 }, { SevenPixelDie.D30, 30 },
    };

    [Theory]
    [MemberData(nameof(FacesOfEveryDie))]
    public void Every_die_shows_each_of_its_faces_inside_the_template(SevenPixelDie die, int faces)
    {
        int[] map = SevenPixelDice.Faces(die);
        DieLayout layout = SevenPixelDice.LayoutOf(die);
        PointI[] places = SevenPixelDice.Places(die);

        Assert.Equal(faces, SevenPixelDice.FacesPerDie(die));
        Assert.Equal(Enumerable.Range(0, faces), map.Where(face => face >= 0).Distinct().Order());
        Assert.Equal(map.Length, places.Length);
        Assert.True(map.Length <= layout.Rows * layout.Columns);

        // No two images at the same place, and every image on the template. The images of
        // the D3 are 346 pixels wide in columns of 341, as in the Windows program, so the
        // last ones run 4 pixels past the edge.
        var used = places.Where((_, i) => map[i] >= 0).Select(place => (place.X, place.Y)).ToList();
        Assert.Equal(used.Count, used.Distinct().Count());
        int over = die == SevenPixelDie.D3 ? 4 : 0;
        Assert.All(used, place =>
        {
            Assert.InRange(place.X, 0, SevenPixelDice.Side - layout.ImageSize + over);
            Assert.InRange(place.Y, 0, SevenPixelDice.Side - layout.ImageSize + over);
        });
    }

    [Fact]
    public void Styles_of_the_group_stand_for_the_dice_in_order()
    {
        Assert.Equal(SevenPixelDie.D2, SevenPixelDice.FromStyleIndex(0));
        Assert.Equal(SevenPixelDie.D6, SevenPixelDice.FromStyleIndex(4));
        Assert.Equal(SevenPixelDie.D30, SevenPixelDice.FromStyleIndex(11));
        Assert.Equal(SevenPixelDie.D2, SevenPixelDice.FromStyleIndex(40));
        Assert.Equal(SevenPixelDie.D2, SevenPixelDice.FromStyleIndex(-1));
    }

    [Fact]
    public void Cube_is_laid_out_as_a_cross()
    {
        var dice = new SevenPixelDice(Cards(6), SevenPixelDie.D6, "255_16_32_48");
        Assert.Equal(6, dice.ImageCount);
        Assert.Equal(1, dice.DiceCount);
        var progress = new List<int>();

        string file = dice.Write(_workspace.File("out", "cube"), progress.Add).Single();

        Assert.Equal(_workspace.File("out", "cube_0.png"), file);
        Assert.Equal([1], progress);
        using (var template = new MagickImage(file))
        {
            Assert.Equal(1024u, template.Width);
            Assert.Equal(1024u, template.Height);
        }

        // Rows of three: 6 . . / 4 5 . / 1 2 3, each 341 pixels.
        string[] colors = Workspace.Colors;
        Assert.Equal(colors[5], At(file, new PointI(0, 0)));
        Assert.Equal("#102030", At(file, new PointI(341, 0)));
        Assert.Equal("#102030", At(file, new PointI(682, 0)));
        Assert.Equal(colors[3], At(file, new PointI(0, 341)));
        Assert.Equal(colors[4], At(file, new PointI(341, 341)));
        Assert.Equal("#102030", At(file, new PointI(682, 341)));
        Assert.Equal(colors[0], At(file, new PointI(0, 682)));
        Assert.Equal(colors[1], At(file, new PointI(341, 682)));
        Assert.Equal(colors[2], At(file, new PointI(682, 682)));

        // Images are stretched to the square of a face.
        Assert.Equal(colors[0], At(file, new PointI(0, 682), inside: 330));
    }

    [Fact]
    public void Images_past_one_die_start_another_and_missing_faces_keep_the_fill_colour()
    {
        // Eight images for cubes: one whole and one with two faces. The Windows program failed here.
        var dice = new SevenPixelDice(Cards(8), SevenPixelDie.D6, "255_16_32_48");
        Assert.Equal(2, dice.DiceCount);

        List<string> files = dice.Write(_workspace.File("out", "two"));

        Assert.Equal([_workspace.File("out", "two_0.png"), _workspace.File("out", "two_1.png")], files);
        string[] colors = Workspace.Colors;
        Assert.Equal(colors[6], At(files[1], new PointI(0, 682)));
        Assert.Equal(colors[7], At(files[1], new PointI(341, 682)));
        Assert.Equal("#102030", At(files[1], new PointI(682, 682)));
        Assert.Equal("#102030", At(files[1], new PointI(0, 0)));
        Assert.Equal("#102030", At(files[1], new PointI(0, 341)));
    }

    [Fact]
    public void Last_face_of_the_second_die_is_not_lost()
    {
        // Sixteen images for two D8: the Windows program left the sixteenth out.
        var dice = new SevenPixelDice(Cards(16), SevenPixelDie.D8, AppSettings.ColorWhite);
        PointI[] places = SevenPixelDice.Places(SevenPixelDie.D8);

        List<string> files = dice.Write(_workspace.File("out", "d8"));

        Assert.Equal(2, files.Count);
        Assert.Equal(Workspace.Colors[15], At(files[1], places[7]));
        Assert.Equal(Workspace.Colors[8], At(files[1], places[0]));
        Assert.Equal("#FFFFFF", At(files[1], places[8]));
    }

    [Fact]
    public void Second_d24_starts_on_its_own_template()
    {
        // The Windows program put the first face of the second die on the template of the first.
        Card[] cards = Cards(25);
        var dice = new SevenPixelDice(cards, SevenPixelDie.D24, "255_16_32_48");
        PointI[] places = SevenPixelDice.Places(SevenPixelDie.D24);

        List<string> files = dice.Write(_workspace.File("out", "d24"));

        Assert.Equal(2, files.Count);
        Assert.Equal(Workspace.Colors[23 % Workspace.Colors.Length], At(files[0], places[23]));
        Assert.Equal(Workspace.Colors[24 % Workspace.Colors.Length], At(files[1], places[0]));
        Assert.Equal("#102030", At(files[1], places[1]));

        // Every other row has three faces and is shifted.
        Assert.Equal(places[0].X + 228, places[1].X);
        Assert.Equal(places[4].X, places[0].X + 118);
        Assert.Equal(places[4].X + 228, places[5].X);
        Assert.Equal(places[0].X, places[7].X);
    }

    [Fact]
    public void Copies_are_faces_and_a_coin_shows_each_side_twice()
    {
        var dice = new SevenPixelDice([Card(0, copies: 1), Card(1, copies: 1)], SevenPixelDie.D2, AppSettings.ColorWhite);
        PointI[] places = SevenPixelDice.Places(SevenPixelDie.D2);

        string file = dice.Write(_workspace.File("out", "coin")).Single();

        Assert.Equal(Workspace.Colors[0], At(file, places[4]));
        Assert.Equal(Workspace.Colors[0], At(file, places[7]));
        Assert.Equal(Workspace.Colors[1], At(file, places[5]));
        Assert.Equal(Workspace.Colors[1], At(file, places[8]));
        Assert.Equal("#FFFFFF", At(file, places[0]));

        // Three copies of one image fill a three-sided die.
        var triple = new SevenPixelDice([Card(2, copies: 3)], SevenPixelDie.D3, AppSettings.ColorWhite);
        Assert.Equal(3, triple.ImageCount);
        Assert.Equal(1, triple.DiceCount);
    }

    [Fact]
    public void Blank_card_is_a_patch_of_its_colour_and_an_image_that_is_gone_is_left_out()
    {
        Card blank = new() { Front = new CardFace { Path = "255_200_0_0", ImageKind = ImageKind.Blank }, Copies = 1 };
        Card gone = new() { Front = new CardFace { Path = _workspace.File("out", "gone.png"), ImageKind = ImageKind.Normal }, Copies = 1 };
        var dice = new SevenPixelDice([blank, gone], SevenPixelDie.D2Flat, "255_16_32_48");
        PointI[] places = SevenPixelDice.Places(SevenPixelDie.D2Flat);

        string file = dice.Write(_workspace.File("out", "flat")).Single();

        Assert.Equal("#C80000", At(file, places[0]));
        Assert.Equal("#102030", At(file, places[1], inside: 300));
    }

    [Fact]
    public void Nothing_to_write_writes_nothing_and_stopping_stops()
    {
        Assert.Empty(new SevenPixelDice([], SevenPixelDie.D6, AppSettings.ColorWhite).Write(_workspace.File("out", "none")));

        using var stop = new CancellationTokenSource();
        stop.Cancel();
        var dice = new SevenPixelDice(Cards(2), SevenPixelDie.D2, AppSettings.ColorWhite);
        Assert.Throws<OperationCanceledException>(() => dice.Write(_workspace.File("out", "stopped"), null, stop.Token));
        Assert.False(File.Exists(_workspace.File("out", "stopped_0.png")));
    }
}
