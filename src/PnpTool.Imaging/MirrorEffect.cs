using ImageMagick;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.Imaging;

/// <summary>
/// The "mirror" bleed: the image is surrounded by a frame made of its own edges reflected
/// outwards, so that a slightly misplaced cut does not show white paper.
/// Original: the <c>EfectoEspejo…</c> members of <c>ImagenesGeneral</c>.
/// </summary>
/// <remarks>
/// The variant for book covers (<c>EfectoEspejoImage_Libro</c>), written with GDI in the
/// original, is ported with the book-cover tool.
/// </remarks>
public static class MirrorEffect
{
    /// <summary>
    /// Loads an image and adds a mirrored frame of the given width on every side.
    /// Original: <c>EfectoEspejo_MarcoImagen</c>.
    /// </summary>
    public static (AppError Error, MagickImage Image) AddFrame(string path, Unit frameWidth, FilterType filter, ColorProfile? profile)
    {
        using var image = new MagickImage(path);
        image.ResetPage();
        var original = new PointI(Convert.ToInt32(image.Width), Convert.ToInt32(image.Height));
        return Apply(image, SizeWithFrame(frameWidth, original, image.Density), filter, profile);
    }

    /// <summary>
    /// Enlarges a card image from the size of the face to the size of the card's cell in the
    /// grid, filling the difference with mirrored edges. Original: <c>EfectoEspejoImageMagick_V1_5</c>.
    /// </summary>
    public static (AppError Error, MagickImage Image) Apply(MagickImage image, ProjectSettings settings, FaceType face)
    {
        var original = new PointI(Convert.ToInt32(image.Width), Convert.ToInt32(image.Height));
        return Apply(image, SizeForCell(settings, face, original), settings.Other.ResizeFilter, settings.Other.ColorProfile);
    }

    /// <summary>
    /// The pixel size of the image plus a frame of the given width on each side, at the
    /// image's resolution (<see cref="ImageDensity.Default"/> when it states none).
    /// Original: <c>EscalaAAplicar_PorTamano</c>, which read the density as dots per inch
    /// whatever its unit and added no frame to an image without one.
    /// </summary>
    internal static PointI SizeWithFrame(Unit frameWidth, PointI original, Density density)
    {
        PointU dpi = ImageDensity.Normalize(density);
        return new PointI
        {
            X = original.X + (2 * Convert.ToInt32(frameWidth.Inch * dpi.X)),
            Y = original.Y + (2 * Convert.ToInt32(frameWidth.Inch * dpi.Y)),
        };
    }

    /// <summary>Original: <c>EscalaAAplicar_V2</c>.</summary>
    internal static PointI SizeForCell(ProjectSettings settings, FaceType face, PointI original)
    {
        UnitPoint faceSize = face == FaceType.Back ? settings.Back.Size : settings.Front.Size;
        return new PointI
        {
            X = Convert.ToInt32(original.X * (settings.Grid.CardSize.X.Millimeter / faceSize.X.Millimeter)),
            Y = Convert.ToInt32(original.Y * (settings.Grid.CardSize.Y.Millimeter / faceSize.Y.Millimeter)),
        };
    }

    /// <summary>
    /// Builds an image of <paramref name="newSize"/> pixels with <paramref name="image"/> in the
    /// middle and its edges mirrored around it. When the new size is smaller in both
    /// directions the roles swap: the image is shrunk to the new size and framed back up to
    /// its original size. <paramref name="image"/> is converted to <paramref name="profile"/>
    /// on the way and may be resized. Original: <c>EfectoEspejoImageMagick_CMYK</c>.
    /// </summary>
    public static (AppError Error, MagickImage Image) Apply(MagickImage image, PointI newSize, FilterType filter, ColorProfile? profile)
    {
        var inner = new PointU(image.Width, image.Height);
        var outer = new PointU(Convert.ToUInt32(newSize.X), Convert.ToUInt32(newSize.Y));
        if (inner.X > outer.X && inner.Y > outer.Y)
        {
            (inner, outer) = (outer, inner);
        }

        MagickImage result = CreateCanvas(image, outer, filter);
        PointU offset = PlaceInCentre(result, image, inner, outer, filter, profile);
        MirrorLeft(image, result, offset);
        MirrorRight(image, result, offset);
        MirrorTop(result, offset.Y);
        MirrorBottom(result, offset.Y, image.Height);
        return (new AppError(), result);
    }

    /// <summary>A white image of the outer size with the format, profile, depth and density of the source. Original: <c>…_InicializaImagen</c>.</summary>
    private static MagickImage CreateCanvas(MagickImage image, PointU outer, FilterType filter)
    {
        var canvas = new MagickImage(MagickColors.White, 1u, 1u) { Format = image.Format };
        IColorProfile? profile = image.GetColorProfile();
        if (profile != null)
        {
            canvas.SetProfile(profile);
        }

        canvas.ColorSpace = image.ColorSpace;
        canvas.FilterType = filter;
        canvas.Resize(new MagickGeometry { Width = outer.X, Height = outer.Y, X = 0, Y = 0, IgnoreAspectRatio = true });
        canvas.Depth = image.Depth;
        canvas.Density = new Density(image.Density.X, image.Density.Y, image.Density.Units);
        return canvas;
    }

    /// <summary>Original: <c>…_ColocarEnCentro</c>.</summary>
    private static PointU PlaceInCentre(MagickImage canvas, MagickImage image, PointU inner, PointU outer, FilterType filter, ColorProfile? target)
    {
        image.FilterType = filter;
        image.Resize(new MagickGeometry { Width = inner.X, Height = inner.Y, X = 0, Y = 0, IgnoreAspectRatio = true });

        // The project's profile is unset only while no default profile is configured.
        if (target != null)
        {
            IColorProfile? source = image.GetColorProfile();
            if (source != null)
            {
                image.TransformColorSpace(source, target);
            }
            else
            {
                image.TransformColorSpace(target);
            }
        }

        var offset = new PointU { X = (outer.X - inner.X) / 2, Y = (outer.Y - inner.Y) / 2 };
        canvas.Composite(image, Convert.ToInt32(offset.X), Convert.ToInt32(offset.Y));
        return offset;
    }

    /// <summary>Original: <c>…_LateralIzquierdo</c>.</summary>
    private static void MirrorLeft(MagickImage image, MagickImage canvas, PointU offset)
    {
        if (offset.X == 0)
        {
            return;
        }

        using var strip = (MagickImage)image.Clone();
        strip.Crop(offset.X, image.Height);
        strip.Flop();
        var area = new MagickGeometry { Width = strip.Width, Height = image.Height, X = 0, Y = 0, IgnoreAspectRatio = true };
        canvas.CopyPixels(strip, area, 0, Convert.ToInt32(offset.Y));
    }

    /// <summary>
    /// Original: <c>…_LateralDerecho</c>. When the frame cannot be split evenly the right side
    /// is one pixel wider; the copied area is then also declared one pixel taller, as in the
    /// original.
    /// </summary>
    private static void MirrorRight(MagickImage image, MagickImage canvas, PointU offset)
    {
        using var strip = (MagickImage)image.Clone();
        strip.Flop();
        int extra = image.Width != canvas.Width - (2 * offset.X) ? 1 : 0;
        uint width = Convert.ToUInt32(offset.X + extra);
        if (width == 0)
        {
            return;
        }

        strip.Crop(width, image.Height);
        var area = new MagickGeometry { Width = strip.Width, Height = Convert.ToUInt32(image.Height + extra), X = 0, Y = 0, IgnoreAspectRatio = true };
        canvas.CopyPixels(strip, area, Convert.ToInt32(canvas.Width - strip.Width), Convert.ToInt32(offset.Y));
    }

    /// <summary>Original: <c>…_Superior</c>.</summary>
    private static void MirrorTop(MagickImage canvas, uint offsetY)
    {
        if (offsetY == 0)
        {
            return;
        }

        using var strip = (MagickImage)canvas.Clone();
        strip.Crop(canvas.Width, 2 * offsetY);
        var area = new MagickGeometry { Width = strip.Width, Height = offsetY, X = 0, Y = 0, IgnoreAspectRatio = true };
        strip.Flip();
        canvas.CopyPixels(strip, area, 0, 0);
    }

    /// <summary>Original: <c>…_Inferior</c>.</summary>
    private static void MirrorBottom(MagickImage canvas, uint offsetY, uint imageHeight)
    {
        uint height = offsetY + ((canvas.Height - imageHeight) % 2);
        if (height == 0)
        {
            return;
        }

        using var strip = (MagickImage)canvas.Clone();
        strip.Flip();
        strip.Crop(canvas.Width, Convert.ToUInt32(2 * height));
        var area = new MagickGeometry { Width = strip.Width, Height = height, X = 0, Y = Convert.ToInt32(height), IgnoreAspectRatio = true };
        canvas.CopyPixels(strip, area, 0, Convert.ToInt32(imageHeight + offsetY));
    }
}
