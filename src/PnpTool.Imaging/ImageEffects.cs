using ImageMagick;
using ImageMagick.Drawing;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.Imaging;

/// <summary>
/// The effects that can be applied to card images. Original: <c>ImagenesGeneral.AplicaEfecto</c>
/// and the functions it calls.
/// </summary>
/// <remarks>
/// The original drew its masks with GDI: it painted the shape in a colour one step away from
/// the background and made that colour transparent, which left the anti-aliased edge pixels
/// opaque. The masks here are drawn with Magick to the same result: transparent only where
/// the shape covers a pixel completely.
/// <para>
/// <see cref="ImageEffectType.CreateOutline"/>, the outline for paper miniatures, is not
/// ported and gives an error: nothing in the Windows program reaches paper miniatures.
/// </para>
/// </remarks>
public static class ImageEffects
{
    /// <summary>
    /// Whether the effect depends on the size of the card: the image is brought to the card's
    /// size before a mask is laid over it. Original: <c>ComboboxItemEfectosImagenes.EfectoTieneTamano</c>.
    /// </summary>
    public static bool HasSize(ImageEffectType effect) =>
        effect is ImageEffectType.RoundedCorners or ImageEffectType.HexagonToken or ImageEffectType.CircularToken or ImageEffectType.PentagonToken;

    /// <summary>
    /// Applies an effect to the image and returns the result, which is the same image changed
    /// in place except for the mirror, which builds a new one.
    /// </summary>
    /// <param name="image">The card image.</param>
    /// <param name="effect">The effect.</param>
    /// <param name="size">
    /// What the effect is sized by, in pixels: the corner radius for rounded corners, the
    /// distance from the centre to a corner for hexagon and pentagon, radius and strength of
    /// the blur. Ignored by the others.
    /// </param>
    /// <param name="background">Colour laid over what a mask cuts away.</param>
    /// <param name="settings">The project, for the card size, the resize filter and the colour profile.</param>
    /// <param name="face">Which face the image is, for the mirror.</param>
    public static (AppError Error, MagickImage Image) Apply(
        MagickImage image, ImageEffectType effect, int size, ArgbColor background, ProjectSettings settings, FaceType face)
    {
        var error = new AppError();
        if (image.Density.X <= 0.0 || image.Density.Y <= 0.0)
        {
            error = ImageDensity.RaiseDpi(image, AppSettings.TargetDpi, settings.Grid.CardSize);
        }

        if (error.HasError)
        {
            return (error, image);
        }

        if (HasSize(effect))
        {
            ImageTransforms.ResizeToPrintedSize(image, settings.Grid.CardSize, settings.Other.ResizeFilter);
        }

        switch (effect)
        {
            case ImageEffectType.RoundedCorners:
            case ImageEffectType.CircularToken:
            case ImageEffectType.HexagonToken:
            case ImageEffectType.PentagonToken:
                using (MagickImage mask = CreateMask(effect, image.Width, image.Height, size, background))
                {
                    image.Composite(mask, CompositeOperator.Over);
                }

                return (new AppError(), image);
            case ImageEffectType.Mirror:
                return MirrorEffect.Apply(image, settings, face);
            case ImageEffectType.CreateOutline:
                return (new AppError { Kind = ErrorKind.Error, Message = "The outline effect is not available in this version." }, image);
            case ImageEffectType.Blur:
                image.Blur(size, size);
                return (new AppError(), image);
            case ImageEffectType.Contrast:
                image.Contrast();
                return (new AppError(), image);
            default:
                // As in the original, anything else, "no effect" included, levels the colours.
                image.AutoLevel();
                return (new AppError(), image);
        }
    }

    /// <summary>
    /// An image of the background colour with a transparent hole in the shape of the effect,
    /// to lay over a card image of the same size. Original: <c>AplicaEfectoMascara</c> with
    /// <c>CreaImagenEsquinasRedondeadas</c>, <c>CreaImagenMascaraCircular</c> and
    /// <c>CreaImagenMascaraHexagonal</c>.
    /// </summary>
    public static MagickImage CreateMask(ImageEffectType effect, uint width, uint height, int size, ArgbColor background)
    {
        using var shape = new MagickImage(MagickColors.Black, width, height);
        var drawing = new Drawables().FillColor(MagickColors.White).StrokeColor(MagickColors.Transparent).StrokeWidth(0);
        switch (effect)
        {
            case ImageEffectType.RoundedCorners:
                // The original doubled the size and used it as the diameter of the corner arcs,
                // on a rectangle one pixel beyond the top and left edges.
                if (size > 0)
                {
                    drawing.RoundRectangle(-1, -1, width, height, size, size);
                }
                else
                {
                    drawing.Rectangle(-1, -1, width, height);
                }

                break;
            case ImageEffectType.CircularToken:
                drawing.Ellipse(width / 2.0, height / 2.0, width / 2.0, height / 2.0, 0, 360);
                break;
            case ImageEffectType.HexagonToken:
            case ImageEffectType.PentagonToken:
                drawing.Polygon(
                    PolygonCorners((int)width, (int)height, size, effect == ImageEffectType.PentagonToken ? 5 : 6)
                        .Select(corner => new ImageMagick.PointD(corner.X, corner.Y)));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(effect), effect, "Not an effect that uses a mask.");
        }

        drawing.Draw(shape);

        // Fully covered pixels become the hole; partly covered edge pixels stay background.
        shape.Threshold(new Percentage(99.9));
        shape.Negate();

        var mask = new MagickImage(background.ToMagick(), width, height) { Format = MagickFormat.Png };
        mask.Alpha(AlphaAction.Set);
        mask.Composite(shape, CompositeOperator.CopyAlpha);
        return mask;
    }

    /// <summary>
    /// The corners of a regular polygon around the centre of the image, with the whole-pixel
    /// rounding of the original: a hexagon with flat top and bottom, a pentagon pointing up.
    /// Original: the loop of <c>CreaImagenMascaraHexagonal</c> and <c>RotatePoint</c>.
    /// </summary>
    public static PointD[] PolygonCorners(int width, int height, int radius, int sides)
    {
        double turn = sides == 5 ? 180.0 : 270.0;
        int centreX = width / 2;
        int centreY = height / 2;
        double radians = turn * (Math.PI / 180.0);
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);

        var corners = new PointD[sides];
        for (int i = 0; i < sides; i++)
        {
            double angle = Math.PI * 2.0 * i / sides;
            int x = (int)Math.Round(centreX + (radius * (float)Math.Sin(angle)), MidpointRounding.ToEven);
            int y = (int)Math.Round(centreY + (radius * (float)Math.Cos(angle)), MidpointRounding.ToEven);
            corners[i] = new PointD(
                (int)((cos * (x - centreX)) - (sin * (y - centreY)) + centreX),
                (int)((sin * (x - centreX)) + (cos * (y - centreY)) + centreY));
        }

        return corners;
    }
}
