using ImageMagick;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.Imaging.Tests;

[Collection(GlobalState.Name)]
public sealed class TransformTests : IDisposable
{
    private readonly Scratch _scratch = new();

    public void Dispose() => _scratch.Dispose();

    /// <summary>The colours of a row of the image, left to right.</summary>
    private static string[] Row(MagickImage image, int y) =>
        [.. Enumerable.Range(0, (int)image.Width).Select(x => Pixels.At(image, x, y))];

    private static string[] Column(MagickImage image, int x) =>
        [.. Enumerable.Range(0, (int)image.Height).Select(y => Pixels.At(image, x, y))];

    private static string G(int x, int y, int width) => Pixels.GridColor(x, y, width);

    // ------------------------------------------------------------ rotation

    [Fact]
    public void Quarter_turn_is_clockwise()
    {
        // 3 × 2:  a b c      after 90°:  d a
        //         d e f                  e b
        //                                f c
        using MagickImage image = Pixels.Grid(3, 2);

        ImageTransforms.Rotate(image, new RotationProperties(90, flipHorizontal: false, flipVertical: false));

        Assert.Equal(2u, image.Width);
        Assert.Equal(3u, image.Height);
        Assert.Equal([G(0, 1, 3), G(0, 0, 3)], Row(image, 0));
        Assert.Equal([G(1, 1, 3), G(1, 0, 3)], Row(image, 1));
        Assert.Equal([G(2, 1, 3), G(2, 0, 3)], Row(image, 2));
    }

    [Fact]
    public void Half_turn_reverses_rows_and_columns()
    {
        using MagickImage image = Pixels.Grid(3, 2);

        ImageTransforms.Rotate(image, new RotationProperties(180, flipHorizontal: false, flipVertical: false));

        Assert.Equal([G(2, 1, 3), G(1, 1, 3), G(0, 1, 3)], Row(image, 0));
        Assert.Equal([G(2, 0, 3), G(1, 0, 3), G(0, 0, 3)], Row(image, 1));
    }

    [Fact]
    public void Horizontal_flip_mirrors_left_and_right()
    {
        using MagickImage image = Pixels.Grid(3, 2);

        ImageTransforms.Rotate(image, new RotationProperties(0, flipHorizontal: true, flipVertical: false));

        Assert.Equal([G(2, 0, 3), G(1, 0, 3), G(0, 0, 3)], Row(image, 0));
        Assert.Equal([G(2, 1, 3), G(1, 1, 3), G(0, 1, 3)], Row(image, 1));
    }

    [Fact]
    public void Vertical_flip_mirrors_top_and_bottom()
    {
        using MagickImage image = Pixels.Grid(3, 2);

        ImageTransforms.Rotate(image, new RotationProperties(0, flipHorizontal: false, flipVertical: true));

        Assert.Equal([G(0, 1, 3), G(1, 1, 3), G(2, 1, 3)], Row(image, 0));
        Assert.Equal([G(0, 0, 3), G(1, 0, 3), G(2, 0, 3)], Row(image, 1));
    }

    [Fact]
    public void Rotation_is_applied_before_the_flips()
    {
        // 90° gives rows (d a) (e b) (f c); the horizontal flip then gives (a d) (b e) (c f).
        using MagickImage image = Pixels.Grid(3, 2);

        ImageTransforms.Rotate(image, new RotationProperties(90, flipHorizontal: true, flipVertical: false));

        Assert.Equal([G(0, 0, 3), G(0, 1, 3)], Row(image, 0));
        Assert.Equal([G(2, 0, 3), G(2, 1, 3)], Row(image, 2));
    }

    [Fact]
    public void No_rotation_leaves_the_image_alone()
    {
        using MagickImage image = Pixels.Grid(3, 2);

        MagickImage result = ImageTransforms.Rotate(image, new RotationProperties());

        Assert.Same(image, result);
        Assert.Equal([G(0, 0, 3), G(1, 0, 3), G(2, 0, 3)], Row(image, 0));
    }

    // ------------------------------------------------------------ resizing

    [Theory]
    [InlineData(50, 100u, 75u)]
    [InlineData(200, 400u, 300u)]
    [InlineData(33, 66u, 49u)] // 150 × 0.33 = 49.5, rounded down
    public void Resizing_by_percentage_rounds_down(double percent, uint width, uint height)
    {
        using var image = new MagickImage(MagickColors.SteelBlue, 200, 150);

        ImageTransforms.ResizeByPercent(image, percent, FilterType.Lanczos);

        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
    }

    [Fact]
    public void Resizing_to_a_printed_size_ends_at_the_target_resolution()
    {
        // 2 × 1 inches at 100 dpi is 201 × 101 pixels (the original adds one), then ×3 for 300 dpi.
        using var image = new MagickImage(MagickColors.SteelBlue, 400, 400) { Density = new Density(100, DensityUnit.PixelsPerInch) };
        var size = new UnitPoint { X = Unit.FromInch(2), Y = Unit.FromInch(1) };

        ImageTransforms.ResizeToPrintedSize(image, size, FilterType.Lanczos);

        Assert.Equal(603u, image.Width);
        Assert.Equal(303u, image.Height);
        Assert.Equal(300, image.Density.X);
        Assert.Equal(DensityUnit.PixelsPerInch, image.Density.Units);
    }

    [Fact]
    public void Resizing_to_a_printed_size_takes_an_image_without_resolution_to_fill_that_size()
    {
        // 300 × 150 pixels over 2 × 1 inches is 150 dpi: 301 × 151 pixels, then ×2.
        using var image = new MagickImage(MagickColors.SteelBlue, 300, 150);
        var size = new UnitPoint { X = Unit.FromInch(2), Y = Unit.FromInch(1) };

        ImageTransforms.ResizeToPrintedSize(image, size, FilterType.Lanczos);

        Assert.Equal(602u, image.Width);
        Assert.Equal(302u, image.Height);
    }

    [Fact]
    public void Watermark_is_scaled_to_the_image_and_drawn_over_it()
    {
        using var image = new MagickImage(MagickColors.White, 40, 40);
        using var watermark = new MagickImage(MagickColors.Transparent, 10, 10);
        using (IPixelCollection<ushort> pixels = watermark.GetPixels())
        {
            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 10; x++)
                {
                    pixels.SetPixel(x, y, [ushort.MaxValue, 0, 0, ushort.MaxValue]);
                }
            }
        }

        ImageTransforms.OverlayWatermark(image, watermark, FilterType.Point);

        Assert.Equal(40u, watermark.Width);
        Assert.Equal("#FF0000", Pixels.At(image, 20, 5));
        Assert.Equal("#FFFFFF", Pixels.At(image, 20, 35));
    }

    // ------------------------------------------------------------ mirror bleed

    [Fact]
    public void Mirror_frame_reflects_each_edge_outwards()
    {
        // A 4 × 4 image at 72 dpi with a frame of 2/72 inch: 2 pixels on every side.
        string path = _scratch.File("grid.png");
        using (MagickImage grid = Pixels.Grid(4, 4, dpi: 72))
        {
            grid.Write(path);
        }

        (AppError error, MagickImage framed) = MirrorEffect.AddFrame(path, Unit.FromInch(2 / 72.0), FilterType.Point, null);
        using (framed)
        {
            Assert.False(error.HasError);
            Assert.Equal(8u, framed.Width);
            Assert.Equal(8u, framed.Height);

            // The image itself sits in the middle.
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    Assert.Equal(G(x, y, 4), Pixels.At(framed, x + 2, y + 2));
                }
            }

            // Left and right: columns 0 and 1 of the image reflected, nearest column next to the edge.
            Assert.Equal(G(0, 1, 4), Pixels.At(framed, 1, 3));
            Assert.Equal(G(1, 1, 4), Pixels.At(framed, 0, 3));
            Assert.Equal(G(3, 1, 4), Pixels.At(framed, 6, 3));
            Assert.Equal(G(2, 1, 4), Pixels.At(framed, 7, 3));

            // Top and bottom: whole rows reflected, corners included.
            Assert.Equal(Row(framed, 2), Row(framed, 1));
            Assert.Equal(Row(framed, 3), Row(framed, 0));
            Assert.Equal(Row(framed, 5), Row(framed, 6));
            Assert.Equal(Row(framed, 4), Row(framed, 7));
            Assert.Equal(G(1, 1, 4), Pixels.At(framed, 0, 0));
            Assert.Equal(G(2, 2, 4), Pixels.At(framed, 7, 7));
        }
    }

    [Fact]
    public void Mirror_frame_keeps_resolution_and_format()
    {
        string path = _scratch.Image("card.jpg", 100, 140, dpi: 300);

        (_, MagickImage framed) = MirrorEffect.AddFrame(path, Unit.FromMillimeter(3), FilterType.Lanczos, null);
        using (framed)
        {
            // 3 mm at 300 dpi is 35.4 pixels, rounded to 35 on each side.
            Assert.Equal(170u, framed.Width);
            Assert.Equal(210u, framed.Height);
            Assert.Equal(300, framed.Density.X);
            Assert.Equal(MagickFormat.Jpeg, framed.Format);
        }
    }

    [Fact]
    public void Mirror_frame_of_a_png_uses_its_resolution_in_dots_per_inch()
    {
        string path = _scratch.Image("card.png", 100, 140, dpi: 300);

        (_, MagickImage framed) = MirrorEffect.AddFrame(path, Unit.FromMillimeter(3), FilterType.Lanczos, null);
        using (framed)
        {
            Assert.Equal(170u, framed.Width);
            Assert.Equal(210u, framed.Height);
        }
    }

    [Fact]
    public void Mirror_that_only_adds_height_leaves_the_image_unflipped()
    {
        using MagickImage image = Pixels.Grid(4, 4);

        (_, MagickImage framed) = MirrorEffect.Apply(image, new PointI(4, 8), FilterType.Point, null);
        using (framed)
        {
            Assert.Equal(4u, framed.Width);
            Assert.Equal(8u, framed.Height);
            Assert.Equal([G(0, 0, 4), G(1, 0, 4), G(2, 0, 4), G(3, 0, 4)], Row(framed, 2));
            Assert.Equal(Row(framed, 2), Row(framed, 1));
            Assert.Equal(Row(framed, 5), Row(framed, 6));
        }
    }

    [Fact]
    public void Mirror_that_only_adds_width_reflects_the_sides()
    {
        using MagickImage image = Pixels.Grid(4, 4);

        (_, MagickImage framed) = MirrorEffect.Apply(image, new PointI(8, 4), FilterType.Point, null);
        using (framed)
        {
            Assert.Equal(8u, framed.Width);
            Assert.Equal(4u, framed.Height);
            Assert.Equal(
                [G(1, 0, 4), G(0, 0, 4), G(0, 0, 4), G(1, 0, 4), G(2, 0, 4), G(3, 0, 4), G(3, 0, 4), G(2, 0, 4)],
                Row(framed, 0));
        }
    }

    [Fact]
    public void Mirror_with_an_odd_difference_puts_the_extra_pixel_right_and_below()
    {
        using MagickImage image = Pixels.Grid(4, 4);

        (_, MagickImage framed) = MirrorEffect.Apply(image, new PointI(7, 7), FilterType.Point, null);
        using (framed)
        {
            Assert.Equal(7u, framed.Width);
            Assert.Equal(7u, framed.Height);

            // One pixel on the left and above, two on the right and below.
            Assert.Equal(G(0, 0, 4), Pixels.At(framed, 1, 1));
            Assert.Equal(G(3, 3, 4), Pixels.At(framed, 4, 4));
            Assert.Equal(
                [G(0, 0, 4), G(0, 0, 4), G(1, 0, 4), G(2, 0, 4), G(3, 0, 4), G(3, 0, 4), G(2, 0, 4)],
                Row(framed, 1));
            Assert.Equal(Column(framed, 1)[1], Column(framed, 1)[0]);
            Assert.Equal(Pixels.At(framed, 1, 4), Pixels.At(framed, 1, 5));
            Assert.Equal(Pixels.At(framed, 1, 3), Pixels.At(framed, 1, 6));
        }
    }

    [Fact]
    public void Mirror_towards_a_smaller_size_shrinks_the_image_and_frames_it_back()
    {
        using var image = new MagickImage(MagickColors.SteelBlue, 100, 100) { Density = new Density(300, DensityUnit.PixelsPerInch) };

        (_, MagickImage framed) = MirrorEffect.Apply(image, new PointI(80, 90), FilterType.Lanczos, null);
        using (framed)
        {
            Assert.Equal(100u, framed.Width);
            Assert.Equal(100u, framed.Height);
            Assert.Equal(80u, image.Width);
            Assert.Equal(90u, image.Height);
        }
    }

    [Fact]
    public void Mirror_for_a_card_grows_from_the_face_size_to_the_cell_size()
    {
        // Face 60 × 90 mm inside a cell of 66 × 96 mm: the image grows by 10% and 6.67%.
        var settings = new ProjectSettings();
        settings.Front.Size = new UnitPoint { X = Unit.FromMillimeter(60), Y = Unit.FromMillimeter(90) };
        settings.Back.Size = new UnitPoint { X = Unit.FromMillimeter(66), Y = Unit.FromMillimeter(96) };
        settings.Grid.CardSize = new UnitPoint { X = Unit.FromMillimeter(66), Y = Unit.FromMillimeter(96) };
        settings.Other.ResizeFilter = FilterType.Lanczos;

        using var front = new MagickImage(MagickColors.SteelBlue, 600, 900);
        (_, MagickImage framed) = MirrorEffect.Apply(front, settings, FaceType.Front);
        using (framed)
        {
            Assert.Equal(660u, framed.Width);
            Assert.Equal(960u, framed.Height);
        }

        // The back already has the size of the cell, so nothing is added.
        using var back = new MagickImage(MagickColors.SteelBlue, 600, 900);
        (_, MagickImage same) = MirrorEffect.Apply(back, settings, FaceType.Back);
        using (same)
        {
            Assert.Equal(600u, same.Width);
            Assert.Equal(900u, same.Height);
        }
    }
}
