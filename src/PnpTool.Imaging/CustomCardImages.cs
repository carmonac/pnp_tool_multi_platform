using ImageMagick;
using PnpTool.Core.CustomDecks;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Primitives;

namespace PnpTool.Imaging;

/// <summary>
/// Builds the final image of a card of a custom or sports deck: its picture over a background,
/// with texts on top. Original: the image methods of <c>CartaPersonalizada</c>.
/// </summary>
public static class CustomCardImages
{
    /// <summary>
    /// Lays the card's picture over its background and writes the result as
    /// <c>&lt;outputFolder&gt;/&lt;Spanish name&gt;.png</c>, which becomes the card's merged image.
    /// The background is brought to the picture's size. A picture or background that does not
    /// exist is replaced by the blank stock image. Original: <c>FusionarImagenes</c>.
    /// </summary>
    /// <param name="card">The card; <see cref="CustomCard.MergedImagePath"/> is set.</param>
    /// <param name="outputFolder">Where the merged image is written.</param>
    /// <param name="decksFolder">
    /// Folder the picture's path is relative to for bundled decks; null when the path is complete.
    /// </param>
    /// <remarks>
    /// The original noted the blank image as the replacement for a missing picture but then
    /// still opened the missing file and failed; the replacement is used here.
    /// </remarks>
    public static void Merge(CustomCard card, string outputFolder, string? decksFolder)
    {
        string blank = AppSettings.ImagesFolder + "blanco.jpg";
        string picturePath = decksFolder == null
            ? card.ImagePath
            : PathHelper.FindExisting(decksFolder.TrimEnd(Path.DirectorySeparatorChar) + PathHelper.FromFile(card.ImagePath));
        if (!File.Exists(picturePath))
        {
            card.ImagePath = blank;
            picturePath = blank;
        }

        string backgroundPath = File.Exists(card.BackgroundImagePath) ? card.BackgroundImagePath : blank;

        using var picture = new MagickImage(picturePath);
        picture.ResetPage();
        using var background = new MagickImage(backgroundPath);
        background.ResetPage();

        // A picture stored in shades of grey (as a PNG frame that is all transparent is) would
        // turn a coloured background grey when the two are combined.
        if (picture.ColorSpace is ColorSpace.Gray or ColorSpace.LinearGray)
        {
            picture.ColorSpace = ColorSpace.sRGB;
            picture.ColorType = picture.HasAlpha ? ColorType.TrueColorAlpha : ColorType.TrueColor;
        }

        using var original = (MagickImage)picture.Clone();

        ImageDensity.RaiseDpi(background, Convert.ToInt32(picture.Density.X), null);
        FitTo(background, picture.Width, picture.Height);

        card.MergedImagePath = Path.Combine(outputFolder, card.NameSpanish + ".png");
        picture.Composite(background, Gravity.Center, 0, 0, CompositeOperator.SrcOver);
        picture.Composite(original, Gravity.Center, 0, 0, CompositeOperator.SrcOver);
        picture.Write(card.MergedImagePath);
    }

    /// <summary>
    /// The same for a sports card, whose picture is the deck's template image.
    /// Original: <c>FusionarImagenesDeportivas</c>.
    /// </summary>
    public static void MergeSports(CustomCard card, string outputFolder, string templateImagePath)
    {
        card.ImagePath = templateImagePath;
        Merge(card, outputFolder, null);
    }

    /// <summary>
    /// Draws a text on the card's merged image at 300 dpi. The position is the top-left corner
    /// of the text as a fraction of the image's width and height. An empty text changes
    /// nothing. Original: <c>AnadirTextosImagenesDeportivas</c>, which took the text and font
    /// from a text box.
    /// </summary>
    public static void AddText(CustomCard card, string? text, FontSpec font, PointD position)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        using MagickImage rendered = TextRenderer.Render(text, font, 300.0);
        using var image = new MagickImage(card.MergedImagePath);
        int x = Convert.ToInt32(position.X * image.Width);
        int y = Convert.ToInt32(position.Y * image.Height);
        image.Composite(rendered, Gravity.Northwest, x, y, CompositeOperator.SrcOver);
        image.Write(card.MergedImagePath);
    }

    /// <summary>
    /// Scales the image to fit inside the size, keeping its proportions. The original asked
    /// for a content-aware ("liquid") rescale; a plain resize is used when the Magick build
    /// has no support for it, and gives the same size.
    /// </summary>
    private static void FitTo(MagickImage image, uint width, uint height)
    {
        var geometry = new MagickGeometry { Width = width, Height = height, IgnoreAspectRatio = false };
        try
        {
            image.LiquidRescale(geometry);
        }
        catch (MagickException)
        {
            image.Resize(geometry);
        }

        if (image.Width > width || image.Height > height)
        {
            image.Resize(geometry);
        }
    }
}
