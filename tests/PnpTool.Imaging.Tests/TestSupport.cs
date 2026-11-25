using ImageMagick;
using PnpTool.Core.Infrastructure;

namespace PnpTool.Imaging.Tests;

/// <summary>Tests that change the program's static settings do not run at the same time.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalState
{
    public const string Name = "Global state";
}

/// <summary>
/// A scratch folder that is also the program's temporary folder while the test runs.
/// Restores the settings it touches and deletes the folder on dispose.
/// </summary>
public sealed class Scratch : IDisposable
{
    private readonly string _tempFolder = AppSettings.TempFolder;
    private readonly string _imagesFolder = AppSettings.ImagesFolder;
    private readonly int _target = AppSettings.TargetDpi;
    private readonly int _minimum = AppSettings.MinimumDpi;
    private readonly int _maximum = AppSettings.MaximumDpi;

    public Scratch()
    {
        Root = Directory.CreateTempSubdirectory("pnptool-imaging-").FullName;
        AppSettings.TempFolder = Path.Combine(Root, "tmp") + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(AppSettings.TempFolder);
        AppSettings.TargetDpi = 300;
        AppSettings.MinimumDpi = 100;
        AppSettings.MaximumDpi = 1000;
    }

    public string Root { get; }

    public string File(params string[] parts) => Path.Combine([Root, .. parts]);

    /// <summary>Writes an image of one colour and returns its path.</summary>
    public string Image(string name, uint width, uint height, int? dpi = 300, IMagickColor<ushort>? color = null)
    {
        using var image = new MagickImage(color ?? MagickColors.SteelBlue, width, height);
        if (dpi != null)
        {
            image.Density = new Density(dpi.Value, DensityUnit.PixelsPerInch);
        }

        string path = File(name);
        image.Write(path);
        return path;
    }

    public void Dispose()
    {
        AppSettings.TempFolder = _tempFolder;
        AppSettings.ImagesFolder = _imagesFolder;
        AppSettings.TargetDpi = _target;
        AppSettings.MinimumDpi = _minimum;
        AppSettings.MaximumDpi = _maximum;
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

internal static class Pixels
{
    public static readonly IMagickColor<ushort>[] Palette =
    [
        MagickColors.Red, MagickColors.Lime, MagickColors.Blue, MagickColors.Yellow,
        MagickColors.Cyan, MagickColors.Magenta, MagickColors.Black, MagickColors.Gray,
        MagickColors.Orange, MagickColors.Purple, MagickColors.Brown, MagickColors.Pink,
        MagickColors.Navy, MagickColors.Olive, MagickColors.Teal, MagickColors.Maroon,
    ];

    /// <summary>An image whose pixel (x, y) has the colour <c>Palette[(y × width + x) mod 16]</c>.</summary>
    public static MagickImage Grid(uint width, uint height, int dpi = 72)
    {
        var image = new MagickImage(MagickColors.White, width, height) { Format = MagickFormat.Png };
        using (IPixelCollection<ushort> pixels = image.GetPixels())
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    IMagickColor<ushort> color = Palette[((y * (int)width) + x) % Palette.Length];
                    pixels.SetPixel(x, y, [color.R, color.G, color.B]);
                }
            }
        }

        image.Density = new Density(dpi, DensityUnit.PixelsPerInch);
        return image;
    }

    /// <summary>The colour at (x, y) as "#RRGGBB".</summary>
    public static string At(MagickImage image, int x, int y)
    {
        using IPixelCollection<ushort> pixels = image.GetPixels();
        IMagickColor<ushort> color = pixels.GetPixel(x, y).ToColor()!;
        return $"#{color.R >> 8:X2}{color.G >> 8:X2}{color.B >> 8:X2}";
    }

    public static string Hex(IMagickColor<ushort> color) => $"#{color.R >> 8:X2}{color.G >> 8:X2}{color.B >> 8:X2}";

    /// <summary>The colour <see cref="Grid"/> puts at (x, y) of an image <paramref name="width"/> wide.</summary>
    public static string GridColor(int x, int y, int width) => Hex(Palette[((y * width) + x) % Palette.Length]);
}
