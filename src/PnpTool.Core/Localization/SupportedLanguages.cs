namespace PnpTool.Core.Localization;

/// <summary>Original: <c>IdiomasSoportados.ListadosIdiomas</c>.</summary>
public enum LanguageList
{
    /// <summary>Languages of the user interface.</summary>
    Program,

    /// <summary>Languages cards can be searched in.</summary>
    CardSearch,
}

/// <summary>An ordered list of languages to choose from. Original: <c>IdiomasSoportados</c>.</summary>
public sealed class SupportedLanguages
{
    private readonly int _englishIndex;

    /// <summary>Original: <c>ListadoIdiomas</c>.</summary>
    public IReadOnlyList<SupportedLanguage> Languages { get; }

    public SupportedLanguages(LanguageList list)
    {
        LanguageId[] ids;
        switch (list)
        {
            case LanguageList.CardSearch:
                _englishIndex = 2;
                ids =
                [
                    LanguageId.German, LanguageId.Spanish, LanguageId.EnglishUk, LanguageId.French, LanguageId.Italian,
                    LanguageId.Russian, LanguageId.Portuguese, LanguageId.Korean, LanguageId.Chinese, LanguageId.Japanese,
                ];
                break;
            default:
                _englishIndex = 4;
                ids =
                [
                    LanguageId.Catalan, LanguageId.German, LanguageId.Dutch, LanguageId.Spanish, LanguageId.EnglishUk,
                    LanguageId.EnglishUsa, LanguageId.French, LanguageId.Italian, LanguageId.Russian, LanguageId.Portuguese,
                    LanguageId.Polish, LanguageId.Vietnamese, LanguageId.Turkish, LanguageId.Chinese,
                ];
                break;
        }

        Languages = [.. ids.Select(id => new SupportedLanguage(id))];
    }

    /// <summary>
    /// Position of the language with the same code, or -1. Original: <c>DevuelveIndiceIdioma</c>,
    /// which ran past the end of the list when the language was not in it.
    /// </summary>
    public int IndexOf(SupportedLanguage language)
    {
        for (int i = 0; i < Languages.Count; i++)
        {
            if (Languages[i].Code == language.Code)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The language with this code, or British English when there is none. Original: <c>DevuelveIdiomaCodigo</c>.</summary>
    public SupportedLanguage FindByCode(string? code) =>
        Languages.FirstOrDefault(language => language.Code == code) ?? Languages[_englishIndex];

    /// <summary>The language with this name, or the last of the list when there is none. Original: <c>DevuelveIdiomaNombre</c>.</summary>
    public SupportedLanguage FindByName(string? name) =>
        Languages.FirstOrDefault(language => language.Name == name) ?? Languages[^1];
}
