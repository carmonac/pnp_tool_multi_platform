using System.Globalization;
using PnpTool.Core.Infrastructure;

namespace PnpTool.Core.Localization;

/// <summary>
/// The language the program is running in. Original: <c>ConfiguracionPrograma.IdiomaActual</c>
/// and <c>CambiaIdioma</c>.
/// </summary>
public static class AppLanguage
{
    /// <summary>Original: <c>IdiomaActual</c>. Spanish until a language is chosen or loaded.</summary>
    public static SupportedLanguage Current { get; private set; } = new();

    /// <summary>
    /// Switches texts and number formats to the language and records it in the settings.
    /// A null language changes nothing. Original: <c>CambiaIdioma</c>.
    /// </summary>
    /// <remarks>
    /// As in the original the whole culture changes, not only the texts: in Spanish, for
    /// example, numbers are shown and typed with a decimal comma. Files are always written
    /// with a decimal point. Threads started later use the language too, which the original
    /// did not need because its texts were all read on the main thread.
    /// </remarks>
    public static void Change(SupportedLanguage? language)
    {
        if (language == null)
        {
            return;
        }

        Current = language;
        AppSettings.LanguageCode = language.Code;
        CultureInfo culture = language.Culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
    }

    /// <summary>
    /// Switches to the language named in the settings, or to British English when the code is
    /// not one of the program's languages. Original: the end of <c>CopiarAConfiguracionPrograma</c>
    /// followed by <c>CambiaIdioma</c> in <c>AbrirXMLAPPSettings_V2</c>.
    /// </summary>
    public static void ApplySaved() =>
        Change(new SupportedLanguages(LanguageList.Program).FindByCode(AppSettings.LanguageCode));
}
