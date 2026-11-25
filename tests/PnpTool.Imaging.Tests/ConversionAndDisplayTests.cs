using ImageMagick;
using PnpTool.Core;

namespace PnpTool.Imaging.Tests;

[Collection(GlobalState.Name)]
public sealed class ConversionAndDisplayTests : IDisposable
{
    private readonly Scratch _scratch = new();

    public void Dispose() => _scratch.Dispose();

    /// <summary>A 4 × 4 PNG, green with a transparent right half.</summary>
    private static MagickImage HalfTransparent()
    {
        var image = new MagickImage(MagickColors.Lime, 4, 4) { Format = MagickFormat.Png };
        image.Alpha(AlphaAction.Set);
        using IPixelCollection<ushort> pixels = image.GetPixels();
        for (int y = 0; y < 4; y++)
        {
            for (int x = 2; x < 4; x++)
            {
                pixels.SetPixel(x, y, [0, 0, 0, 0]);
            }
        }

        return image;
    }

    [Fact]
    public void Png_to_jpeg_fills_transparency_with_the_colour_of_the_first_pixel()
    {
        using MagickImage png = HalfTransparent();

        using MagickImage jpeg = ImageConversions.PngToJpeg(png);

        Assert.Equal(MagickFormat.Jpeg, jpeg.Format);
        Assert.Equal(100u, jpeg.Quality);
        Assert.False(jpeg.HasAlpha);
        Assert.Equal("#00FF00", Pixels.At(jpeg, 3, 3));
        Assert.Equal(MagickFormat.Png, png.Format);
    }

    [Fact]
    public void Webp_to_jpeg_is_a_copy_at_quality_80()
    {
        using var webp = new MagickImage(MagickColors.SteelBlue, 20, 20) { Format = MagickFormat.WebP };

        using MagickImage jpeg = ImageConversions.WebpToJpeg(webp);

        Assert.Equal(MagickFormat.Jpeg, jpeg.Format);
        Assert.Equal(80u, jpeg.Quality);
        Assert.Equal(MagickFormat.WebP, webp.Format);
    }

    [Theory]
    [InlineData(MagickFormat.WebP, false, MagickFormat.Jpeg)]
    [InlineData(MagickFormat.Avif, false, MagickFormat.Jpeg)]
    [InlineData(MagickFormat.Png, true, MagickFormat.Jpeg)]
    [InlineData(MagickFormat.Png, false, MagickFormat.Png)]
    [InlineData(MagickFormat.Jpeg, true, MagickFormat.Jpeg)]
    [InlineData(MagickFormat.Tiff, false, MagickFormat.Tiff)]
    public void Bytes_for_a_pdf_are_in_a_format_pdf_can_hold(MagickFormat source, bool pdfX, MagickFormat expected)
    {
        using var image = new MagickImage(MagickColors.SteelBlue, 20, 30) { Format = source };

        byte[] bytes = ImageConversions.ToPdfImageBytes(image, pdfX);

        var info = new MagickImageInfo(bytes);
        Assert.Equal(expected, info.Format);
        Assert.Equal(20u, info.Width);
        Assert.Equal(30u, info.Height);
        Assert.Equal(source, image.Format);
    }

    [Fact]
    public void Stream_holds_the_image_in_its_format_from_the_start()
    {
        using var png = new MagickImage(MagickColors.SteelBlue, 20, 30) { Format = MagickFormat.Png };
        using var webp = new MagickImage(MagickColors.SteelBlue, 20, 30) { Format = MagickFormat.WebP };

        using MemoryStream pngStream = ImageConversions.ToStream(png);
        using MemoryStream webpStream = ImageConversions.ToStream(webp);

        Assert.Equal(0, pngStream.Position);
        Assert.Equal(MagickFormat.Png, new MagickImageInfo(pngStream).Format);
        Assert.Equal(MagickFormat.Jpeg, new MagickImageInfo(webpStream).Format);
    }

    [Fact]
    public void First_pixel_colour_is_read()
    {
        using MagickImage grid = Pixels.Grid(3, 3);
        Assert.Equal("#FF0000", Pixels.Hex(ImageConversions.FirstPixelColor(grid)));
    }

    // ------------------------------------------------------------ display

    [Fact]
    public void Display_pixels_are_blue_green_red_alpha_from_the_top_left()
    {
        using MagickImage grid = Pixels.Grid(3, 2, dpi: 150);

        DisplayImage display = DisplayImages.From(grid);

        Assert.Equal(3, display.Width);
        Assert.Equal(2, display.Height);
        Assert.Equal(12, display.Stride);
        Assert.Equal(24, display.Pixels.Length);
        Assert.Equal(150, display.DpiX);
        Assert.Equal([0, 0, 255, 255], display.Pixels[..4]);        // red
        Assert.Equal([0, 255, 0, 255], display.Pixels[4..8]);       // lime
        Assert.Equal([255, 0, 0, 255], display.Pixels[8..12]);      // blue
        Assert.Equal([0, 255, 255, 255], display.Pixels[12..16]);   // yellow, start of the second row
    }

    [Fact]
    public void Display_pixels_keep_transparency()
    {
        using MagickImage image = HalfTransparent();

        DisplayImage display = DisplayImages.From(image);

        Assert.Equal(255, display.Pixels[3]);
        Assert.Equal(0, display.Pixels[(3 * 4) + 3]);
    }

    [Fact]
    public void Cmyk_image_is_shown_in_rgb()
    {
        using var image = new MagickImage(MagickColors.Red, 2, 2);
        image.ColorSpace = ColorSpace.CMYK;

        DisplayImage display = DisplayImages.From(image);

        Assert.Equal(16, display.Pixels.Length);
        Assert.True(display.Pixels[2] > 200, "red " + display.Pixels[2]);
        Assert.True(display.Pixels[0] < 60, "blue " + display.Pixels[0]);
        Assert.Equal(ColorSpace.CMYK, image.ColorSpace);
    }

    [Fact]
    public void Display_image_without_resolution_reports_96()
    {
        using var image = new MagickImage(MagickColors.Red, 2, 2);
        Assert.Equal(96, DisplayImages.From(image).DpiX);
    }

    [Theory]
    [InlineData(ImageResolutionType.SmallBitmap, 32)]
    [InlineData(ImageResolutionType.MediumBitmap, 96)]
    [InlineData(ImageResolutionType.LargeBitmap, 256)]
    [InlineData(ImageResolutionType.ExtraLargeBitmap, 1024)]
    [InlineData(ImageResolutionType.Original, 0)]
    public void Thumbnail_sizes_follow_the_panel_setting(ImageResolutionType resolution, int size)
    {
        Assert.Equal(size, DisplayImages.ThumbnailSize(resolution));
    }

    [Fact]
    public void Thumbnail_fits_the_size_and_keeps_proportions()
    {
        string path = _scratch.Image("card.jpg", 600, 900);

        DisplayImage medium = DisplayImages.LoadThumbnail(path, ImageResolutionType.MediumBitmap);
        DisplayImage original = DisplayImages.LoadThumbnail(path, ImageResolutionType.Original);

        Assert.Equal(64, medium.Width);
        Assert.Equal(96, medium.Height);
        Assert.Equal(600, original.Width);
        Assert.Equal(900, original.Height);
    }

    [Fact]
    public void Small_image_is_not_enlarged_for_a_thumbnail()
    {
        DisplayImage display = DisplayImages.Load(_scratch.Image("icon.png", 20, 10), longestSide: 96);

        Assert.Equal(20, display.Width);
        Assert.Equal(10, display.Height);
    }

    [Theory]
    [InlineData("card.png")]
    [InlineData("card.jpg")]
    [InlineData("card.bmp")]
    [InlineData("card.gif")]
    [InlineData("card.tiff")]
    [InlineData("card.webp")]
    [InlineData("card.avif")]
    public void Every_supported_format_can_be_shown(string name)
    {
        DisplayImage display = DisplayImages.Load(_scratch.Image(name, 30, 20, color: MagickColors.Red));

        Assert.Equal(30, display.Width);
        Assert.Equal(20, display.Height);
        Assert.True(display.Pixels[2] > 240, "red " + display.Pixels[2]);
        Assert.True(display.Pixels[0] < 20, "blue " + display.Pixels[0]);
    }

    [Fact]
    public void Photo_is_shown_upright_according_to_its_exif_orientation()
    {
        string path = _scratch.File("photo.jpg");
        using (var image = new MagickImage(MagickColors.SteelBlue, 60, 30))
        {
            image.Orientation = Orientation.RightTop;
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.Orientation, (ushort)Orientation.RightTop);
            image.SetProfile(exif);
            image.Write(path);
        }

        DisplayImage display = DisplayImages.Load(path);

        Assert.Equal(30, display.Width);
        Assert.Equal(60, display.Height);
    }
}
