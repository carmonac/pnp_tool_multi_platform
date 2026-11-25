using ImageMagick;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Projects;

namespace PnpTool.Imaging.Tests;

[Collection(GlobalState.Name)]
public sealed class ColorManagementTests : IDisposable
{
    private readonly Scratch _scratch = new();
    private readonly string _rgbPath = AppSettings.RgbIccPath;
    private readonly string _cmykPath = AppSettings.CmykIccPath;
    private readonly bool _embed = AppSettings.EmbedColorProfile;
    private readonly ColorConversionMode _mode = ColorManagement.Mode;
    private readonly string _icc;

    public ColorManagementTests()
    {
        _icc = Path.Combine(AppPaths.FindAssetsFolder(AppContext.BaseDirectory), "icc");
        AppSettings.RgbIccPath = Path.Combine(_icc, AppPaths.DefaultRgbProfile);
        AppSettings.CmykIccPath = Path.Combine(_icc, AppPaths.DefaultCmykProfile);
        AppSettings.EmbedColorProfile = false;
    }

    public void Dispose()
    {
        AppSettings.RgbIccPath = _rgbPath;
        AppSettings.CmykIccPath = _cmykPath;
        AppSettings.EmbedColorProfile = _embed;
        ColorManagement.Mode = _mode;
        _scratch.Dispose();
    }

    private static MagickImage Rgb(IMagickColor<ushort> color) =>
        new(color, 8, 8) { Format = MagickFormat.Jpeg, Density = new Density(300, DensityUnit.PixelsPerInch) };

    /// <summary>Cyan, magenta, yellow and black of a pixel in percent.</summary>
    private static int[] Cmyk(MagickImage image, int x = 4, int y = 4)
    {
        using IPixelCollection<ushort> pixels = image.GetPixels();
        ushort[] values = pixels.GetPixel(x, y).ToArray();
        return [.. values.Take(4).Select(value => (int)Math.Round(value * 100.0 / ushort.MaxValue))];
    }

    private static int[] RgbBytes(MagickImage image, int x = 4, int y = 4)
    {
        using IPixelCollection<ushort> pixels = image.GetPixels();
        ushort[] values = pixels.GetPixel(x, y).ToArray();
        return [.. values.Take(3).Select(value => value >> 8)];
    }

    private static void AssertClose(int[] expected, int[] actual, int tolerance)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.True(Math.Abs(expected[i] - actual[i]) <= tolerance, $"[{string.Join(", ", actual)}] is not within {tolerance} of [{string.Join(", ", expected)}]");
        }
    }

    [Fact]
    public void Default_profiles_are_srgb_and_coated_fogra39()
    {
        Assert.Equal(ColorSpace.sRGB, ColorManagement.DefaultRgbProfile().ColorSpace);
        Assert.Equal(ColorSpace.CMYK, ColorManagement.DefaultCmykProfile().ColorSpace);
        Assert.Contains("sRGB", ColorManagement.DefaultRgbProfile().Description);
        Assert.Contains("FOGRA39", ColorManagement.DefaultCmykProfile().Description);
    }

    // ------------------------------------------------------------ as the Windows program converts

    [Fact]
    public void Conversion_through_the_profiles_is_the_default()
    {
        Assert.Equal(ColorConversionMode.Icc, _mode);
    }

    [Fact]
    public void Original_rgb_to_cmyk_is_plain_arithmetic_and_keeps_the_rgb_profile()
    {
        ColorManagement.Mode = ColorConversionMode.Original;
        using MagickImage red = Rgb(MagickColors.Red);
        using MagickImage black = Rgb(MagickColors.Black);
        using MagickImage white = Rgb(MagickColors.White);
        using MagickImage orange = Rgb(MagickColors.Orange);

        foreach (MagickImage image in new[] { red, black, white, orange })
        {
            ColorManagement.RgbToCmyk(image, ColorManagement.DefaultCmykProfile());
            Assert.Equal(ColorSpace.CMYK, image.ColorSpace);
        }

        Assert.Equal([0, 100, 100, 0], Cmyk(red));
        Assert.Equal([0, 0, 0, 100], Cmyk(black));
        Assert.Equal([0, 0, 0, 0], Cmyk(white));
        Assert.Equal([0, 35, 100, 0], Cmyk(orange));

        // The profile left on the CMYK image is the RGB one: the defect described in ColorManagement.
        Assert.Contains("sRGB", red.GetColorProfile()!.Description);
    }

    [Fact]
    public void Original_cmyk_to_rgb_undoes_the_plain_arithmetic()
    {
        ColorManagement.Mode = ColorConversionMode.Original;
        using MagickImage image = Rgb(new MagickColor("#B46432"));

        ColorManagement.RgbToCmyk(image, ColorManagement.DefaultCmykProfile());
        ColorManagement.CmykToRgb(image);

        Assert.Equal(ColorSpace.sRGB, image.ColorSpace);
        AssertClose([0xB4, 0x64, 0x32], RgbBytes(image), 1);
    }

    // ------------------------------------------------------------ through the ICC profiles

    [Fact]
    public void Icc_rgb_to_cmyk_matches_a_direct_conversion_with_the_same_profiles()
    {
        ColorManagement.Mode = ColorConversionMode.Icc;
        ColorProfile cmyk = ColorManagement.DefaultCmykProfile();
        foreach (IMagickColor<ushort> color in new[] { MagickColors.Red, MagickColors.Lime, MagickColors.Blue, MagickColors.White, MagickColors.Black, MagickColors.Gray, MagickColors.Orange })
        {
            using MagickImage converted = Rgb(color);
            using MagickImage reference = Rgb(color);
            Assert.True(reference.TransformColorSpace(ColorManagement.DefaultRgbProfile(), cmyk));

            ColorManagement.RgbToCmyk(converted, cmyk);

            Assert.Equal(ColorSpace.CMYK, converted.ColorSpace);
            AssertClose(Cmyk(reference), Cmyk(converted), 1);
        }
    }

    [Fact]
    public void Icc_rgb_to_cmyk_gives_printable_inks_and_embeds_the_cmyk_profile()
    {
        ColorManagement.Mode = ColorConversionMode.Icc;
        using MagickImage white = Rgb(MagickColors.White);
        using MagickImage red = Rgb(MagickColors.Red);
        using MagickImage black = Rgb(MagickColors.Black);
        ColorProfile cmyk = ColorManagement.DefaultCmykProfile();

        ColorManagement.RgbToCmyk(white, cmyk);
        ColorManagement.RgbToCmyk(red, cmyk);
        ColorManagement.RgbToCmyk(black, cmyk);

        AssertClose([0, 0, 0, 0], Cmyk(white), 1);
        int[] redInks = Cmyk(red);
        Assert.True(redInks[0] < 10 && redInks[1] > 85 && redInks[2] > 80 && redInks[3] < 10, string.Join(", ", redInks));

        // Black is built from all four inks, within the 330% total the FOGRA39 profile allows.
        int[] blackInks = Cmyk(black);
        Assert.True(blackInks[3] > 80 && blackInks[0] > 50, string.Join(", ", blackInks));
        Assert.InRange(blackInks.Sum(), 250, 335);

        Assert.Equal(cmyk.Description, red.GetColorProfile()!.Description);
    }

    [Fact]
    public void Icc_rgb_to_cmyk_reads_the_colours_through_the_profile_the_image_carries()
    {
        // The same numbers mean a more saturated green in Adobe RGB than in sRGB.
        ColorManagement.Mode = ColorConversionMode.Icc;
        using MagickImage plain = Rgb(new MagickColor("#40C040"));
        using MagickImage adobe = Rgb(new MagickColor("#40C040"));
        adobe.SetProfile(ColorProfiles.AdobeRGB1998);
        ColorProfile cmyk = ColorManagement.DefaultCmykProfile();

        ColorManagement.RgbToCmyk(plain, cmyk);
        ColorManagement.RgbToCmyk(adobe, cmyk);

        Assert.NotEqual(Cmyk(plain), Cmyk(adobe));
        Assert.Equal(cmyk.Description, adobe.GetColorProfile()!.Description);
    }

    [Fact]
    public void Icc_cmyk_to_rgb_matches_a_direct_conversion_and_embeds_the_rgb_profile()
    {
        ColorManagement.Mode = ColorConversionMode.Icc;
        ColorProfile cmyk = ColorManagement.DefaultCmykProfile();
        using MagickImage image = Rgb(MagickColors.Orange);
        ColorManagement.RgbToCmyk(image, cmyk);
        using var reference = (MagickImage)image.Clone();
        Assert.True(reference.TransformColorSpace(cmyk, ColorManagement.DefaultRgbProfile()));

        ColorManagement.CmykToRgb(image);

        Assert.Equal(ColorSpace.sRGB, image.ColorSpace);
        AssertClose(RgbBytes(reference), RgbBytes(image), 2);
        Assert.Contains("sRGB", image.GetColorProfile()!.Description);
    }

    [Fact]
    public void Icc_round_trip_through_cmyk_stays_close_for_printable_colours()
    {
        ColorManagement.Mode = ColorConversionMode.Icc;
        using MagickImage image = Rgb(new MagickColor("#B46432"));

        ColorManagement.RgbToCmyk(image, ColorManagement.DefaultCmykProfile());
        ColorManagement.CmykToRgb(image);

        AssertClose([0xB4, 0x64, 0x32], RgbBytes(image), 6);
    }

    [Fact]
    public void Icc_cmyk_image_without_profile_is_read_with_the_default_cmyk_profile()
    {
        ColorManagement.Mode = ColorConversionMode.Icc;
        using MagickImage tagged = Rgb(MagickColors.Orange);
        ColorManagement.RgbToCmyk(tagged, ColorManagement.DefaultCmykProfile());
        using var untagged = (MagickImage)tagged.Clone();
        untagged.RemoveProfile("icc");
        Assert.Null(untagged.GetColorProfile());

        ColorManagement.CmykToRgb(tagged);
        ColorManagement.CmykToRgb(untagged);

        AssertClose(RgbBytes(tagged), RgbBytes(untagged), 1);
    }

    [Theory]
    [InlineData(ColorConversionMode.Original)]
    [InlineData(ColorConversionMode.Icc)]
    public void Cmyk_file_gets_an_rgb_copy_in_the_temporary_folder(ColorConversionMode mode)
    {
        ColorManagement.Mode = mode;
        string source = _scratch.File("cmyk.jpg");
        using (MagickImage image = Rgb(MagickColors.Orange))
        {
            ColorManagement.RgbToCmyk(image, ColorManagement.DefaultCmykProfile());
            image.Write(source);
        }

        Assert.True(ImageFormats.IsCmyk(source));

        string copy = ColorManagement.CmykToRgbFile(source);

        Assert.StartsWith(AppSettings.TempFolder, copy);
        Assert.False(ImageFormats.IsCmyk(copy));
        Assert.True(ImageFormats.IsCmyk(source));
        using var converted = new MagickImage(copy);
        AssertClose([255, 165, 0], RgbBytes(converted), 40);
    }

    // ------------------------------------------------------------ project profile

    private CardFace Face(string name, IColorProfile? profile, FaceType type = FaceType.Front)
    {
        string path = _scratch.File(name);
        using (MagickImage image = Rgb(MagickColors.Orange))
        {
            if (profile != null)
            {
                image.SetProfile(profile);
            }

            image.Write(path);
        }

        CardFace face = MagickImageInspector.GetImageProperties(path, null);
        face.FaceType = type;
        return face;
    }

    private Deck DeckOf(params (CardFace Front, CardFace Back)[] cards)
    {
        var deck = new Deck();
        deck.Cards = [.. cards.Select(card => new Card { Front = card.Front, Back = card.Back })];
        return deck;
    }

    [Fact]
    public void Faces_without_the_project_profile_get_a_tagged_copy_and_are_reported()
    {
        ColorProfile project = ColorManagement.DefaultRgbProfile();
        CardFace matching = Face("ok.jpg", project);
        CardFace untagged = Face("plain.jpg", null);
        CardFace other = Face("adobe.jpg", ColorProfiles.AdobeRGB1998);
        Deck deck = DeckOf((matching, new CardFace()), (untagged, new CardFace()), (other, new CardFace()));

        AppError error = ColorManagement.ApplyProjectProfile(deck, project, doublePanel: false, pdfX: true);

        Assert.True(error.HasError);
        Assert.StartsWith(StringResources.Get("Distintos_Perfiles") + " " + project.Description, error.Message);
        Assert.DoesNotContain("ok.jpg", error.Message);
        Assert.Contains("#1 Frontal " + _scratch.File("plain.jpg"), error.Message);
        Assert.Contains("#2 Frontal (" + ColorProfiles.AdobeRGB1998.Description + ") " + _scratch.File("adobe.jpg"), error.Message);

        Assert.Equal(_scratch.File("ok.jpg"), matching.Path);
        Assert.StartsWith(AppSettings.TempFolder, untagged.Path);
        Assert.StartsWith(AppSettings.TempFolder, other.Path);
        Assert.Equal(_scratch.File("plain.jpg"), untagged.OriginalPath);
        Assert.Equal(project.Description, MagickImageInspector.ColorProfileOf(untagged.Path)!.Description);
        Assert.Equal(project.Description, MagickImageInspector.ColorProfileOf(other.Path)!.Description);
        Assert.Null(MagickImageInspector.ColorProfileOf(_scratch.File("plain.jpg")));
    }

    [Fact]
    public void Backs_are_checked_only_in_double_panel_mode()
    {
        ColorProfile project = ColorManagement.DefaultRgbProfile();
        CardFace front = Face("front.jpg", project);
        CardFace back = Face("back.jpg", null, FaceType.Back);

        AppError single = ColorManagement.ApplyProjectProfile(DeckOf((front, back)), project, doublePanel: false, pdfX: true);
        Assert.False(single.HasError);
        Assert.Equal(_scratch.File("back.jpg"), back.Path);

        AppError both = ColorManagement.ApplyProjectProfile(DeckOf((front, back)), project, doublePanel: true, pdfX: true);
        Assert.True(both.HasError);
        Assert.Contains("#0 Trasera " + _scratch.File("back.jpg"), both.Message);
        Assert.StartsWith(AppSettings.TempFolder, back.Path);
    }

    [Fact]
    public void Nothing_is_checked_when_the_pdf_embeds_no_profile()
    {
        CardFace untagged = Face("plain.jpg", null);
        Deck deck = DeckOf((untagged, new CardFace()));

        AppError error = ColorManagement.ApplyProjectProfile(deck, ColorManagement.DefaultRgbProfile(), doublePanel: false, pdfX: false);
        Assert.False(error.HasError);
        Assert.Equal(_scratch.File("plain.jpg"), untagged.Path);

        AppSettings.EmbedColorProfile = true;
        error = ColorManagement.ApplyProjectProfile(deck, ColorManagement.DefaultRgbProfile(), doublePanel: false, pdfX: false);
        Assert.True(error.HasError);
    }

    // ------------------------------------------------------------ images going into the PDF

    private OtherSettings Project(ProjectColorSpace colorSpace) => new()
    {
        ColorSpace = colorSpace,
        ColorProfilePath = Path.Combine(_icc, "Adobe", "RGB", "AdobeRGB1998.icc"),
    };

    [Fact]
    public void Cmyk_image_becomes_rgb_in_an_rgb_project_and_stays_cmyk_in_a_cmyk_project()
    {
        using MagickImage forRgb = Rgb(MagickColors.Orange);
        ColorManagement.RgbToCmyk(forRgb, ColorManagement.DefaultCmykProfile());
        using var forCmyk = (MagickImage)forRgb.Clone();
        int[] inks = Cmyk(forCmyk);

        ColorManagement.PrepareForPdf(forRgb, isCmyk: true, forRgb.GetColorProfile(), Project(ProjectColorSpace.RGB), pdfX: false);
        ColorManagement.PrepareForPdf(forCmyk, isCmyk: true, forCmyk.GetColorProfile(), Project(ProjectColorSpace.CMYK), pdfX: false);

        Assert.Equal(ColorSpace.sRGB, forRgb.ColorSpace);
        Assert.Equal(ColorSpace.CMYK, forCmyk.ColorSpace);
        Assert.Equal(inks, Cmyk(forCmyk));
    }

    [Fact]
    public void Rgb_image_with_a_profile_is_left_alone_unless_a_profile_must_be_embedded()
    {
        using MagickImage image = Rgb(MagickColors.Orange);
        image.SetProfile(ColorProfiles.SRGB);

        ColorManagement.PrepareForPdf(image, isCmyk: false, image.GetColorProfile(), Project(ProjectColorSpace.CMYK), pdfX: false);

        Assert.Equal(ColorSpace.sRGB, image.ColorSpace);
        Assert.Equal(ColorProfiles.SRGB.Description, image.GetColorProfile()!.Description);
    }

    [Theory]
    [InlineData(true, false, true)]    // PDF/X
    [InlineData(false, true, true)]    // setting to embed profiles
    [InlineData(false, false, false)]  // neither, but the image has no profile
    public void Rgb_image_is_converted_for_a_cmyk_project_when_a_profile_is_embedded(bool pdfX, bool embedSetting, bool hasProfile)
    {
        ColorManagement.Mode = ColorConversionMode.Icc;
        AppSettings.EmbedColorProfile = embedSetting;
        using MagickImage image = Rgb(MagickColors.Orange);
        if (hasProfile)
        {
            image.SetProfile(ColorProfiles.SRGB);
        }

        ColorManagement.PrepareForPdf(image, isCmyk: false, image.GetColorProfile(), Project(ProjectColorSpace.CMYK), pdfX);

        Assert.Equal(ColorSpace.CMYK, image.ColorSpace);
        Assert.Contains("FOGRA39", image.GetColorProfile()!.Description);
        int[] inks = Cmyk(image);
        Assert.True(inks[0] < 15 && inks[2] > 80, string.Join(", ", inks));
    }

    [Fact]
    public void Rgb_image_is_tagged_with_the_project_profile_in_an_rgb_project()
    {
        using MagickImage image = Rgb(MagickColors.Orange);

        ColorManagement.PrepareForPdf(image, isCmyk: false, null, Project(ProjectColorSpace.RGB), pdfX: true);

        Assert.Equal(ColorSpace.sRGB, image.ColorSpace);
        Assert.Equal(ColorProfiles.AdobeRGB1998.Description, image.GetColorProfile()!.Description);
    }
}
