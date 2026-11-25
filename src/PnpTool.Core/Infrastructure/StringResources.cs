using System.Globalization;
using System.Resources;
using PnpTool.Core.Xml;

namespace PnpTool.Core.Infrastructure;

/// <summary>
/// The translated texts of the program. Original: the <c>Resources</c> class of
/// ClasesGlobalesPNPTool and <c>FuncionesGenerales.DevuelveCadenaRecurso</c>.
/// </summary>
/// <remarks>
/// The texts are in <c>Resources/Strings.resx</c> (English, used when a language lacks a text)
/// and one <c>Strings.&lt;language&gt;.resx</c> per translation, rebuilt from the Windows app by
/// <c>scripts/import-strings.py</c>. Keys are the original ones: much of the user interface
/// looks its texts up by the name of the control, so they cannot be renamed.
/// </remarks>
public static class StringResources
{
    /// <summary>Prefix of the text returned for a key that has no text. Original wording.</summary>
    public const string MissingPrefix = "Need Translation ";

    /// <summary>Access to the resources themselves, for tools and tests.</summary>
    public static ResourceManager Manager { get; } =
        new("PnpTool.Core.Resources.Strings", typeof(StringResources).Assembly);

    /// <summary>
    /// The text for a key in the current language (<see cref="CultureInfo.CurrentCulture"/>),
    /// or "Need Translation " followed by the key when there is none.
    /// </summary>
    public static string Get(string key) => Get(key, CultureInfo.CurrentCulture);

    /// <summary>The text for a key in the given language.</summary>
    public static string Get(string key, CultureInfo culture)
    {
        string? text;
        try
        {
            text = Manager.GetString(key, culture);
        }
        catch (MissingManifestResourceException)
        {
            text = null;
        }

        return string.IsNullOrEmpty(text) ? MissingPrefix + key : text;
    }

    /// <summary>Whether the key has a text in the current language or in English.</summary>
    public static bool Has(string key) => !Get(key).StartsWith(MissingPrefix, StringComparison.Ordinal);

    /// <summary>
    /// The text for one value of an option list. The key is the prefix followed by the name the
    /// value had in the original code, for example <c>ComboBox_ImagenTrasera_SinTrasera</c>.
    /// </summary>
    public static string ForEnum<TEnum>(string keyPrefix, TEnum value)
        where TEnum : struct, Enum =>
        Get(keyPrefix + value.ToOriginalName());
}
