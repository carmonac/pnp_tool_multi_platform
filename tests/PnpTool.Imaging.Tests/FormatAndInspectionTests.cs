using ImageMagick;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;

namespace PnpTool.Imaging.Tests;

[Collection(GlobalState.Name)]
public sealed class FormatAndInspectionTests : IDisposable
{
    private readonly Scratch _scratch = new();

    public void Dispose() => _scratch.Dispose();

    /// <summary>Formats the program accepts; the flag says whether the format can record a resolution.</summary>
    [Theory]
    [InlineData("card.png", MagickFormat.Png, true)]
    [InlineData("card.jpg", MagickFormat.Jpeg, true)]
    [InlineData("card.bmp", MagickFormat.Bmp, true)]
    [InlineData("card.tiff", MagickFormat.Tiff, true)]
    [InlineData("card.gif", MagickFormat.Gif, false)]
    [InlineData("card.webp", MagickFormat.WebP, false)]
    [InlineData("card.avif", MagickFormat.Avif, false)]
    public void Supported_formats_load_with_their_size_and_resolution(string name, MagickFormat format, bool storesResolution)
    {
        // 600 × 900 pixels at 300 dpi is 2 × 3 inches; without a resolution 96 dpi is assumed.
        string path = _scratch.Image(name, 600, 900, dpi: 300);

        Assert.True(ImageFormats.IsImage(path));
        Assert.False(ImageFormats.CheckReadable(path).HasError);
        Assert.Equal(format, ImageFormats.FormatOf(path));

        CardFace face = MagickImageInspector.GetImageProperties(path, null);
        Assert.False(face.IsCorrupt);
        Assert.False(face.IsCmyk);
        Assert.Equal(path, face.Path);
        Assert.Equal(path, face.OriginalPath);
        Assert.True(face.SizeOnDiskMb > 0);
        double dpi = storesResolution ? 300 : 96;
        Assert.Equal(600 / dpi, face.Size.X.Inch, 2);
        Assert.Equal(900 / dpi, face.Size.Y.Inch, 2);
        Assert.Equal((uint)dpi, ImageDensity.Normalize(path).X);

        using var loaded = new MagickImage(path);
        Assert.Equal(600u, loaded.Width);
        Assert.Equal(900u, loaded.Height);
    }

    [Theory]
    [InlineData("png", MagickFormat.Png)]
    [InlineData(".PNG", MagickFormat.Png)]
    [InlineData(".jpg", MagickFormat.Jpg)]
    [InlineData("JPEG", MagickFormat.Jpeg)]
    [InlineData(".webp", MagickFormat.WebP)]
    [InlineData("avif", MagickFormat.Avif)]
    [InlineData("tiff", MagickFormat.Tiff)]
    [InlineData("", MagickFormat.Jpg)]
    public void Extension_names_a_format(string extension, MagickFormat format)
    {
        Assert.Equal(format, ImageFormats.FormatFromExtension(extension));
    }

    [Fact]
    public void Unknown_extension_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => ImageFormats.FormatFromExtension(".notaformat"));
    }

    [Fact]
    public void Files_that_are_not_images_are_recognised()
    {
        string text = _scratch.File("notes.txt");
        File.WriteAllText(text, "just some words");
        string broken = _scratch.File("broken.png");
        File.WriteAllBytes(broken, [0x89, 0x50, 0x4E, 0x47, 1, 2, 3]);

        Assert.False(ImageFormats.IsImage(null));
        Assert.False(ImageFormats.IsImage(string.Empty));
        Assert.False(ImageFormats.IsImage(_scratch.File("missing.png")));
        Assert.False(ImageFormats.IsImage(broken));

        AppError error = ImageFormats.CheckReadable(broken);
        Assert.True(error.HasError);
        Assert.StartsWith(broken + " - ", error.Message);
        Assert.DoesNotContain(StringResources.MissingPrefix, error.Message);
    }

    [Fact]
    public void Unreadable_file_gives_a_face_marked_corrupt()
    {
        string broken = _scratch.File("broken.jpg");
        File.WriteAllText(broken, "not a jpeg");

        CardFace face = new MagickImageInspector().GetImageProperties(broken);

        Assert.True(face.IsCorrupt);
        Assert.Equal(broken, face.OriginalPath);
    }

    [Fact]
    public void Cmyk_image_is_detected()
    {
        string path = _scratch.File("cmyk.jpg");
        using (var image = new MagickImage(MagickColors.SteelBlue, 20, 20))
        {
            image.ColorSpace = ColorSpace.CMYK;
            image.Write(path);
        }

        Assert.True(ImageFormats.IsCmyk(path));
        Assert.True(MagickImageInspector.GetImageProperties(path, null).IsCmyk);
        Assert.False(ImageFormats.IsCmyk(_scratch.Image("rgb.jpg", 20, 20)));
    }

    [Fact]
    public void Embedded_colour_profile_is_read()
    {
        string with = _scratch.File("with.jpg");
        using (var image = new MagickImage(MagickColors.SteelBlue, 20, 20))
        {
            image.SetProfile(ColorProfiles.SRGB);
            image.Write(with);
        }

        Assert.NotNull(MagickImageInspector.GetImageProperties(with, null).ColorProfile);
        Assert.Null(MagickImageInspector.GetImageProperties(_scratch.Image("without.jpg", 20, 20), null).ColorProfile);
    }

    [Theory]
    [InlineData(Orientation.TopLeft, 0, false, false, false)]
    [InlineData(Orientation.TopRight, 0, true, false, false)]
    [InlineData(Orientation.BottomRight, 180, false, false, false)]
    [InlineData(Orientation.BottomLeft, 0, false, true, false)]
    [InlineData(Orientation.RightTop, 90, false, false, true)]
    [InlineData(Orientation.LeftBottom, 270, false, false, true)]
    public void Exif_orientation_gives_the_rotation_and_swaps_the_size_for_quarter_turns(
        Orientation orientation, int angle, bool flipHorizontal, bool flipVertical, bool swapped)
    {
        string path = _scratch.File("photo.jpg");
        using (var image = new MagickImage(MagickColors.SteelBlue, 300, 600))
        {
            image.Density = new Density(300, DensityUnit.PixelsPerInch);
            // Magick writes the orientation only when the image and its EXIF data both state it.
            image.Orientation = orientation;
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.Orientation, (ushort)orientation);
            image.SetProfile(exif);
            image.Write(path);
        }

        var rotation = MagickImageInspector.ExifRotation(path);
        Assert.Equal(angle, rotation.Angle);
        Assert.Equal(flipHorizontal, rotation.FlipHorizontal);
        Assert.Equal(flipVertical, rotation.FlipVertical);

        CardFace face = MagickImageInspector.GetImageProperties(path, null);
        Assert.Equal(swapped ? 2 : 1, face.Size.X.Inch, 6);
        Assert.Equal(swapped ? 1 : 2, face.Size.Y.Inch, 6);
    }

    [Fact]
    public void Missing_file_has_no_exif_rotation()
    {
        Assert.Equal(0, MagickImageInspector.ExifRotation(_scratch.File("missing.jpg")).Angle);
        Assert.Equal(0, MagickImageInspector.ExifRotation((string?)null).Angle);
    }

    [Fact]
    public void Registering_makes_the_inspector_available_to_the_models()
    {
        MagickImageInspector.Register();

        CardFace face = ImageInspector.Current.GetImageProperties(_scratch.Image("card.png", 300, 300));

        Assert.Equal(1, face.Size.X.Inch, 6);
    }

    [Fact]
    public void Stock_images_are_looked_up_in_the_images_folder()
    {
        AppSettings.ImagesFolder = _scratch.Root + Path.DirectorySeparatorChar;
        string existing = _scratch.Image("card.png", 10, 10);

        Assert.Equal(_scratch.File("calibracion.jpg"), StockImages.PathOf(StockImage.Calibration));
        Assert.Equal(_scratch.File("calibracion_marca.jpg"), StockImages.PathOf(StockImage.CalibrationMark));
        Assert.Equal(_scratch.File("hexa.png"), StockImages.HexagonPath);

        (string path, AppError error) = StockImages.OrNotFound(existing);
        Assert.Equal(existing, path);
        Assert.False(error.HasError);

        (path, error) = StockImages.OrNotFound(_scratch.File("gone.png"));
        Assert.Equal(_scratch.File("Image_NotFound.jpg"), path);
        Assert.True(error.HasError);
    }

    [Fact]
    public void Temporary_copy_is_named_after_the_whole_path_and_reused_while_fresh()
    {
        string source = _scratch.File("deck", "ace.png");
        string stem = string.Join("_", source.Split(Path.DirectorySeparatorChar)[1..^1]) + "_ace_";
        string first = source.Split(Path.DirectorySeparatorChar)[0];
        string prefix = (first.Length > 0 ? first[..1] : string.Empty) + "_";

        (string path, bool mustCreate) = TempImageFiles.UniqueName(source, "Png");
        Assert.Equal(Path.Combine(AppSettings.TempFolder, prefix + stem + "0.Png"), path);
        Assert.True(mustCreate);

        // Just written: the same name comes back and need not be made again.
        File.WriteAllText(path, "x");
        Assert.Equal((path, false), TempImageFiles.UniqueName(source, "Png"));

        // Older than 50 seconds: a new name with the next number.
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-2));
        (string second, mustCreate) = TempImageFiles.UniqueName(source, "Png");
        Assert.EndsWith("_ace_1.Png", second);
        Assert.True(mustCreate);
    }

    [Fact]
    public void Temporary_copy_of_a_deeply_nested_image_keeps_a_usable_file_name()
    {
        string deep = Path.Combine([_scratch.Root, .. Enumerable.Repeat("a-rather-long-folder-name", 12), "ace of spades.png"]);

        (string path, _) = TempImageFiles.UniqueName(deep, "Png");
        (string again, _) = TempImageFiles.UniqueName(deep, "Png");

        Assert.True(System.Text.Encoding.UTF8.GetByteCount(Path.GetFileName(path)) < 200, Path.GetFileName(path));
        Assert.EndsWith("ace of spades_0.Png", path);
        Assert.Equal(path, again);
        File.WriteAllText(path, "x");
    }
}
