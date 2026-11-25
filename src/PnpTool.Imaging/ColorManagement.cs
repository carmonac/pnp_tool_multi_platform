using System.Text;
using ImageMagick;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Projects;
using PnpTool.Core.Xml;

namespace PnpTool.Imaging;

/// <summary>
/// Conversion of images between RGB and CMYK with ICC profiles, and the checks that keep the
/// images of a project in the project's profile. Original: the colour members of
/// <c>ImagenesGeneral</c>, <c>GestionDelColor.CargaPerfilColor</c> and the profile handling of
/// <c>FicheroPdfMaquetadoNoDependienteLibreria</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>What the original really does.</b> Its two conversion functions first switch the image's
/// colour space, which converts the pixels with plain arithmetic (pure red becomes 100%
/// magenta and yellow, black becomes 100% black ink), and only then ask Magick to transform
/// from the source profile to the target profile. Magick skips a transformation whose source
/// profile is not in the image's current colour space, so that second step does nothing: the
/// profiles never take part, and the image ends up carrying its source profile, an RGB
/// profile on a CMYK image. This program converts through the profiles, as that code
/// evidently intended (<see cref="ColorConversionMode.Icc"/>, the default), so CMYK colours
/// differ from those of the Windows version: black, for one, is built from four inks instead
/// of black ink alone. <see cref="ColorConversionMode.Original"/> reproduces the Windows
/// result and is kept for comparison.
/// </para>
/// The default profiles are the files named by <see cref="AppSettings.RgbIccPath"/> and
/// <see cref="AppSettings.CmykIccPath"/> (sRGB IEC61966-2.1 and Coated FOGRA39 unless the user
/// chose others). The check for the printing company's own CMYK profile
/// (<c>ComprobarPerfilCMYK_Soriano</c>) is not ported.
/// </remarks>
public static class ColorManagement
{
    /// <summary>How <see cref="RgbToCmyk"/> and <see cref="CmykToRgb"/> convert; see the remarks of the class.</summary>
    public static ColorConversionMode Mode { get; set; } = ColorConversionMode.Icc;

    /// <summary>Loads an ICC profile file. Original: <c>CargaPerfilColor</c>.</summary>
    public static ColorProfile LoadProfile(string path) => new(path);

    /// <summary>The program's default RGB profile.</summary>
    public static ColorProfile DefaultRgbProfile() => new(AppSettings.RgbIccPath);

    /// <summary>The program's default CMYK profile.</summary>
    public static ColorProfile DefaultCmykProfile() => new(AppSettings.CmykIccPath);

    /// <summary>
    /// Converts a CMYK image to RGB with the default RGB profile. The colours are read through
    /// the image's own profile, or through the default CMYK profile when it has none. Other
    /// metadata of the image is dropped. Original: <c>ConvertirCMYKaRGB_V2</c>.
    /// </summary>
    public static void CmykToRgb(MagickImage image)
    {
        IColorProfile? original = image.GetColorProfile();
        image.Strip();
        ColorProfile rgb = DefaultRgbProfile();
        IColorProfile source = original ?? DefaultCmykProfile();
        if (Mode == ColorConversionMode.Icc)
        {
            image.SetProfile(source);
            image.TransformColorSpace(source, rgb);
            return;
        }

        image.ColorSpace = ColorSpace.sRGB;
        image.SetProfile(source);
        image.TransformColorSpace(source, rgb);
    }

    /// <summary>
    /// Writes an RGB copy of a CMYK image file to the temporary folder and returns its path.
    /// Original: <c>ConvertirCMYKaRGB</c>.
    /// </summary>
    public static string CmykToRgbFile(string path)
    {
        using var image = new MagickImage(path);
        CmykToRgb(image);
        image.ResetPage();
        string copy = TempImageFiles.UniqueName(path, image.Format.ToString()).Path;
        image.Write(copy);
        return copy;
    }

    /// <summary>
    /// Converts an RGB image to CMYK with the given profile. The colours are read through the
    /// image's own profile, or through the default RGB profile when it has none. Other
    /// metadata of the image is dropped. Original: <c>ConvertirRGBaCMYK_v2</c>.
    /// </summary>
    public static void RgbToCmyk(MagickImage image, IColorProfile cmykProfile)
    {
        IColorProfile? original = image.GetColorProfile();
        image.RemoveProfile("icc");
        image.Strip();
        IColorProfile source = original ?? DefaultRgbProfile();
        if (Mode == ColorConversionMode.Icc)
        {
            image.SetProfile(source);
            image.TransformColorSpace(source, cmykProfile);
            return;
        }

        image.ColorSpace = ColorSpace.CMYK;
        image.SetProfile(source);
        image.TransformColorSpace(source, cmykProfile);
    }

    /// <summary>
    /// Writes a copy of the face's image tagged with the profile to the temporary folder and
    /// makes it the face's image. Original: <c>CambiarPerfilColor</c>.
    /// </summary>
    public static void ChangeProfile(CardFace face, IColorProfile profile)
    {
        using var image = new MagickImage(face.Path);
        image.ResetPage();
        image.SetProfile(profile);
        IColorProfile? current = image.GetColorProfile();
        if (current != null)
        {
            image.TransformColorSpace(current, profile);
        }

        string copy = TempImageFiles.UniqueName(face.Path, image.Format.ToString()).Path;
        face.Path = copy;
        image.Write(copy);
    }

    /// <summary>
    /// Before a PDF that embeds its colour profile (the setting is on, or the PDF is PDF/X),
    /// finds the faces whose image does not carry the project's profile, gives each a copy
    /// that does, and reports them as an error listing card number, face, the profile found
    /// and the file. With <paramref name="doublePanel"/> the backs are checked too. When the
    /// PDF does not embed a profile nothing is checked. Original: <c>DisparidadPerfilesColor</c>.
    /// </summary>
    public static AppError ApplyProjectProfile(Deck deck, IColorProfile profile, bool doublePanel, bool pdfX)
    {
        if (!AppSettings.EmbedColorProfile && !pdfX)
        {
            return new AppError();
        }

        var report = new StringBuilder();
        for (int i = 0; i < deck.Cards.Length; i++)
        {
            Check(deck.Cards[i].Front, i);
            if (doublePanel)
            {
                Check(deck.Cards[i].Back, i);
            }
        }

        var error = new AppError();
        if (report.Length != 0)
        {
            error.Kind = ErrorKind.Error;
            error.Message = StringResources.Get("Distintos_Perfiles") + " " + profile.Description + Environment.NewLine + report;
        }

        return error;

        void Check(CardFace face, int index)
        {
            string? line = MismatchLine(face, profile.Description, index);
            if (line != null)
            {
                report.AppendLine(line);
                ChangeProfile(face, profile);
            }
        }
    }

    /// <summary>
    /// The report line for a face whose profile is missing or has another description, or
    /// null when it matches. Original: <c>DisparidadPerfilesColor_Cara</c>.
    /// The face is named as in the original, "Frontal" or "Trasera", in every language.
    /// </summary>
    private static string? MismatchLine(CardFace face, string? profileName, int index)
    {
        if (face.ColorProfile != null && face.ColorProfile.Description == profileName)
        {
            return null;
        }

        string line = "#" + index + " " + face.FaceType.ToOriginalName() + " ";
        if (face.ColorProfile?.Description != null)
        {
            line += "(" + face.ColorProfile.Description + ") ";
        }

        return line + face.OriginalPath + Environment.NewLine;
    }

    /// <summary>
    /// Brings an image about to be placed in the PDF into the project's colour space.
    /// Original: <c>CargarImagenNuevaAPDFDisparidadPerfiles</c>.
    /// <list type="bullet">
    /// <item>A CMYK image in an RGB project is converted to RGB; in a CMYK project it is left as it is.</item>
    /// <item>An RGB image is touched only when a profile is to be embedded: the PDF is PDF/X,
    /// the image has no profile, or the setting to embed profiles is on. In a CMYK project it
    /// is then converted with the default CMYK profile; in an RGB project it is tagged with
    /// the project's profile.</item>
    /// </list>
    /// </summary>
    /// <param name="image">The image.</param>
    /// <param name="isCmyk">Whether the image is CMYK.</param>
    /// <param name="imageProfile">The profile the image carries, if any.</param>
    /// <param name="settings">The project's "other" settings.</param>
    /// <param name="pdfX">Whether the PDF is PDF/X.</param>
    public static void PrepareForPdf(MagickImage image, bool isCmyk, IColorProfile? imageProfile, OtherSettings settings, bool pdfX)
    {
        if (isCmyk)
        {
            if (settings.ColorSpace == ProjectColorSpace.RGB)
            {
                CmykToRgb(image);
            }

            return;
        }

        bool embed = pdfX || imageProfile == null || AppSettings.EmbedColorProfile;
        if (!embed)
        {
            return;
        }

        if (settings.ColorSpace == ProjectColorSpace.CMYK)
        {
            RgbToCmyk(image, DefaultCmykProfile());
        }
        else
        {
            image.SetProfile(LoadProfile(settings.ColorProfilePath));
        }
    }
}

/// <summary>How images are converted between RGB and CMYK.</summary>
public enum ColorConversionMode
{
    /// <summary>
    /// As PNP Tool for Windows does: by plain arithmetic, without the ICC profiles, leaving
    /// the source profile on the image.
    /// </summary>
    Original,

    /// <summary>Through the ICC profiles, embedding the target profile.</summary>
    Icc,
}
