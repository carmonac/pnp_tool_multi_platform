using ImageMagick;
using PnpTool.Core.CustomDecks;
using PnpTool.Core.Infrastructure;

namespace PnpTool.Imaging.Tests;

[Collection(GlobalState.Name)]
public sealed class TextAndCustomCardTests : IDisposable
{
    private readonly Scratch _scratch = new();

    public void Dispose() => _scratch.Dispose();

    /// <summary>A font family that exists on this computer, or null on a system without fonts.</summary>
    private static string? AnyFamily() =>
        FontCatalog.Has("Arial") ? "Arial"
        : FontCatalog.Has("DejaVu Sans") ? "DejaVu Sans"
        : FontCatalog.Has("Liberation Sans") ? "Liberation Sans"
        : FontCatalog.InstalledFamilies.FirstOrDefault();

    /// <summary>A font file of this computer to stand in for a font shipped with the program.</summary>
    private static string? AnyFontFile()
    {
        string[] folders = ["/System/Library/Fonts/Supplemental", "/Library/Fonts", "/usr/share/fonts", "/usr/local/share/fonts"];
        return folders
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.ttf", SearchOption.AllDirectories))
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static int OpaquePixels(MagickImage image)
    {
        using IPixelCollection<ushort> pixels = image.GetPixels();
        int count = 0;
        foreach (IPixel<ushort> pixel in pixels)
        {
            if (pixel.ToColor()!.A > 32000)
            {
                count++;
            }
        }

        return count;
    }

    [Fact]
    public void Text_is_drawn_in_its_colour_on_a_transparent_background()
    {
        if (AnyFamily() is not { } family)
        {
            return;
        }

        var font = new FontSpec { FontFamily = family, FontSize = 32, Color = "255_255_0_0" };

        using MagickImage image = TextRenderer.Render("HI", font, 300);

        // Size 32 is a third of an inch: 100 pixels at 300 dpi, plus the font's line spacing.
        Assert.InRange((int)image.Height, 100, 160);
        Assert.InRange((int)image.Width, 60, 250);
        Assert.Equal(300, image.Density.X);
        Assert.True(image.HasAlpha);
        Assert.InRange(OpaquePixels(image), 500, (int)(image.Width * image.Height * 0.8));

        using IPixelCollection<ushort> pixels = image.GetPixels();
        Assert.Equal(0, pixels.GetPixel(0, 0).ToColor()!.A);
        IMagickColor<ushort> ink = pixels.Select(pixel => pixel.ToColor()!).First(color => color.A == ushort.MaxValue);
        Assert.Equal("#FF0000", Pixels.Hex(ink));
    }

    [Fact]
    public void Text_scales_with_font_size_and_resolution()
    {
        if (AnyFamily() is not { } family)
        {
            return;
        }

        using MagickImage small = TextRenderer.Render("Team", new FontSpec { FontFamily = family, FontSize = 16 }, 300);
        using MagickImage large = TextRenderer.Render("Team", new FontSpec { FontFamily = family, FontSize = 32 }, 300);
        using MagickImage screen = TextRenderer.Render("Team", new FontSpec { FontFamily = family, FontSize = 32 }, 96);

        Assert.InRange((double)large.Width / small.Width, 1.9, 2.1);
        Assert.InRange((double)large.Height / small.Height, 1.9, 2.1);
        Assert.InRange((double)large.Width / screen.Width, 3.0, 3.25);
        Assert.Equal(96, screen.Density.X);
    }

    [Fact]
    public void Line_breaks_start_new_lines()
    {
        if (AnyFamily() is not { } family)
        {
            return;
        }

        var font = new FontSpec { FontFamily = family, FontSize = 24 };
        using MagickImage one = TextRenderer.Render("Player", font, 300);
        using MagickImage two = TextRenderer.Render("Player\nNumber ten", font, 300);

        Assert.InRange((double)two.Height / one.Height, 1.95, 2.05);
        Assert.True(two.Width > one.Width);
    }

    [Fact]
    public void Bold_text_is_wider_than_regular_text()
    {
        if (!FontCatalog.Has("Arial"))
        {
            return;
        }

        using MagickImage regular = TextRenderer.Render("Wide words", new FontSpec { FontFamily = "Arial", FontSize = 32, FontWeight = 400 }, 300);
        using MagickImage bold = TextRenderer.Render("Wide words", new FontSpec { FontFamily = "Arial", FontSize = 32, FontWeight = 700 }, 300);

        Assert.True(bold.Width > regular.Width, $"{bold.Width} > {regular.Width}");
    }

    [Fact]
    public void Empty_text_gives_a_single_transparent_pixel()
    {
        using MagickImage empty = TextRenderer.Render(string.Empty, new FontSpec(), 300);
        using MagickImage none = TextRenderer.Render(null, new FontSpec(), 300);

        Assert.Equal(1u, empty.Width);
        Assert.Equal(1u, empty.Height);
        Assert.Equal(0, OpaquePixels(empty));
        Assert.Equal(1u, none.Width);
    }

    [Fact]
    public void Unknown_font_family_still_renders_with_a_fallback()
    {
        if (AnyFamily() == null)
        {
            return;
        }

        Assert.False(FontCatalog.Has("No Such Font Family 123"));
        using MagickImage image = TextRenderer.Render("Text", new FontSpec { FontFamily = "No Such Font Family 123", FontSize = 24 }, 300);

        Assert.True(OpaquePixels(image) > 100);
    }

    [Fact]
    public void Fonts_shipped_with_the_program_are_found_before_installed_ones()
    {
        if (AnyFontFile() is not { } fontFile)
        {
            return;
        }

        string assets = AppPaths.AssetsFolder;
        string user = AppPaths.UserDataFolder;
        try
        {
            Directory.CreateDirectory(_scratch.File("assets", "fonts"));
            File.Copy(fontFile, _scratch.File("assets", "fonts", Path.GetFileName(fontFile)));
            File.WriteAllText(_scratch.File("assets", "fonts", "readme.txt"), "not a font");
            AppPaths.Initialize(_scratch.File("assets"), _scratch.File("user"));
            FontCatalog.Reload();

            string family = Assert.Single(FontCatalog.BundledFamilies);
            Assert.True(FontCatalog.Has(family));
            Assert.Equal(family, FontCatalog.Resolve(new FontSpec { FontFamily = family.ToUpperInvariant() }).FamilyName);

            using MagickImage image = TextRenderer.Render("Bundled", new FontSpec { FontFamily = family, FontSize = 24 }, 300);
            Assert.True(OpaquePixels(image) > 100);
        }
        finally
        {
            AppPaths.Initialize(assets, user);
            FontCatalog.Reload();
        }
    }

    /// <summary>The font the program ships in place of Times New Roman, in its four styles.</summary>
    [Fact]
    public void Liberation_serif_is_shipped_and_renders()
    {
        string assets = AppPaths.AssetsFolder;
        string user = AppPaths.UserDataFolder;
        try
        {
            AppPaths.Initialize(AppPaths.FindAssetsFolder(AppContext.BaseDirectory), _scratch.File("user"));
            FontCatalog.Reload();

            Assert.Equal(["Liberation Serif"], FontCatalog.BundledFamilies);
            Assert.True(FontCatalog.Has("Liberation Serif"));

            var regular = new FontSpec { FontFamily = "Liberation Serif", FontSize = 32 };
            var bold = new FontSpec { FontFamily = "Liberation Serif", FontSize = 32, FontWeight = 700 };
            var italic = new FontSpec { FontFamily = "Liberation Serif", FontSize = 32, FontStyle = FontSlant.Italic };
            var boldItalic = new FontSpec { FontFamily = "Liberation Serif", FontSize = 32, FontWeight = 700, FontStyle = FontSlant.Italic };
            Assert.Equal(400, FontCatalog.Resolve(regular).FontWeight);
            Assert.Equal(700, FontCatalog.Resolve(bold).FontWeight);
            Assert.True(FontCatalog.Resolve(italic).IsItalic);
            Assert.False(FontCatalog.Resolve(regular).IsItalic);
            Assert.True(FontCatalog.Resolve(boldItalic).IsItalic);
            Assert.Equal(700, FontCatalog.Resolve(boldItalic).FontWeight);

            using MagickImage plain = TextRenderer.Render("Print and play", regular, 300);
            using MagickImage heavy = TextRenderer.Render("Print and play", bold, 300);
            Assert.True(OpaquePixels(plain) > 1000);
            Assert.True(heavy.Width > plain.Width);
        }
        finally
        {
            AppPaths.Initialize(assets, user);
            FontCatalog.Reload();
        }
    }

    [Fact]
    public void No_bundled_fonts_when_the_folder_is_missing()
    {
        string assets = AppPaths.AssetsFolder;
        string user = AppPaths.UserDataFolder;
        try
        {
            AppPaths.Initialize(_scratch.File("no-assets"), _scratch.File("user"));
            FontCatalog.Reload();
            Assert.Empty(FontCatalog.BundledFamilies);
        }
        finally
        {
            AppPaths.Initialize(assets, user);
            FontCatalog.Reload();
        }
    }

    // ------------------------------------------------------------ custom cards

    /// <summary>A 100 × 140 picture: red on the left half, transparent on the right.</summary>
    private string Picture(string name)
    {
        using var image = new MagickImage(MagickColors.Red, 100, 140) { Format = MagickFormat.Png, Density = new Density(300, DensityUnit.PixelsPerInch) };
        image.Alpha(AlphaAction.Set);
        using (IPixelCollection<ushort> pixels = image.GetPixels())
        {
            for (int y = 0; y < 140; y++)
            {
                for (int x = 50; x < 100; x++)
                {
                    pixels.SetPixel(x, y, [0, 0, 0, 0]);
                }
            }
        }

        string path = _scratch.File(name);
        image.Write(path);
        return path;
    }

    [Fact]
    public void Card_picture_is_laid_over_its_background()
    {
        var card = new CustomCard
        {
            NameSpanish = "As de oros",
            ImagePath = Picture("ace.png"),
            BackgroundImagePath = _scratch.Image("background.png", 200, 280, dpi: 300, color: MagickColors.Blue),
        };
        Directory.CreateDirectory(_scratch.File("out"));

        CustomCardImages.Merge(card, _scratch.File("out"), null);

        Assert.Equal(_scratch.File("out", "As de oros.png"), card.MergedImagePath);
        using var merged = new MagickImage(card.MergedImagePath);
        Assert.Equal(100u, merged.Width);
        Assert.Equal(140u, merged.Height);
        Assert.Equal("#FF0000", Pixels.At(merged, 10, 70));
        Assert.Equal("#0000FF", Pixels.At(merged, 90, 70));
    }

    [Fact]
    public void Picture_of_a_bundled_deck_is_found_under_the_decks_folder()
    {
        Directory.CreateDirectory(_scratch.File("decks", "imagenes", "uno"));
        File.Move(Picture("zero.png"), _scratch.File("decks", "imagenes", "uno", "0_rojo.png"));
        var card = new CustomCard { NameSpanish = "0 rojo", ImagePath = @"\imagenes\uno\0_rojo.png" };
        AppSettings.ImagesFolder = _scratch.Root + Path.DirectorySeparatorChar;
        _scratch.Image("blanco.jpg", 50, 70, color: MagickColors.White);

        CustomCardImages.Merge(card, _scratch.Root, _scratch.File("decks") + Path.DirectorySeparatorChar);

        using var merged = new MagickImage(card.MergedImagePath);
        Assert.Equal(100u, merged.Width);
        Assert.Equal("#FF0000", Pixels.At(merged, 10, 70));
        Assert.Equal("#FFFFFF", Pixels.At(merged, 90, 70));   // no background: the blank image shows through
    }

    [Fact]
    public void Missing_picture_is_replaced_by_the_blank_image()
    {
        AppSettings.ImagesFolder = _scratch.Root + Path.DirectorySeparatorChar;
        string blank = _scratch.Image("blanco.jpg", 50, 70, color: MagickColors.White);
        var card = new CustomCard { NameSpanish = "lost", ImagePath = _scratch.File("gone.png") };

        CustomCardImages.Merge(card, _scratch.Root, null);

        Assert.Equal(blank, card.ImagePath);
        Assert.True(File.Exists(card.MergedImagePath));
        using var merged = new MagickImage(card.MergedImagePath);
        Assert.Equal(50u, merged.Width);
    }

    [Fact]
    public void Sports_card_uses_the_template_as_its_picture_and_takes_texts()
    {
        var card = new CustomCard { NameSpanish = "Jugador 10", BackgroundImagePath = _scratch.Image("photo.png", 300, 420, color: MagickColors.Blue) };
        string template = _scratch.File("template.png");
        using (var frame = new MagickImage(MagickColors.Transparent, 300, 420) { Format = MagickFormat.Png, Density = new Density(300, DensityUnit.PixelsPerInch) })
        {
            frame.Write(template);
        }

        CustomCardImages.MergeSports(card, _scratch.Root, template);
        Assert.Equal(template, card.ImagePath);
        using (var before = new MagickImage(card.MergedImagePath))
        {
            Assert.Equal("#0000FF", Pixels.At(before, 150, 210));
        }

        if (AnyFamily() is not { } family)
        {
            return;
        }

        CustomCardImages.AddText(card, null, new FontSpec { FontFamily = family }, new PointD(0.1, 0.5));
        CustomCardImages.AddText(card, "MMMM", new FontSpec { FontFamily = family, FontSize = 24, FontWeight = 700, Color = "255_255_255_0" }, new PointD(0.1, 0.5));

        using var after = new MagickImage(card.MergedImagePath);
        Assert.Equal(300u, after.Width);
        Assert.Equal("#0000FF", Pixels.At(after, 150, 100));   // above the text: untouched
        int yellow = 0;
        for (int y = 210; y < 300; y++)
        {
            for (int x = 30; x < 290; x++)
            {
                if (Pixels.At(after, x, y) == "#FFFF00")
                {
                    yellow++;
                }
            }
        }

        Assert.True(yellow > 200, "yellow pixels: " + yellow);
    }
}
