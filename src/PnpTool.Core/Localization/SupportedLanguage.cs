using System.Globalization;

namespace PnpTool.Core.Localization;

/// <summary>Original: <c>IdiomaSoportado.IdiomasSoportadosEnum</c>.</summary>
public enum LanguageId
{
    German,
    Catalan,
    Chinese,
    Korean,
    Dutch,
    Spanish,
    French,
    EnglishUk,
    EnglishUsa,
    Italian,
    Japanese,
    Portuguese,
    Polish,
    Russian,
    Turkish,
    Vietnamese,
}

/// <summary>
/// A language the program, or the card search, can use. Original: <c>IdiomaSoportado</c>,
/// which also loaded the flag image; here only the name of the image file is kept.
/// </summary>
public sealed class SupportedLanguage
{
    /// <summary>
    /// Version of the program whose texts count as current, as major·10000 + minor·100
    /// (200900 is 20.9). Original: <c>UltimaActualizacionIdiomas</c>.
    /// </summary>
    public const int LatestTextsVersion = 200900;

    /// <summary>Name of the language in that language. Original: <c>NombreIdioma</c>.</summary>
    public string Name { get; }

    /// <summary>Start of the flag file name, such as "en_usa". Original: the argument of <c>DevuelveRutaImagenPorResolucion</c>.</summary>
    public string FlagName { get; }

    /// <summary>
    /// Code stored in the settings file. It is a culture name except for Catalan, which the
    /// original calls "cat". Original: <c>ValorIdioma</c>.
    /// </summary>
    public string Code { get; }

    /// <summary>Original: <c>Traductor</c>.</summary>
    public string Translator { get; }

    /// <summary>Version of the program the translation was last brought up to date with. Original: <c>UltimaVActualizada</c>.</summary>
    public int LastUpdatedVersion { get; }

    public SupportedLanguage(string name, string flagName, string code, string translator, int lastUpdatedVersion)
    {
        Name = name;
        FlagName = flagName;
        Code = code;
        Translator = translator;
        LastUpdatedVersion = lastUpdatedVersion;
    }

    /// <summary>The language the program starts in before any is chosen: Spanish. Original: the parameterless constructor.</summary>
    public SupportedLanguage()
        : this("Español", "es", "es", "Jupklass", 1)
    {
    }

    public SupportedLanguage(LanguageId language)
    {
        (Name, FlagName, Code, Translator, LastUpdatedVersion) = language switch
        {
            LanguageId.German => ("Deutsch", "de", "de", "Micha", 190000),
            LanguageId.Catalan => ("Catalan", "cat", "cat", "Didacqs2", 190000),
            LanguageId.Chinese => ("中文", "zh", "zh", "鸾舆", 200300),
            LanguageId.Korean => ("한국어", "ko", "ko", "Not Traslated", 190000),
            LanguageId.Spanish => ("Español", "es", "es", "Jupklass", 200900),
            LanguageId.EnglishUk => ("English GB", "en", "en-GB", "Stephen Ellis", 200900),
            LanguageId.EnglishUsa => ("English USA", "en_usa", "en-US", "ryan_c_byrd / Goofy thing / Pakoito / Stephen Ellis", 190000),
            LanguageId.Italian => ("Italiano", "it", "it", "Leonardo Russog", 190000),
            LanguageId.French => ("Francais", "fr", "fr", "Desgardel / Griffin Dark / Mister Digdug", 190000),
            LanguageId.Japanese => ("日本語", "ja", "ja", "Not Traslated", 190000),
            LanguageId.Dutch => ("Dutch", "nl", "nl", "Sreukers", 190000),
            LanguageId.Portuguese => ("Português", "pt", "pt", "Rangel Sardinha / Walter Andrade", 200600),
            LanguageId.Polish => ("Polski", "pl", "pl", "Marcin Rys(marc131)", 190000),
            LanguageId.Russian => ("русский", "ru", "ru", "Tikitavy / ChupVulf", 190000),
            LanguageId.Turkish => ("Türkçe", "tr", "tr", "Furkan Halifeoğlu / Yasir Bilgiç", 190000),
            LanguageId.Vietnamese => ("Tiếng Việt", "vi", "vi", "PlayPlus", 190000),
            _ => throw new ArgumentOutOfRangeException(nameof(language)),
        };
    }

    /// <summary>
    /// Name of the .NET culture whose texts and number formats the language uses. The original
    /// passed <see cref="Code"/> straight to <see cref="CultureInfo"/>; "cat" is not the name
    /// of the Catalan culture, whose translation is filed under "ca".
    /// </summary>
    public string CultureName => Code == "cat" ? "ca" : Code;

    public CultureInfo Culture => new(CultureName, useUserOverride: false);

    /// <summary>Whether the translation covers the current texts. Original: <c>IdiomasSoportados.EstaActualizadoIdioma</c>.</summary>
    public bool IsUpToDate => LastUpdatedVersion >= LatestTextsVersion;

    /// <summary>
    /// File name of the flag for a screen of the given width in pixels: "_uhd" from 3840,
    /// "_fhd" from 1920, otherwise "_hd". The files are in the flags folder of the assets.
    /// Original: <c>DevuelveRutaImagenPorResolucion</c>.
    /// </summary>
    public string FlagFileName(double screenWidth) =>
        FlagName + (screenWidth >= 3840.0 ? "_uhd.png" : screenWidth >= 1920.0 ? "_fhd.png" : "_hd.png");

    public override string ToString() => Name;
}
