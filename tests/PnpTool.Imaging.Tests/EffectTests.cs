using ImageMagick;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.Imaging.Tests;

[Collection(GlobalState.Name)]
public sealed class EffectTests : IDisposable
{
    private static readonly ArgbColor Red = new(255, 0, 0);
    private readonly Scratch _scratch = new();

    public void Dispose() => _scratch.Dispose();

    /// <summary>Whether the pixel of a mask is part of the hole.</summary>
    private static bool IsHole(MagickImage mask, int x, int y)
    {
        using IPixelCollection<ushort> pixels = mask.GetPixels();
        return pixels.GetPixel(x, y).ToColor()!.A == 0;
    }

    /// <summary>A project whose cards are 1 × 1 inch, so that at 300 dpi a card image is 301 × 301 pixels.</summary>
    private static ProjectSettings OneInchCards()
    {
        var settings = new ProjectSettings();
        var inch = new UnitPoint { X = Unit.FromInch(1), Y = Unit.FromInch(1) };
        settings.Grid.CardSize = new UnitPoint(inch);
        settings.Front.Size = new UnitPoint(inch);
        settings.Back.Size = new UnitPoint(inch);
        settings.Other.ResizeFilter = FilterType.Lanczos;
        return settings;
    }

    [Theory]
    [InlineData(ImageEffectType.RoundedCorners, true)]
    [InlineData(ImageEffectType.CircularToken, true)]
    [InlineData(ImageEffectType.HexagonToken, true)]
    [InlineData(ImageEffectType.PentagonToken, true)]
    [InlineData(ImageEffectType.None, false)]
    [InlineData(ImageEffectType.Mirror, false)]
    [InlineData(ImageEffectType.Blur, false)]
    [InlineData(ImageEffectType.Autolevel, false)]
    [InlineData(ImageEffectType.Contrast, false)]
    [InlineData(ImageEffectType.CreateOutline, false)]
    public void Only_the_masks_depend_on_the_card_size(ImageEffectType effect, bool hasSize)
    {
        Assert.Equal(hasSize, ImageEffects.HasSize(effect));
    }

    [Fact]
    public void Rounded_corner_mask_covers_the_four_corners_only()
    {
        using MagickImage mask = ImageEffects.CreateMask(ImageEffectType.RoundedCorners, 100, 140, 20, Red);

        Assert.Equal(100u, mask.Width);
        Assert.Equal(140u, mask.Height);
        Assert.Equal("#FF0000", Pixels.At(mask, 0, 0));
        Assert.False(IsHole(mask, 0, 0));
        Assert.False(IsHole(mask, 2, 2));
        Assert.False(IsHole(mask, 97, 2));
        Assert.False(IsHole(mask, 2, 137));
        Assert.False(IsHole(mask, 97, 137));

        Assert.True(IsHole(mask, 50, 70));
        Assert.True(IsHole(mask, 7, 7));    // inside the arc: 17 pixels from the arc's centre at (19, 19)
        Assert.True(IsHole(mask, 30, 0));   // straight part of the top edge
        Assert.True(IsHole(mask, 0, 70));   // straight part of the left edge
        Assert.True(IsHole(mask, 99, 70));
        Assert.True(IsHole(mask, 50, 139));
    }

    [Fact]
    public void Rounded_corners_of_size_zero_cut_nothing()
    {
        using MagickImage mask = ImageEffects.CreateMask(ImageEffectType.RoundedCorners, 20, 20, 0, Red);

        Assert.True(IsHole(mask, 0, 0));
        Assert.True(IsHole(mask, 19, 19));
    }

    [Fact]
    public void Circular_mask_keeps_the_inscribed_ellipse()
    {
        using MagickImage mask = ImageEffects.CreateMask(ImageEffectType.CircularToken, 100, 60, 0, Red);

        Assert.True(IsHole(mask, 50, 30));
        Assert.True(IsHole(mask, 50, 3));
        Assert.True(IsHole(mask, 4, 30));
        Assert.True(IsHole(mask, 95, 30));
        Assert.False(IsHole(mask, 0, 0));
        Assert.False(IsHole(mask, 99, 59));
        Assert.False(IsHole(mask, 10, 5));
    }

    [Fact]
    public void Hexagon_mask_has_corners_left_and_right_and_flat_top_and_bottom()
    {
        // Radius 40 around (50, 50): corners near x = 10 and x = 90, flat sides near y = 15 and y = 85.
        using MagickImage mask = ImageEffects.CreateMask(ImageEffectType.HexagonToken, 100, 100, 40, Red);

        Assert.True(IsHole(mask, 50, 50));
        Assert.True(IsHole(mask, 86, 50));
        Assert.True(IsHole(mask, 14, 50));
        Assert.True(IsHole(mask, 50, 19));
        Assert.True(IsHole(mask, 50, 81));
        Assert.False(IsHole(mask, 95, 50));
        Assert.False(IsHole(mask, 50, 11));
        Assert.False(IsHole(mask, 50, 89));
        Assert.False(IsHole(mask, 86, 30));   // beside the right corner, above the slanted side
    }

    [Fact]
    public void Pentagon_mask_points_up()
    {
        // Radius 40 around (50, 50): tip near (50, 10), flat side at the bottom near y = 82.
        using MagickImage mask = ImageEffects.CreateMask(ImageEffectType.PentagonToken, 100, 100, 40, Red);

        Assert.True(IsHole(mask, 50, 50));
        Assert.True(IsHole(mask, 50, 15));
        Assert.True(IsHole(mask, 50, 78));
        Assert.False(IsHole(mask, 50, 5));
        Assert.False(IsHole(mask, 50, 87));
        Assert.False(IsHole(mask, 30, 15));   // beside the tip
    }

    [Fact]
    public void Polygon_corners_are_whole_pixels_at_the_radius()
    {
        PointD[] hexagon = ImageEffects.PolygonCorners(100, 100, 40, 6);
        PointD[] pentagon = ImageEffects.PolygonCorners(100, 100, 40, 5);

        Assert.Equal(6, hexagon.Length);
        Assert.Equal(5, pentagon.Length);
        Assert.All(hexagon.Concat(pentagon), corner =>
        {
            Assert.Equal(Math.Round(corner.X), corner.X);
            Assert.InRange(Math.Sqrt(Math.Pow(corner.X - 50, 2) + Math.Pow(corner.Y - 50, 2)), 38.5, 41.5);
        });
        Assert.Equal(90, hexagon[0].X);
        Assert.InRange(pentagon[0].Y, 9, 11);
        Assert.InRange(pentagon[0].X, 49, 51);
    }

    [Fact]
    public void Mask_effect_brings_the_image_to_the_card_size_and_covers_what_is_cut()
    {
        using var image = new MagickImage(MagickColors.Blue, 150, 150) { Density = new Density(150, DensityUnit.PixelsPerInch) };

        (AppError error, MagickImage result) = ImageEffects.Apply(
            image, ImageEffectType.CircularToken, 0, Red, OneInchCards(), FaceType.Front);

        Assert.False(error.HasError);
        Assert.Same(image, result);
        Assert.Equal(302u, result.Width);    // 1 inch at 150 dpi is 151 pixels (one added), doubled for 300 dpi
        Assert.Equal(302u, result.Height);
        Assert.Equal("#0000FF", Pixels.At(result, 151, 151));
        Assert.Equal("#FF0000", Pixels.At(result, 2, 2));
        Assert.Equal("#FF0000", Pixels.At(result, 299, 299));
    }

    [Fact]
    public void Rounded_corners_take_the_size_as_the_corner_radius_in_pixels()
    {
        using var image = new MagickImage(MagickColors.Blue, 300, 300) { Density = new Density(300, DensityUnit.PixelsPerInch) };

        (_, MagickImage result) = ImageEffects.Apply(image, ImageEffectType.RoundedCorners, 30, Red, OneInchCards(), FaceType.Front);

        Assert.Equal("#FF0000", Pixels.At(result, 3, 3));
        Assert.Equal("#0000FF", Pixels.At(result, 12, 12));
        Assert.Equal("#0000FF", Pixels.At(result, 150, 0));
    }

    [Fact]
    public void Blur_softens_an_edge()
    {
        using var image = new MagickImage(MagickColors.Black, 40, 10) { Density = new Density(300, DensityUnit.PixelsPerInch) };
        using (IPixelCollection<ushort> pixels = image.GetPixels())
        {
            for (int y = 0; y < 10; y++)
            {
                for (int x = 20; x < 40; x++)
                {
                    pixels.SetPixel(x, y, [ushort.MaxValue, ushort.MaxValue, ushort.MaxValue]);
                }
            }
        }

        (AppError error, MagickImage result) = ImageEffects.Apply(image, ImageEffectType.Blur, 3, Red, OneInchCards(), FaceType.Front);

        Assert.False(error.HasError);
        Assert.Equal(40u, result.Width);
        Assert.Equal("#000000", Pixels.At(result, 0, 5));
        Assert.Equal("#FFFFFF", Pixels.At(result, 39, 5));
        string edge = Pixels.At(result, 19, 5);
        Assert.NotEqual("#000000", edge);
        Assert.NotEqual("#FFFFFF", edge);
    }

    [Fact]
    public void Autolevel_stretches_the_tones_to_the_full_range()
    {
        using var image = new MagickImage(new MagickColor("#404040"), 20, 10) { Density = new Density(300, DensityUnit.PixelsPerInch) };
        using (IPixelCollection<ushort> pixels = image.GetPixels())
        {
            for (int y = 0; y < 10; y++)
            {
                for (int x = 10; x < 20; x++)
                {
                    pixels.SetPixel(x, y, [0xC0C0, 0xC0C0, 0xC0C0]);
                }
            }
        }

        ImageEffects.Apply(image, ImageEffectType.Autolevel, 0, Red, OneInchCards(), FaceType.Front);

        Assert.Equal("#000000", Pixels.At(image, 2, 5));
        Assert.Equal("#FFFFFF", Pixels.At(image, 17, 5));
    }

    [Fact]
    public void Contrast_pushes_tones_apart()
    {
        using var image = new MagickImage(new MagickColor("#606060"), 20, 10) { Density = new Density(300, DensityUnit.PixelsPerInch) };
        using (IPixelCollection<ushort> pixels = image.GetPixels())
        {
            for (int y = 0; y < 10; y++)
            {
                for (int x = 10; x < 20; x++)
                {
                    pixels.SetPixel(x, y, [0xA0A0, 0xA0A0, 0xA0A0]);
                }
            }
        }

        ImageEffects.Apply(image, ImageEffectType.Contrast, 0, Red, OneInchCards(), FaceType.Front);

        int dark = Convert.ToInt32(Pixels.At(image, 2, 5)[1..3], 16);
        int light = Convert.ToInt32(Pixels.At(image, 17, 5)[1..3], 16);
        Assert.True(dark < 0x60, "dark " + dark);
        Assert.True(light > 0xA0, "light " + light);
    }

    [Fact]
    public void Mirror_effect_grows_the_image_to_the_cell()
    {
        ProjectSettings settings = OneInchCards();
        settings.Grid.CardSize = new UnitPoint { X = Unit.FromInch(1.1), Y = Unit.FromInch(1.2) };
        using var image = new MagickImage(MagickColors.Blue, 300, 300) { Density = new Density(300, DensityUnit.PixelsPerInch) };

        (AppError error, MagickImage result) = ImageEffects.Apply(image, ImageEffectType.Mirror, 0, Red, settings, FaceType.Front);
        using (result)
        {
            Assert.False(error.HasError);
            Assert.Equal(330u, result.Width);
            Assert.Equal(360u, result.Height);
        }
    }

    [Fact]
    public void Image_without_resolution_is_first_given_the_card_size_at_the_target_resolution()
    {
        // 150 pixels across a 1 inch card is 150 dpi; resampled to 300 dpi it becomes 300 pixels.
        using var image = new MagickImage(MagickColors.Blue, 150, 150);

        (AppError error, _) = ImageEffects.Apply(image, ImageEffectType.Autolevel, 0, Red, OneInchCards(), FaceType.Front);

        Assert.False(error.HasError);
        Assert.Equal(300u, image.Width);
        Assert.Equal(300, image.Density.X);
    }

    [Fact]
    public void Outline_effect_reports_that_it_is_not_available()
    {
        using var image = new MagickImage(MagickColors.Blue, 30, 30) { Density = new Density(300, DensityUnit.PixelsPerInch) };

        (AppError error, MagickImage result) = ImageEffects.Apply(image, ImageEffectType.CreateOutline, 0, Red, OneInchCards(), FaceType.Front);

        Assert.True(error.HasError);
        Assert.Same(image, result);
    }

    // ------------------------------------------------------------ solid colour and pattern

    [Fact]
    public void Solid_colour_image_has_the_stored_colour()
    {
        using MagickImage image = SolidColorImages.Create("255_10_200_30", null, ProjectColorSpace.RGB, MagickFormat.Png);

        Assert.Equal(100u, image.Width);
        Assert.Equal(100u, image.Height);
        Assert.Equal(MagickFormat.Png, image.Format);
        Assert.Equal(ColorSpace.sRGB, image.ColorSpace);
        Assert.Equal("#0AC81E", Pixels.At(image, 50, 50));
    }

    [Fact]
    public void Solid_colour_image_for_a_cmyk_project_is_cmyk()
    {
        using MagickImage image = SolidColorImages.Create("255_255_0_0", ColorProfiles.USWebCoatedSWOP, ProjectColorSpace.CMYK, MagickFormat.Jpeg);

        Assert.Equal(ColorSpace.CMYK, image.ColorSpace);
        Assert.Equal(MagickFormat.Jpeg, image.Format);
    }

    [Fact]
    public void Solid_colour_image_for_an_rgb_project_carries_the_profile()
    {
        using MagickImage image = SolidColorImages.Create("255_255_0_0", ColorProfiles.SRGB, ProjectColorSpace.RGB, MagickFormat.Jpeg);

        Assert.NotNull(image.GetColorProfile());
        Assert.Equal("#FF0000", Pixels.At(image, 10, 10));
    }

    [Fact]
    public void Unreadable_colour_gives_a_white_image()
    {
        using MagickImage image = SolidColorImages.Create(string.Empty, null, ProjectColorSpace.RGB, MagickFormat.Png);
        Assert.Equal("#FFFFFF", Pixels.At(image, 0, 0));
    }

    [Fact]
    public void Pattern_repeats_the_tile_and_cuts_the_last_column_and_row()
    {
        using MagickImage tile = Pixels.Grid(2, 2, dpi: 150);

        using MagickImage pattern = BackPattern.Tile(tile, columns: 2.5, rows: 1.5);

        Assert.Equal(5u, pattern.Width);
        Assert.Equal(3u, pattern.Height);
        Assert.Equal(150, pattern.Density.X);
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                Assert.Equal(Pixels.GridColor(x % 2, y % 2, 2), Pixels.At(pattern, x, y));
            }
        }
    }

    [Fact]
    public void Pattern_for_a_project_covers_the_sheet()
    {
        // A 60 × 90 pixel back for a 2 × 3 inch card on a 5 × 7.5 inch sheet: 2.5 cards each way.
        ProjectSettings settings = OneInchCards();
        settings.Back.Size = new UnitPoint { X = Unit.FromInch(2), Y = Unit.FromInch(3) };
        settings.PrintLayout.Paper.Size = new UnitPoint { X = Unit.FromInch(5), Y = Unit.FromInch(7.5) };
        settings.SharedBack.Path = _scratch.Image("back.png", 60, 90, dpi: 30);

        string path = BackPattern.Create(settings);

        Assert.Equal(Path.Combine(PnpTool.Core.Infrastructure.AppSettings.TempFolder, "Imagen_Patron_temp.jpg"), path);
        var info = new MagickImageInfo(path);
        Assert.Equal(150u, info.Width);
        Assert.Equal(225u, info.Height);
        Assert.Equal(MagickFormat.Png, info.Format);
    }
}
