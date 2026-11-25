using PnpTool.Core.Text;

namespace PnpTool.Core.Tests.Text;

public class StringLogicalComparerTests
{
    [Theory]
    [InlineData("card2.png", "card10.png", -1)]
    [InlineData("card10.png", "card2.png", 1)]
    [InlineData("card2.png", "card2.png", 0)]
    [InlineData("Card2.png", "card2.png", 0)]     // letters compare without case
    [InlineData("a", "b", -1)]
    [InlineData("b", "a", 1)]
    [InlineData("1", "a", -1)]                    // digits before letters
    [InlineData("a", "1", 1)]
    [InlineData("_a", "a", -1)]                   // symbols before letters and digits
    [InlineData("a", "_a", 1)]
    [InlineData("007", "7", -1)]                  // same number: more leading zeros first
    [InlineData("7", "007", 1)]
    [InlineData("100", "99", 1)]
    public void Orders_like_a_file_manager(string a, string b, int expectedSign)
    {
        Assert.Equal(expectedSign, Math.Sign(StringLogicalComparer.Compare(a, b)));
    }

    [Fact]
    public void Null_and_empty_handling_matches_the_original()
    {
        Assert.Equal(0, StringLogicalComparer.Compare(null, null));
        Assert.Equal(-1, StringLogicalComparer.Compare(null, "a"));
        Assert.Equal(1, StringLogicalComparer.Compare("a", null));
        Assert.Equal(0, StringLogicalComparer.Compare("", ""));
        Assert.Equal(-1, StringLogicalComparer.Compare("", "a"));
        Assert.Equal(-1, StringLogicalComparer.Compare("a", "")); // quirk kept: not symmetric
    }

    [Fact]
    public void Prefix_compares_as_minus_one_in_both_directions()
    {
        // Quirk kept from the original.
        Assert.Equal(-1, StringLogicalComparer.Compare("card", "card1"));
        Assert.Equal(-1, StringLogicalComparer.Compare("card1", "card"));
    }

    [Fact]
    public void Sorts_a_typical_image_folder()
    {
        string[] files = ["img10.png", "img2.png", "img1.png", "back.png", "img20.png", "_cover.png", "3.png"];
        Array.Sort(files, new NumericComparer());

        Assert.Equal(["_cover.png", "3.png", "back.png", "img1.png", "img2.png", "img10.png", "img20.png"], files);
    }
}
