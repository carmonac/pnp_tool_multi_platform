using PnpTool.Core.CustomDecks;
using PnpTool.Core.Infrastructure;
using SkiaSharp;

namespace PnpTool.Imaging;

/// <summary>
/// Finds the typeface for a font description: first among the fonts shipped with the program
/// (<see cref="AppPaths.FontsFolder"/>), then among those installed on the computer.
/// </summary>
/// <remarks>
/// The original relied on WPF, which only knows installed fonts, plus one bundled font file
/// opened by path for the PDF. Looking in the bundled folder first makes texts come out the
/// same on computers that lack a font the program ships.
/// </remarks>
public static class FontCatalog
{
    private static readonly Lock Gate = new();
    private static string? _loadedFolder;
    private static List<SKTypeface> _bundled = [];

    /// <summary>Family names of the fonts shipped with the program.</summary>
    public static IReadOnlyList<string> BundledFamilies
    {
        get
        {
            lock (Gate)
            {
                return [.. Bundled().Select(typeface => typeface.FamilyName).Distinct(StringComparer.OrdinalIgnoreCase).Order()];
            }
        }
    }

    /// <summary>Family names of the fonts installed on the computer.</summary>
    public static IReadOnlyList<string> InstalledFamilies => [.. SKFontManager.Default.FontFamilies.Order()];

    /// <summary>Whether a font of that family is shipped or installed.</summary>
    public static bool Has(string family)
    {
        lock (Gate)
        {
            if (Bundled().Any(typeface => SameFamily(typeface, family)))
            {
                return true;
            }
        }

        using SKTypeface? installed = SKFontManager.Default.MatchFamily(family);
        return installed != null;
    }

    /// <summary>
    /// The typeface closest to the description. A family that is neither shipped nor installed
    /// gives the system's default typeface, as WPF fell back to its own.
    /// </summary>
    public static SKTypeface Resolve(FontSpec font)
    {
        SKFontStyleSlant slant = font.FontStyle switch
        {
            FontSlant.Italic => SKFontStyleSlant.Italic,
            FontSlant.Oblique => SKFontStyleSlant.Oblique,
            _ => SKFontStyleSlant.Upright,
        };

        lock (Gate)
        {
            SKTypeface? best = Bundled()
                .Where(typeface => SameFamily(typeface, font.FontFamily))
                .OrderBy(typeface => Distance(typeface, font.FontWeight, font.FontStretch, slant))
                .FirstOrDefault();
            if (best != null)
            {
                return best;
            }
        }

        return SKTypeface.FromFamilyName(font.FontFamily, font.FontWeight, font.FontStretch, slant) ?? SKTypeface.Default;
    }

    /// <summary>Forgets the bundled fonts read so far, so that the folder is read again.</summary>
    public static void Reload()
    {
        lock (Gate)
        {
            _loadedFolder = null;
        }
    }

    private static bool SameFamily(SKTypeface typeface, string family) =>
        string.Equals(typeface.FamilyName, family, StringComparison.OrdinalIgnoreCase);

    /// <summary>How far a typeface is from the style asked for; slant counts most, then weight.</summary>
    private static int Distance(SKTypeface typeface, int weight, int width, SKFontStyleSlant slant) =>
        ((typeface.FontSlant == slant) ? 0 : 10000)
        + Math.Abs(typeface.FontWeight - weight)
        + (Math.Abs(typeface.FontWidth - width) * 100);

    /// <summary>The typefaces of the bundled folder, read on first use. Call with <see cref="Gate"/> held.</summary>
    private static List<SKTypeface> Bundled()
    {
        string folder = AppPaths.FontsFolder;
        if (_loadedFolder == folder)
        {
            return _bundled;
        }

        var loaded = new List<SKTypeface>();
        if (Directory.Exists(folder))
        {
            foreach (string file in Directory.EnumerateFiles(folder).Order(StringComparer.Ordinal))
            {
                if (Path.GetExtension(file).ToLowerInvariant() is ".ttf" or ".otf" or ".ttc")
                {
                    SKTypeface? typeface = SKTypeface.FromFile(file);
                    if (typeface != null)
                    {
                        loaded.Add(typeface);
                    }
                }
            }
        }

        _bundled = loaded;
        _loadedFolder = folder;
        return _bundled;
    }
}
