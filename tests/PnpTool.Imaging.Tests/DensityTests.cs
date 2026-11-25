using ImageMagick;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.Imaging.Tests;

[Collection(GlobalState.Name)]
public sealed class DensityTests : IDisposable
{
    private readonly Scratch _scratch = new();

    public void Dispose() => _scratch.Dispose();

    [Fact]
    public void Missing_or_zero_resolution_becomes_96()
    {
        Assert.Equal(96u, ImageDensity.Normalize((Density?)null).X);
        Assert.Equal(96u, ImageDensity.Normalize(new Density(0, 0, DensityUnit.Undefined)).Y);
        PointU mixed = ImageDensity.Normalize(new Density(150, 0, DensityUnit.PixelsPerInch));
        Assert.Equal(150u, mixed.X);
        Assert.Equal(96u, mixed.Y);
        Assert.Equal(300u, ImageDensity.Normalize(new Density(299.6, 300.4, DensityUnit.PixelsPerInch)).X);
    }

    [Fact]
    public void Resolution_per_centimetre_is_converted_to_per_inch()
    {
        var perCentimetre = new Density(118.11, 118.11, DensityUnit.PixelsPerCentimeter);

        Assert.Equal(300u, ImageDensity.Normalize(perCentimetre).X);
        Assert.Equal(2, ImageDensity.PrintedSize(perCentimetre, 600, 900).X.Inch, 6);
    }

    [Fact]
    public void Printed_size_is_pixels_divided_by_resolution()
    {
        UnitPoint size = ImageDensity.PrintedSize(new Density(300, 150, DensityUnit.PixelsPerInch), 750, 1050);
        Assert.Equal(2.5, size.X.Inch, 9);
        Assert.Equal(7, size.Y.Inch, 9);
        Assert.Equal(63.5, size.X.Millimeter, 9);

        UnitPoint assumed = ImageDensity.PrintedSize(null, 96, 192);
        Assert.Equal(1, assumed.X.Inch, 9);
        Assert.Equal(2, assumed.Y.Inch, 9);
    }

    [Fact]
    public void A_png_and_a_jpeg_of_the_same_pixels_and_resolution_print_at_the_same_size()
    {
        PointD png = ImageDensity.Of(_scratch.Image("card.png", 600, 900, dpi: 300));
        PointD jpeg = ImageDensity.Of(_scratch.Image("card.jpg", 600, 900, dpi: 300));

        Assert.Equal(300, jpeg.X, 6);
        Assert.Equal(300, png.X, 1);
    }

    [Fact]
    public void File_without_resolution_reports_zero()
    {
        PointD dpi = ImageDensity.Of(_scratch.Image("card.gif", 60, 90, dpi: null));
        Assert.Equal(0, dpi.X);
        Assert.Equal(0, dpi.Y);
    }

    [Fact]
    public void Changing_the_resolution_writes_a_copy_with_the_same_pixels()
    {
        string source = _scratch.File("grid.png");
        using (MagickImage grid = Pixels.Grid(6, 4, dpi: 72))
        {
            grid.Write(source);
        }

        (AppError error, string copy) = ImageDensity.ChangeDpi(source, 300);

        Assert.False(error.HasError, error.Message);
        Assert.StartsWith(AppSettings.TempFolder, copy);
        Assert.NotEqual(source, copy);
        Assert.Equal(72, ImageDensity.Of(source).X, 0);
        Assert.Equal(300, ImageDensity.Of(copy).X, 0);
        using var changed = new MagickImage(copy);
        Assert.Equal(6u, changed.Width);
        Assert.Equal(4u, changed.Height);
        Assert.Equal(Pixels.GridColor(5, 3, 6), Pixels.At(changed, 5, 3));
        Assert.Equal(Pixels.GridColor(2, 1, 6), Pixels.At(changed, 2, 1));
    }

    [Fact]
    public void Changing_the_resolution_of_an_unreadable_file_is_an_error()
    {
        string broken = _scratch.File("broken.png");
        File.WriteAllText(broken, "nothing");

        (AppError error, _) = ImageDensity.ChangeDpi(broken, 300);

        Assert.True(error.HasError);
        Assert.Equal("error while upscaling dpi", error.Message);
    }

    [Theory]
    [InlineData(72, false, 200, 200)] // not forced: the resolution asked for
    [InlineData(72, true, 200, 300)]  // forced and below it: the program's target (300)
    [InlineData(600, true, 200, 600)] // forced and above it: the image keeps its own
    public void Raising_the_resolution_of_a_file_picks_the_target(int imageDpi, bool force, int asked, int expected)
    {
        string source = _scratch.Image("card.jpg", 100, 100, dpi: imageDpi);

        (AppError error, string copy) = ImageDensity.RaiseDpi(source, force, asked);

        Assert.False(error.HasError, error.Message);
        Assert.Equal(expected, ImageDensity.Of(copy).X, 0);
    }

    [Fact]
    public void Resampling_keeps_the_printed_size_with_more_pixels()
    {
        using var image = new MagickImage(MagickColors.SteelBlue, 100, 50) { Density = new Density(100, DensityUnit.PixelsPerInch) };

        AppError error = ImageDensity.RaiseDpi(image, 300, null);

        Assert.False(error.HasError);
        Assert.Equal(300u, image.Width);
        Assert.Equal(150u, image.Height);
        Assert.Equal(300, image.Density.X);
        Assert.Equal(DensityUnit.PixelsPerInch, image.Density.Units);
    }

    [Fact]
    public void Resampling_an_image_already_at_the_target_changes_nothing()
    {
        using var image = new MagickImage(MagickColors.SteelBlue, 100, 50) { Density = new Density(300, DensityUnit.PixelsPerInch) };

        ImageDensity.RaiseDpi(image, 300, null);

        Assert.Equal(100u, image.Width);
    }

    [Fact]
    public void Resampling_an_image_without_resolution_uses_its_printed_size_or_96()
    {
        // 200 pixels meant to print 2 inches wide is 100 dpi, so 300 dpi needs 600 pixels.
        using var sized = new MagickImage(MagickColors.SteelBlue, 200, 100);
        var twoByOne = new UnitPoint { X = Unit.FromInch(2), Y = Unit.FromInch(1) };
        ImageDensity.RaiseDpi(sized, 300, twoByOne);
        Assert.Equal(600u, sized.Width);
        Assert.Equal(300u, sized.Height);

        // With no size given 96 dpi is assumed: 96 pixels become 300.
        using var unsized = new MagickImage(MagickColors.SteelBlue, 96, 96);
        ImageDensity.RaiseDpi(unsized, 300, null);
        Assert.Equal(300u, unsized.Width);
    }

    [Fact]
    public void Resampling_converts_resolution_per_centimetre()
    {
        // 118 per centimetre rounds to 300 per inch, the target, so nothing is resampled.
        using var image = new MagickImage(MagickColors.SteelBlue, 100, 50) { Density = new Density(118.11, DensityUnit.PixelsPerCentimeter) };

        ImageDensity.RaiseDpi(image, 300, null);

        Assert.Equal(100u, image.Width);
        Assert.Equal(300, image.Density.X);
        Assert.Equal(DensityUnit.PixelsPerInch, image.Density.Units);
    }

    [Theory]
    [InlineData(300, "")]
    [InlineData(100, "")]
    [InlineData(1000, "")]
    [InlineData(72, "_1")]
    [InlineData(1200, "_2")]
    public void Resolution_is_checked_against_the_limits_in_the_settings(int dpi, string code)
    {
        AppError error = ImageDensity.Validate(_scratch.Image("card.jpg", 50, 50, dpi: dpi));

        Assert.Equal(code.Length > 0, error.HasError);
        Assert.Equal(code, error.Message);
    }

    [Fact]
    public void Png_resolution_is_validated_in_dots_per_inch()
    {
        Assert.False(ImageDensity.Validate(_scratch.Image("card.png", 50, 50, dpi: 300)).HasError);
    }
}
