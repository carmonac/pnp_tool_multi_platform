using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Paper;
using PnpTool.Core.Projects;

namespace PnpTool.Core.Tests.Localization;

/// <summary>
/// Checks the translated texts: every language holds every key, placeholders agree, and the
/// number of texts still in English per language does not grow.
/// </summary>
public sealed partial class StringResourcesTests(ITestOutputHelper output)
{
    /// <summary>Cultures with a translation file. Persian is shipped but not offered, as in the Windows program.</summary>
    private static readonly string[] Translations =
        ["ca", "de", "en-GB", "en-US", "es", "fa", "fr", "it", "nl", "pl", "pt", "ru", "tr", "vi", "zh"];

    private const int KeyCount = 620;

    public static TheoryData<string> TranslationNames => [.. Translations];

    /// <summary>The texts of exactly one language, without falling back to English.</summary>
    private static Dictionary<string, string> Texts(CultureInfo culture)
    {
        ResourceSet? set = StringResources.Manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        Assert.True(set != null, "No texts for " + (culture.Name.Length == 0 ? "the default language" : culture.Name));
        return set.Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
    }

    private static Dictionary<string, string> English => Texts(CultureInfo.InvariantCulture);

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex Placeholder();

    [Fact]
    public void Default_language_holds_every_key_with_a_text()
    {
        Dictionary<string, string> english = English;

        Assert.Equal(KeyCount, english.Count);
        Assert.DoesNotContain(english, text => string.IsNullOrEmpty(text.Value));
        Assert.Equal("Undo", english["QATButton_Undo_Small"]);
    }

    [Theory]
    [MemberData(nameof(TranslationNames))]
    public void Translation_holds_exactly_the_keys_of_the_default_language(string name)
    {
        Dictionary<string, string> english = English;
        Dictionary<string, string> translated = Texts(new CultureInfo(name));

        string[] missing = [.. english.Keys.Except(translated.Keys).Order()];
        string[] unknown = [.. translated.Keys.Except(english.Keys).Order()];
        string[] empty = [.. translated.Where(text => string.IsNullOrEmpty(text.Value)).Select(text => text.Key).Order()];

        Assert.True(missing.Length == 0, name + " lacks: " + string.Join(", ", missing));
        Assert.True(unknown.Length == 0, name + " has unknown keys: " + string.Join(", ", unknown));
        Assert.True(empty.Length == 0, name + " has empty texts: " + string.Join(", ", empty));
    }

    [Theory]
    [MemberData(nameof(TranslationNames))]
    public void Translation_keeps_the_placeholders_of_the_default_language(string name)
    {
        Dictionary<string, string> english = English;
        string[] different =
        [
            .. Texts(new CultureInfo(name))
                .Where(text => !Placeholders(text.Value).SequenceEqual(Placeholders(english[text.Key])))
                .Select(text => text.Key)
                .Order(),
        ];

        Assert.True(different.Length == 0, name + ": " + string.Join(", ", different));

        static string[] Placeholders(string text) => [.. Placeholder().Matches(text).Select(match => match.Value).Order()];
    }

    /// <summary>
    /// Texts a translation has left exactly as in English. Some are legitimately the same
    /// ("PDF", "A4"), so this cannot be zero; the numbers are those of PNP Tool 20.11.1 and
    /// the test fails when a language gets worse. The report lists the gaps per language.
    /// </summary>
    [Theory]
    [InlineData("ca", 153)]
    [InlineData("de", 140)]
    [InlineData("es", 63)]
    [InlineData("fa", 131)]
    [InlineData("fr", 129)]
    [InlineData("it", 188)]
    [InlineData("nl", 187)]
    [InlineData("pl", 194)]
    [InlineData("pt", 79)]
    [InlineData("ru", 167)]
    [InlineData("tr", 99)]
    [InlineData("vi", 114)]
    [InlineData("zh", 78)]
    public void Untranslated_texts_per_language_do_not_grow(string name, int known)
    {
        Dictionary<string, string> english = English;
        string[] untranslated =
        [
            .. Texts(new CultureInfo(name)).Where(text => text.Value == english[text.Key]).Select(text => text.Key).Order(StringComparer.Ordinal),
        ];

        output.WriteLine($"{name}: {untranslated.Length} of {english.Count} texts are the same as in English");
        foreach (string key in untranslated)
        {
            output.WriteLine("  " + key);
        }

        Assert.True(untranslated.Length <= known, $"{name}: {untranslated.Length} untranslated texts, {known} before");
    }

    [Fact]
    public void Text_comes_from_the_language_asked_for()
    {
        Assert.Equal(
            "The file has been generated in a newer version of the program. Please update the software",
            StringResources.Get("FicheroXML_MasModerno", CultureInfo.InvariantCulture));
        Assert.Equal(
            "El fichero ha sido generado en una versión más nueva del programa. Por favor, actualice el software",
            StringResources.Get("FicheroXML_MasModerno", new CultureInfo("es")));
    }

    [Fact]
    public void Regional_variant_uses_the_translation_of_its_language()
    {
        Assert.Equal(
            StringResources.Get("FicheroXML_MasModerno", new CultureInfo("es")),
            StringResources.Get("FicheroXML_MasModerno", new CultureInfo("es-MX")));
    }

    [Fact]
    public void Language_without_translation_falls_back_to_english()
    {
        Assert.Equal("Undo", StringResources.Get("QATButton_Undo_Small", new CultureInfo("ja")));
    }

    [Fact]
    public void Missing_key_gives_the_original_placeholder_text()
    {
        Assert.Equal("Need Translation No_Such_Key", StringResources.Get("No_Such_Key"));
        Assert.False(StringResources.Has("No_Such_Key"));
        Assert.True(StringResources.Has("QATButton_Undo_Small"));
    }

    [Fact]
    public void Keys_with_accents_and_spaces_are_found()
    {
        Assert.True(StringResources.Has("Añadir"));
        Assert.True(StringResources.Has("Carta_Baraja_Española"));
        Assert.True(StringResources.Has("no_hay ficheros_unir_pdf"));
    }

    /// <summary>
    /// Option lists build their keys from a prefix and the original name of each value. Two
    /// values have no text in the Windows program either, which never lists them: the back
    /// mode "none" and the outline effect.
    /// </summary>
    [Fact]
    public void Every_value_of_the_option_lists_has_a_text()
    {
        var missing = new List<string>();
        Check<BackImageMode>("ComboBox_ImagenTrasera_");
        Check<PdfOutputMode>("ComboBox_FicheroPDFSalida_");
        Check<PerforationMarkType>("ComboBox_MarcasPerforacion_");
        Check<CardBackgroundType>("ComboBox_EfectosMarcoExterior_");
        Check<OuterFrameEffect>("ComboBox_EfectosMarcoExterior_");
        Check<UpdateChannel>("ComboBox_Actualizacion_");
        Check<ImageEffectType>("ComboBox_EfectosImagenes_");
        Check<CompressionType>("ComboBox_Comprimir_");
        Check<ImagePathMode>("ComboBox_TiposRutasImagenes_");

        Assert.Equal(["ComboBox_ImagenTrasera_Ninguna", "ComboBox_EfectosImagenes_CrearContorno"], missing);

        void Check<TEnum>(string prefix)
            where TEnum : struct, Enum
        {
            foreach (TEnum value in Enum.GetValues<TEnum>())
            {
                string text = StringResources.ForEnum(prefix, value);
                if (text.StartsWith(StringResources.MissingPrefix, StringComparison.Ordinal))
                {
                    missing.Add(text[StringResources.MissingPrefix.Length..]);
                }
            }
        }
    }

    [Fact]
    public void Paper_size_lists_are_those_of_the_original()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es");
            PaperTypeOption[] sheets = PaperTypeOption.ForSheets(covers: false);

            Assert.Equal(
                [PaperType.A4, PaperType.A5, PaperType.A3, PaperType.A2, PaperType.A1, PaperType.A0, PaperType.Letter, PaperType.Legal, PaperType.Custom],
                sheets.Select(option => option.Value));
            Assert.Equal("A4", sheets[0].ToString());
            Assert.Equal(StringResources.Get("ComboBox_TamanosPapel_Carta"), sheets[6].Text);
            Assert.Equal(StringResources.Get("ComboBox_TamanosPapel_Personalizado"), sheets[8].Text);
            Assert.DoesNotContain(sheets, option => option.Text.StartsWith(StringResources.MissingPrefix, StringComparison.Ordinal));

            PaperTypeOption[] withCovers = PaperTypeOption.ForSheets(covers: true);
            Assert.Equal(10, withCovers.Length);
            Assert.Equal(PaperType.SorianoCovers, withCovers[^1].Value);
            Assert.Equal("500x330", withCovers[^1].Text);

            Assert.Equal(["330x480", "320x450", "225x320", "A4"], PaperTypeOption.ForTickets().Select(option => option.Text));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}

public class OriginalEnumNameTests
{
    [Fact]
    public void Original_code_name_differs_from_the_file_name_only_where_marked()
    {
        Assert.Equal("Sin_trasera", PnpTool.Core.Xml.XmlEnumNames.ToFileName(BackImageMode.NoBack));
        Assert.Equal("SinTrasera", PnpTool.Core.Xml.XmlEnumNames.ToOriginalName(BackImageMode.NoBack));
        Assert.Equal("TraseraIgual", PnpTool.Core.Xml.XmlEnumNames.ToOriginalName(BackImageMode.SameBack));
        Assert.Equal("TraseraDiferente", PnpTool.Core.Xml.XmlEnumNames.ToOriginalName(BackImageMode.DifferentBack));
        Assert.Equal("Patron", PnpTool.Core.Xml.XmlEnumNames.ToOriginalName(BackImageMode.Pattern));
        Assert.Equal("ImagenAtamano", PnpTool.Core.Xml.XmlEnumNames.ToFileName(CompressionType.ImageToSize));
        Assert.Equal("ImagenATamano", PnpTool.Core.Xml.XmlEnumNames.ToOriginalName(CompressionType.ImageToSize));
        Assert.Equal("A4", PnpTool.Core.Xml.XmlEnumNames.ToOriginalName(PaperType.A4));
    }
}
