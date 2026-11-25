using Avalonia.Controls;
using Avalonia.Media;
using PnpTool.Core.CustomDecks;
using PnpTool.Core.Primitives;

namespace PnpTool.Controls;

/// <summary>
/// Between the fonts of the program (<see cref="FontSpec"/>) and those of the user interface.
/// Original: <c>FontHelper</c> and <c>FontExtension</c>.
/// </summary>
public static class FontConversions
{
    public static FontStyle ToAvaloniaStyle(this FontSlant slant) => slant switch
    {
        FontSlant.Italic => FontStyle.Italic,
        FontSlant.Oblique => FontStyle.Oblique,
        _ => FontStyle.Normal,
    };

    public static FontSlant ToSlant(this FontStyle style) => style switch
    {
        FontStyle.Italic => FontSlant.Italic,
        FontStyle.Oblique => FontSlant.Oblique,
        _ => FontSlant.Normal,
    };

    /// <summary>The lines drawn on the text, or null for none.</summary>
    public static TextDecorationCollection? ToAvaloniaDecorations(this TextDecorationType decoration) => decoration switch
    {
        TextDecorationType.OverLine => TextDecorations.Overline,
        TextDecorationType.Strikethrough => TextDecorations.Strikethrough,
        TextDecorationType.Baseline => TextDecorations.Baseline,
        TextDecorationType.Underline => TextDecorations.Underline,
        _ => null,
    };

    /// <summary>Family, slant, weight and width of the font. Weight and width use the same OpenType numbers on both sides.</summary>
    public static Typeface ToTypeface(this FontSpec font) =>
        new(font.FontFamily, font.FontStyle.ToAvaloniaStyle(), (FontWeight)font.FontWeight, (FontStretch)font.FontStretch);

    /// <summary>Shows a text in the font, colour included. Original: <c>FontHelper.SetFont</c>.</summary>
    public static void ApplyTo(this FontSpec font, TextBlock text)
    {
        text.FontFamily = new FontFamily(font.FontFamily);
        text.FontSize = font.FontSize;
        text.FontStyle = font.FontStyle.ToAvaloniaStyle();
        text.FontWeight = (FontWeight)font.FontWeight;
        text.FontStretch = (FontStretch)font.FontStretch;
        text.TextDecorations = font.TextDecorationType.ToAvaloniaDecorations();
        text.Foreground = ArgbColor.Parse(font.Color).ToBrush();
    }

    /// <summary>A separate font with the same values.</summary>
    public static FontSpec Copy(this FontSpec font) => new()
    {
        FontFamily = font.FontFamily,
        FontSize = font.FontSize,
        FontStyle = font.FontStyle,
        FontStretch = font.FontStretch,
        FontWeight = font.FontWeight,
        TextDecorationType = font.TextDecorationType,
        Color = font.Color,
    };
}
