using Avalonia.Media;
using PnpTool.Core.CustomDecks;

namespace PnpTool.Controls;

/// <summary>
/// One variant of a font family, such as "Bold Italic". Original: WPF's <c>FamilyTypeface</c>
/// as listed by the font picker.
/// </summary>
/// <param name="Style">Slant.</param>
/// <param name="Weight">OpenType weight, 400 is regular.</param>
/// <param name="Stretch">OpenType width class, 5 is normal.</param>
public sealed record FontFace(FontSlant Style, int Weight, int Stretch)
{
    public static FontFace Regular { get; } = new(FontSlant.Normal, 400, 5);

    /// <summary>The name shown, in English like the original's: width, weight and slant, or "Regular".</summary>
    public string Name
    {
        get
        {
            var parts = new List<string>();
            if (Stretch != 5)
            {
                parts.Add(StretchName(Stretch));
            }

            if (Weight != 400)
            {
                parts.Add(WeightName(Weight));
            }

            if (Style != FontSlant.Normal)
            {
                parts.Add(Style.ToString());
            }

            return parts.Count == 0 ? "Regular" : string.Join(" ", parts);
        }
    }

    public override string ToString() => Name;

    /// <summary>The variants installed for a family, lightest first; just the regular one when the family is not installed.</summary>
    public static IReadOnlyList<FontFace> Of(string family)
    {
        if (!FontManager.Current.SystemFonts.TryGetFamilyTypefaces(family, out IReadOnlyList<Typeface>? typefaces)
            || typefaces.Count == 0)
        {
            return [Regular];
        }

        return
        [
            .. typefaces
                .Select(typeface => new FontFace(typeface.Style.ToSlant(), (int)typeface.Weight, (int)typeface.Stretch))
                .Distinct()
                .OrderBy(face => face.Stretch == 5 ? 0 : 1)
                .ThenBy(face => face.Stretch)
                .ThenBy(face => face.Weight)
                .ThenBy(face => face.Style),
        ];
    }

    /// <summary>The variant of a font.</summary>
    public static FontFace Of(FontSpec font) => new(font.FontStyle, font.FontWeight, font.FontStretch);

    private static string WeightName(int weight) => weight switch
    {
        100 => "Thin",
        200 => "Extra Light",
        300 => "Light",
        500 => "Medium",
        600 => "Semi Bold",
        700 => "Bold",
        800 => "Extra Bold",
        900 => "Black",
        950 => "Extra Black",
        _ => weight.ToString(),
    };

    private static string StretchName(int stretch) => stretch switch
    {
        1 => "Ultra Condensed",
        2 => "Extra Condensed",
        3 => "Condensed",
        4 => "Semi Condensed",
        6 => "Semi Expanded",
        7 => "Expanded",
        8 => "Extra Expanded",
        9 => "Ultra Expanded",
        _ => "Width " + stretch,
    };
}
