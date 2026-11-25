using ImageMagick;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.Imaging;

/// <summary>Images of a single colour, used to fill card backgrounds and blank faces in the PDF.</summary>
public static class SolidColorImages
{
    /// <summary>
    /// A 100 × 100 image of the stored colour ("A_R_G_B"), tagged with the project's profile
    /// and in its colour space. The PDF stretches it to the area to fill.
    /// Original: <c>ImagenesGeneral.CreaImagenColorSolido</c>.
    /// </summary>
    public static MagickImage Create(string? color, IColorProfile? profile, ProjectColorSpace colorSpace, MagickFormat format)
    {
        MagickColor solid = ArgbColor.Parse(color).ToMagick();
        var image = new MagickImage(solid, 100u, 100u) { Format = format };
        if (profile != null)
        {
            image.SetProfile(profile);
        }

        image.ColorSpace = colorSpace == ProjectColorSpace.CMYK ? ColorSpace.CMYK : ColorSpace.sRGB;
        image.Colorize(solid, new Percentage(100));
        return image;
    }
}
