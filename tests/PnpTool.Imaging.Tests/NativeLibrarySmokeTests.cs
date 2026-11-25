using ImageMagick;

namespace PnpTool.Imaging.Tests;

/// <summary>Checks that the Magick.NET native library loads on the current OS and CPU.</summary>
public class NativeLibrarySmokeTests
{
    [Fact]
    public void Magick_creates_and_encodes_an_image()
    {
        using var image = new MagickImage(MagickColors.Red, 10, 20);
        image.Format = MagickFormat.Png;
        byte[] png = image.ToByteArray();

        using var decoded = new MagickImage(png);
        Assert.Equal(10u, decoded.Width);
        Assert.Equal(20u, decoded.Height);
    }
}
