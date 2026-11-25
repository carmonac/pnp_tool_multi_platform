using System.Globalization;

namespace PnpTool.Core.Primitives;

/// <summary>
/// A colour as the program stores it: the text "A_R_G_B" with each part from 0 to 255.
/// Original: the colour text helpers of <c>GestionDelColor</c>.
/// </summary>
public readonly record struct ArgbColor(byte A, byte R, byte G, byte B)
{
    public static ArgbColor White => new(255, 255, 255, 255);

    public static ArgbColor Black => new(255, 0, 0, 0);

    public ArgbColor(byte r, byte g, byte b)
        : this(255, r, g, b)
    {
    }

    /// <summary>
    /// Reads a stored colour. The alpha part is ignored and the colour is always opaque;
    /// text that is empty or does not have four parts gives white.
    /// Original: <c>DevuelveStringAIntColorSeparado</c> and <c>DevuelveColor(string)</c>.
    /// </summary>
    public static ArgbColor Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return White;
        }

        string[] parts = text.Split('_');
        if (parts.Length != 4)
        {
            return White;
        }

        return new ArgbColor(
            255,
            Convert.ToByte(parts[1], CultureInfo.InvariantCulture),
            Convert.ToByte(parts[2], CultureInfo.InvariantCulture),
            Convert.ToByte(parts[3], CultureInfo.InvariantCulture));
    }

    /// <summary>The stored form, alpha included. Original: <c>DevuelveStringColor</c>.</summary>
    public override string ToString() => A + "_" + R + "_" + G + "_" + B;

    /// <summary>
    /// Cyan, magenta, yellow and black in percent, by the plain formula without colour
    /// profiles. Pure black gives 0, 0, 0, 100. Original: <c>RgbToCmyk</c>, which divided by
    /// zero for black and stored the resulting not-a-number values as 0.
    /// </summary>
    public (byte C, byte M, byte Y, byte K) ToCmykPercent()
    {
        double r = R / 255.0;
        double g = G / 255.0;
        double b = B / 255.0;
        double k = 1.0 - Math.Max(r, Math.Max(g, b));
        if (k >= 1.0)
        {
            return (0, 0, 0, 100);
        }

        return (
            (byte)Math.Round((1.0 - r - k) / (1.0 - k) * 100.0),
            (byte)Math.Round((1.0 - g - k) / (1.0 - k) * 100.0),
            (byte)Math.Round((1.0 - b - k) / (1.0 - k) * 100.0),
            (byte)Math.Round(k * 100.0));
    }
}
