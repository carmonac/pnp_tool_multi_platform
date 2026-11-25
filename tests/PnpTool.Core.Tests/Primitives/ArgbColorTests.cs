using PnpTool.Core.Primitives;

namespace PnpTool.Core.Tests.Primitives;

public class ArgbColorTests
{
    [Fact]
    public void Stored_colour_is_read_as_opaque_whatever_its_alpha()
    {
        Assert.Equal(new ArgbColor(255, 10, 20, 30), ArgbColor.Parse("255_10_20_30"));
        Assert.Equal(new ArgbColor(255, 0, 0, 0), ArgbColor.Parse("0_0_0_0"));
        Assert.Equal(ArgbColor.Black, ArgbColor.Parse("0_0_0_0"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("255_255_255")]
    [InlineData("red")]
    public void Text_that_is_not_a_colour_gives_white(string? text)
    {
        Assert.Equal(ArgbColor.White, ArgbColor.Parse(text));
    }

    [Fact]
    public void Colour_is_written_with_its_four_parts()
    {
        Assert.Equal("255_10_20_30", new ArgbColor(10, 20, 30).ToString());
        Assert.Equal("128_1_2_3", new ArgbColor(128, 1, 2, 3).ToString());
    }

    [Theory]
    [InlineData(255, 0, 0, 0, 100, 100, 0)]
    [InlineData(255, 255, 255, 0, 0, 0, 0)]
    [InlineData(0, 0, 0, 0, 0, 0, 100)]
    [InlineData(0, 128, 255, 100, 50, 0, 0)]
    [InlineData(128, 128, 128, 0, 0, 0, 50)]
    public void Plain_conversion_to_cmyk_percentages(byte r, byte g, byte b, byte c, byte m, byte y, byte k)
    {
        Assert.Equal((c, m, y, k), new ArgbColor(r, g, b).ToCmykPercent());
    }
}
