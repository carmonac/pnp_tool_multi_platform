using PnpTool.Core.Infrastructure;

namespace PnpTool.Core.CustomDecks;

/// <summary>Original: <c>TextDecorationType</c>.</summary>
public enum TextDecorationType
{
    None,
    OverLine,
    Strikethrough,
    Baseline,
    Underline,
}

/// <summary>Slant of a font; names as WPF writes them.</summary>
public enum FontSlant
{
    Normal,
    Italic,
    Oblique,
}

/// <summary>
/// Font of a text on a sports card. Original: <c>Tipos.Fuente.Font</c>, which wrapped WPF
/// font objects; here the same information is held as plain values.
/// </summary>
public class FontSpec
{
    public string FontFamily { get; set; } = "Arial";

    public double FontSize { get; set; } = 12.0;

    public FontSlant FontStyle { get; set; } = FontSlant.Normal;

    /// <summary>OpenType width class, 1 (ultra-condensed) to 9 (ultra-expanded); 5 is normal.</summary>
    public int FontStretch { get; set; } = 5;

    /// <summary>OpenType weight, 1–999; 400 is normal and 700 bold.</summary>
    public int FontWeight { get; set; } = 400;

    public TextDecorationType TextDecorationType { get; set; } = TextDecorationType.None;

    /// <summary>Colour as "A_R_G_B". Original: <c>ColorFuente</c>.</summary>
    public string Color { get; set; } = AppSettings.ColorBlack;

    /// <summary>
    /// Text form stored in sports-deck files:
    /// "family:size:style:stretch:weight:decoration:colour". Original: <c>FontHelper.ToStringSerialize</c>.
    /// The size is written with the current culture, as the original does.
    /// </summary>
    public string ToSerializedString() =>
        FontFamily + ":" + FontSize + ":" + FontStyle + ":" + StretchName(FontStretch) + ":" + WeightName(FontWeight) + ":" + TextDecorationType + ":" + Color;

    /// <summary>
    /// Original: <c>FontHelper.FromStringSerialize</c>, quirks included: weight "Normal" is not
    /// recognised and reads as Medium, and an unknown or "None" decoration reads as Baseline.
    /// </summary>
    public static FontSpec FromSerializedString(string value)
    {
        string[] parts = value.Split(':');
        return new FontSpec
        {
            FontFamily = parts[0],
            FontSize = Convert.ToDouble(parts[1]),
            FontStyle = parts[2] switch
            {
                "Italic" => FontSlant.Italic,
                "Oblique" => FontSlant.Oblique,
                _ => FontSlant.Normal,
            },
            FontStretch = parts[3] switch
            {
                "UltraCondensed" => 1,
                "ExtraCondensed" => 2,
                "Condensed" => 3,
                "SemiCondensed" => 4,
                "Medium" => 5,
                "SemiExpanded" => 6,
                "Expanded" => 7,
                "ExtraExpanded" => 8,
                "UltraExpanded" => 9,
                _ => 5,
            },
            FontWeight = parts[4] switch
            {
                "Thin" => 100,
                "ExtraLight" or "UltraLight" => 200,
                "Light" => 300,
                "Regular" => 400,
                "Medium" => 500,
                "DemiBold" or "SemiBold" => 600,
                "Bold" => 700,
                "ExtraBold" or "UltraBold" => 800,
                "Black" or "Heavy" => 900,
                "ExtraBlack" or "UltraBlack" => 950,
                _ => 500,
            },
            TextDecorationType = parts[5] switch
            {
                "OverLine" => TextDecorationType.OverLine,
                "Strikethrough" => TextDecorationType.Strikethrough,
                "Underline" => TextDecorationType.Underline,
                _ => TextDecorationType.Baseline,
            },
            Color = parts[6],
        };
    }

    // Names as WPF's FontStretch.ToString() and FontWeight.ToString() give them.
    private static string StretchName(int stretch) => stretch switch
    {
        1 => "UltraCondensed",
        2 => "ExtraCondensed",
        3 => "Condensed",
        4 => "SemiCondensed",
        5 => "Normal",
        6 => "SemiExpanded",
        7 => "Expanded",
        8 => "ExtraExpanded",
        9 => "UltraExpanded",
        _ => stretch.ToString(),
    };

    private static string WeightName(int weight) => weight switch
    {
        100 => "Thin",
        200 => "ExtraLight",
        300 => "Light",
        400 => "Normal",
        500 => "Medium",
        600 => "SemiBold",
        700 => "Bold",
        800 => "ExtraBold",
        900 => "Black",
        950 => "ExtraBlack",
        _ => weight.ToString(),
    };
}
