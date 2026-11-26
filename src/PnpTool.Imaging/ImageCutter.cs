using ImageMagick;
using PnpTool.Core.Infrastructure;

namespace PnpTool.Imaging;

/// <summary>
/// Cuts images into a table of pieces, after taking a border off each side. All measures are
/// in pixels of the image. Original: <c>FormCortarImagenesModelo</c>.
/// </summary>
public sealed class ImageCutter
{
    /// <summary>The extensions the pieces can be written with; the first is the one offered.</summary>
    public static readonly string[] Extensions = [".jpg", ".png", ".bmp", ".tif", ".webp", ".avif"];

    /// <summary>How many pieces across.</summary>
    public int Columns { get; set; } = 1;

    /// <summary>How many pieces down.</summary>
    public int Rows { get; set; } = 2;

    public int BorderLeft { get; set; }

    public int BorderRight { get; set; }

    public int BorderTop { get; set; }

    public int BorderBottom { get; set; }

    /// <summary>Pixels left out between a piece and the next. Original: <c>Separacion</c>.</summary>
    public int SpacingX { get; set; }

    public int SpacingY { get; set; }

    /// <summary>Whether pieces of less than <see cref="AppSettings.SmallFileSizeKb"/> are deleted, as nearly empty.</summary>
    public bool DeleteSmallPieces { get; set; }

    /// <summary>The extension, and so the format, of the pieces.</summary>
    public string Extension { get; set; } = Extensions[0];

    /// <summary>
    /// The place of every piece in an image of the given size, row by row.
    /// Original: the arithmetic of <c>CortaImagen</c> and <c>CreaCorte</c>, kept as it is.
    /// </summary>
    public IReadOnlyList<(int Row, int Column, int X, int Y, int Width, int Height)> Pieces(int imageWidth, int imageHeight)
    {
        int columns = Math.Max(1, Columns);
        int rows = Math.Max(1, Rows);
        int halfX = Convert.ToInt32(SpacingX / 2.0);
        int halfY = Convert.ToInt32(SpacingY / 2.0);
        int stepX = Convert.ToInt32((double)(imageWidth - BorderLeft - BorderRight) / columns);
        int stepY = Convert.ToInt32((double)(imageHeight - BorderTop - BorderBottom) / rows);
        int width = stepX - halfX;
        int height = stepY - halfY;
        var pieces = new List<(int, int, int, int, int, int)>();
        int advanceY = 0;
        for (int row = 0; row < rows; row++)
        {
            int advanceX = 0;
            for (int column = 0; column < columns; column++)
            {
                pieces.Add((row, column, advanceX + BorderLeft + (column * SpacingX), advanceY + BorderTop + (row * SpacingY), width, height));
                advanceX = ((column + 1) * stepX) - ((column + 1) * halfX);
            }

            advanceY = ((row + 1) * stepY) - ((row + 1) * halfY);
        }

        return pieces;
    }

    /// <summary>
    /// Cuts every image and writes the pieces in the folder as
    /// <c>&lt;name&gt;_Y_&lt;row&gt;_X_&lt;column&gt;&lt;extension&gt;</c>. Gives the files written.
    /// Original: <c>CortarImagenes</c>.
    /// </summary>
    /// <param name="progress">Told the share done, from 0 to 100.</param>
    public List<string> Cut(IReadOnlyList<string> images, string folder, Action<double>? progress = null)
    {
        Directory.CreateDirectory(folder);
        var written = new List<string>();
        string[] found = [.. images.Where(File.Exists)];
        double step = 100.0 / Math.Max(1, found.Length * Math.Max(1, Columns) * Math.Max(1, Rows));
        double done = 0.0;
        progress?.Invoke(0.0);
        foreach (string path in found)
        {
            using var image = new MagickImage(path);
            image.ResetPage();
            string name = Path.GetFileNameWithoutExtension(path);
            foreach ((int row, int column, int x, int y, int width, int height) in Pieces((int)image.Width, (int)image.Height))
            {
                done += step;
                if (width > 0 && height > 0)
                {
                    using IMagickImage<ushort> piece = image.Clone();
                    piece.ResetPage();
                    piece.Crop(new MagickGeometry(x, y, (uint)width, (uint)height) { IgnoreAspectRatio = true });
                    piece.ResetPage();
                    string target = Path.Combine(folder, name + "_Y_" + row + "_X_" + column + Extension);
                    piece.Write(target);
                    if (DeleteSmallPieces && new FileInfo(target).Length / 1024 < AppSettings.SmallFileSizeKb)
                    {
                        File.Delete(target);
                    }
                    else
                    {
                        written.Add(target);
                    }
                }

                progress?.Invoke(done);
            }
        }

        return written;
    }
}
