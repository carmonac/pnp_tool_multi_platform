using ImageMagick;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Projects;

namespace PnpTool.Imaging;

/// <summary>
/// The "pattern" back: one image repeated to cover the whole sheet, so that the backs need not
/// line up with the fronts. Original: <c>ImagenesGeneral.CreaPatronTrasera</c>.
/// </summary>
public static class BackPattern
{
    /// <summary>File the pattern is written to, in the temporary folder. Original name.</summary>
    public const string FileName = "Imagen_Patron_temp.jpg";

    /// <summary>
    /// Builds the pattern for a project from its shared back image and writes it to the
    /// temporary folder. Returns the path of the file.
    /// </summary>
    public static string Create(ProjectSettings settings)
    {
        string path = Path.Combine(AppSettings.TempFolder, FileName);
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        using var tile = new MagickImage(settings.SharedBack.Path);
        tile.ResetPage();
        using MagickImage pattern = Tile(
            tile,
            settings.PrintLayout.Paper.Size.X.Inch / settings.Back.Size.X.Inch,
            settings.PrintLayout.Paper.Size.Y.Inch / settings.Back.Size.Y.Inch);

        // The original saved through GDI without naming a format, which writes PNG data
        // whatever the extension says. Kept: the pattern stays lossless.
        pattern.Write(path, MagickFormat.Png);
        return path;
    }

    /// <summary>
    /// Repeats the tile <paramref name="columns"/> times across and <paramref name="rows"/>
    /// times down; the fractions give a last column and row that are cut off.
    /// </summary>
    /// <remarks>
    /// The original filled that last partial column and row with a slightly stretched copy of
    /// the pattern drawn so far, an accident of how it reused its cropping helper. Here the
    /// tiles simply continue.
    /// </remarks>
    public static MagickImage Tile(MagickImage tile, double columns, double rows)
    {
        uint width = (uint)Convert.ToInt32(Math.Round(tile.Width * columns));
        uint height = (uint)Convert.ToInt32(Math.Round(tile.Height * rows));
        var pattern = new MagickImage(MagickColors.White, Math.Max(width, 1), Math.Max(height, 1))
        {
            Format = tile.Format,
            Density = new Density(tile.Density.X, tile.Density.Y, tile.Density.Units),
        };
        pattern.Texture(tile);
        return pattern;
    }
}
