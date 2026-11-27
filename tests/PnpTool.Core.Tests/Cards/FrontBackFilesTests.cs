using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Projects;

namespace PnpTool.Core.Tests.Cards;

/// <summary>How chosen files become fronts and backs, and what the user is warned of.</summary>
[Collection(GlobalState.Name)]
public class FrontBackFilesTests
{
    [Fact]
    public void Pairs_take_the_files_two_by_two()
    {
        (string[] fronts, string[] backs) = FrontBackFiles.Pair(["c10.png", "c2.png", "c1.png", "c3.png"], [], AddImagesResult.OneFrontOneBack);

        Assert.Equal(["c1.png", "c3.png"], fronts);
        Assert.Equal(["c2.png", "c10.png"], backs);
    }

    [Fact]
    public void Odd_file_out_is_a_front_without_back()
    {
        (string[] fronts, string[] backs) = FrontBackFiles.Pair(["a.png", "b.png", "c.png"], [], AddImagesResult.OneFrontOneBack);

        Assert.Equal(["a.png", "c.png"], fronts);
        Assert.Equal(["b.png", string.Empty], backs);
    }

    [Fact]
    public void One_back_is_repeated_for_many_fronts_and_the_reverse()
    {
        (string[] fronts, string[] backs) = FrontBackFiles.Pair(["f2.png", "f10.png", "f1.png"], ["back.png"], AddImagesResult.ManyFrontsOneBack);
        Assert.Equal(["f1.png", "f2.png", "f10.png"], fronts);
        Assert.Equal(["back.png", "back.png", "back.png"], backs);

        (fronts, backs) = FrontBackFiles.Pair(["front.png"], ["b2.png", "b1.png"], AddImagesResult.OneFrontManyBacks);
        Assert.Equal(["front.png", "front.png"], fronts);
        Assert.Equal(["b1.png", "b2.png"], backs);
    }

    [Fact]
    public void Many_and_many_gives_every_front_with_every_back()
    {
        (string[] fronts, string[] backs) = FrontBackFiles.Pair(["f1.png", "f2.png"], ["b1.png", "b2.png", "b3.png"], AddImagesResult.ManyFrontsManyBacks);
        Assert.Equal(["f1.png", "f2.png", "f1.png", "f2.png", "f1.png", "f2.png"], fronts);
        Assert.Equal(["b1.png", "b1.png", "b2.png", "b2.png", "b3.png", "b3.png"], backs);

        // More fronts than backs made the original fail.
        (fronts, backs) = FrontBackFiles.Pair(["f1.png", "f2.png", "f3.png"], ["b1.png", "b2.png"], AddImagesResult.ManyFrontsManyBacks);
        Assert.Equal(["f1.png", "f2.png", "f3.png", "f1.png", "f2.png", "f3.png"], fronts);
        Assert.Equal(["b1.png", "b1.png", "b1.png", "b2.png", "b2.png", "b2.png"], backs);
    }

    [Fact]
    public void Nothing_chosen_gives_no_cards()
    {
        Assert.Empty(FrontBackFiles.Pair([], [], AddImagesResult.OneFrontOneBack).Fronts);
        Assert.Empty(FrontBackFiles.Pair(["f.png"], [], AddImagesResult.ManyFrontsOneBack).Fronts);
        Assert.Empty(FrontBackFiles.Pair([], ["b.png"], AddImagesResult.OneFrontManyBacks).Fronts);
    }

    [Fact]
    public void Several_files_can_be_chosen_only_where_the_way_takes_many()
    {
        Assert.Equal((true, true), FrontBackFiles.MultiSelect(AddImagesResult.OneFrontOneBack));
        Assert.Equal((true, false), FrontBackFiles.MultiSelect(AddImagesResult.ManyFrontsOneBack));
        Assert.Equal((false, true), FrontBackFiles.MultiSelect(AddImagesResult.OneFrontManyBacks));
        Assert.Equal((true, true), FrontBackFiles.MultiSelect(AddImagesResult.ManyFrontsManyBacks));
        Assert.False(FrontBackFiles.AsksForBacks(AddImagesResult.OneFrontOneBack));
        Assert.True(FrontBackFiles.AsksForBacks(AddImagesResult.ManyFrontsOneBack));
    }

    [Fact]
    public void Every_file_asking_for_too_many_copies_is_named()
    {
        string[] found = FrontBackFiles.WithTooManyCopies(["dragon x80.png", "elf x3.png", "orc x51.png"], ["back x99.png", string.Empty]);

        Assert.Equal(["dragon x80.png", "orc x51.png", "back x99.png"], found);
        Assert.Empty(FrontBackFiles.WithTooManyCopies(["elf x50.png"], []));
    }

    private static Card CardOf(bool frontCmyk, bool? backCmyk = null)
    {
        var card = new Card { Front = new CardFace { ImageKind = ImageKind.Normal, IsCmyk = frontCmyk } };
        card.Back = backCmyk == null
            ? new CardFace { ImageKind = ImageKind.Empty }
            : new CardFace { ImageKind = ImageKind.Normal, IsCmyk = backCmyk.Value };
        return card;
    }

    [Fact]
    public void Images_in_another_colour_space_are_noticed_on_any_card_and_any_face()
    {
        AppSettings.Reset();
        try
        {
            AppSettings.ShowCmykWarning = true;

            Assert.Null(Card.ColorSpaceWarning([CardOf(false), CardOf(false, false)], ProjectColorSpace.RGB));
            Assert.NotNull(Card.ColorSpaceWarning([CardOf(false), CardOf(false), CardOf(true)], ProjectColorSpace.RGB));
            Assert.NotNull(Card.ColorSpaceWarning([CardOf(false, true)], ProjectColorSpace.RGB));
            Assert.Null(Card.ColorSpaceWarning([CardOf(true, true)], ProjectColorSpace.CMYK));
            Assert.NotNull(Card.ColorSpaceWarning([CardOf(true, false)], ProjectColorSpace.CMYK));

            // Blank and empty faces, and images that could not be read, have no colour space.
            var unread = new Card { Front = new CardFace { ImageKind = ImageKind.Normal, IsCorrupt = true }, Back = new CardFace { ImageKind = ImageKind.Blank } };
            Assert.Null(Card.ColorSpaceWarning([unread], ProjectColorSpace.CMYK));
        }
        finally
        {
            AppSettings.Reset();
        }
    }

    [Fact]
    public void Warning_turned_off_is_still_given_once_and_only_spent_when_given()
    {
        AppSettings.Reset();
        try
        {
            Assert.False(AppSettings.ShowCmykWarning);

            // Cards that are fine do not use up the one warning.
            Assert.Null(Card.ColorSpaceWarning([CardOf(false)], ProjectColorSpace.RGB));
            Assert.NotNull(Card.ColorSpaceWarning([CardOf(true)], ProjectColorSpace.RGB));
            Assert.Null(Card.ColorSpaceWarning([CardOf(true)], ProjectColorSpace.RGB));
        }
        finally
        {
            AppSettings.Reset();
        }
    }
}
