using ImageMagick;
using PnpTool.Core.Primitives;

namespace PnpTool.Imaging;

/// <summary>Moves colours between the program's stored form and Magick.</summary>
public static class MagickColorExtensions
{
    /// <summary>The colour as an opaque Magick colour.</summary>
    public static MagickColor ToMagick(this ArgbColor color) => MagickColor.FromRgb(color.R, color.G, color.B);

    /// <summary>Original: <c>GestionDelColor.DevuelveColor(MagickColor)</c>, 16 bits per channel reduced to 8.</summary>
    public static ArgbColor ToArgb(this IMagickColor<ushort> color) =>
        new((byte)(color.A / 257), (byte)(color.R / 257), (byte)(color.G / 257), (byte)(color.B / 257));
}
