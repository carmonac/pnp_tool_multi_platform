using System.Globalization;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;

namespace PnpTool.Core.Tests.Localization;

[Collection(GlobalState.Name)]
public sealed class LanguageTests : IDisposable
{
    private readonly GlobalStateScope _scope = new();
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;
    private readonly CultureInfo? _defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
    private readonly CultureInfo? _defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
    private readonly SupportedLanguage _language = AppLanguage.Current;

    public void Dispose()
    {
        AppLanguage.Change(_language);
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
        CultureInfo.DefaultThreadCurrentCulture = _defaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _defaultUiCulture;
        _scope.Dispose();
    }

    public static TheoryData<string> ProgramLanguageCodes =>
        [.. new SupportedLanguages(LanguageList.Program).Languages.Select(language => language.Code)];

    private static string FlagsFolder()
    {
        string assets = AppPaths.FindAssetsFolder(AppContext.BaseDirectory);
        return Path.Combine(assets, "images", "flags");
    }

    [Fact]
    public void Program_offers_the_fourteen_languages_of_the_windows_version_in_its_order()
    {
        var languages = new SupportedLanguages(LanguageList.Program);

        Assert.Equal(
            ["cat", "de", "nl", "es", "en-GB", "en-US", "fr", "it", "ru", "pt", "pl", "vi", "tr", "zh"],
            languages.Languages.Select(language => language.Code));
        Assert.Equal(
            ["Catalan", "Deutsch", "Dutch", "Español", "English GB", "English USA", "Francais", "Italiano", "русский", "Português", "Polski", "Tiếng Việt", "Türkçe", "中文"],
            languages.Languages.Select(language => language.Name));
    }

    [Fact]
    public void Card_search_offers_its_ten_languages()
    {
        Assert.Equal(
            ["de", "es", "en-GB", "fr", "it", "ru", "pt", "ko", "zh", "ja"],
            new SupportedLanguages(LanguageList.CardSearch).Languages.Select(language => language.Code));
    }

    [Theory]
    [MemberData(nameof(ProgramLanguageCodes))]
    public void Every_program_language_can_be_selected_and_shows_its_own_texts(string code)
    {
        SupportedLanguage language = new SupportedLanguages(LanguageList.Program).FindByCode(code);

        AppLanguage.Change(language);

        Assert.Equal(code, AppLanguage.Current.Code);
        Assert.Equal(code, AppSettings.LanguageCode);
        Assert.Equal(language.CultureName, CultureInfo.CurrentCulture.Name);
        Assert.Equal(language.CultureName, CultureInfo.CurrentUICulture.Name);
        Assert.Equal(language.CultureName, CultureInfo.DefaultThreadCurrentUICulture!.Name);

        // The translation file of the language is the one in use, not the English fallback.
        var set = StringResources.Manager.GetResourceSet(CultureInfo.CurrentCulture, createIfNotExists: true, tryParents: false);
        Assert.NotNull(set);
        Assert.Equal(set.GetString("Actualizacion_Disponible"), StringResources.Get("Actualizacion_Disponible"));
        if (!code.StartsWith("en", StringComparison.Ordinal))
        {
            Assert.NotEqual(
                StringResources.Get("Actualizacion_Disponible", CultureInfo.InvariantCulture),
                StringResources.Get("Actualizacion_Disponible"));
        }
    }

    [Fact]
    public void Catalan_keeps_its_original_code_but_uses_the_catalan_culture()
    {
        var catalan = new SupportedLanguage(LanguageId.Catalan);

        Assert.Equal("cat", catalan.Code);
        Assert.Equal("ca", catalan.CultureName);
        Assert.Equal("ca", catalan.Culture.Name);
    }

    [Fact]
    public void Texts_are_read_in_the_chosen_language_on_other_threads_too()
    {
        AppLanguage.Change(new SupportedLanguage(LanguageId.Spanish));
        string? fromThread = null;
        var thread = new Thread(() => fromThread = StringResources.Get("FicheroXML_MasModerno"));
        thread.Start();
        thread.Join();

        Assert.StartsWith("El fichero ha sido generado", fromThread);
    }

    [Fact]
    public void Numbers_follow_the_language_but_null_changes_nothing()
    {
        AppLanguage.Change(new SupportedLanguage(LanguageId.Spanish));
        Assert.Equal("1,5", 1.5.ToString());

        AppLanguage.Change(null);
        Assert.Equal("es", AppLanguage.Current.Code);

        AppLanguage.Change(new SupportedLanguage(LanguageId.EnglishUk));
        Assert.Equal("1.5", 1.5.ToString());
    }

    [Fact]
    public void Saved_language_is_applied_and_an_unknown_code_means_british_english()
    {
        AppSettings.LanguageCode = "pt";
        AppLanguage.ApplySaved();
        Assert.Equal("Português", AppLanguage.Current.Name);

        AppSettings.LanguageCode = "fa";
        AppLanguage.ApplySaved();
        Assert.Equal("en-GB", AppLanguage.Current.Code);
        Assert.Equal("en-GB", AppSettings.LanguageCode);
    }

    [Fact]
    public void Languages_are_found_by_code_name_and_position()
    {
        var program = new SupportedLanguages(LanguageList.Program);
        var search = new SupportedLanguages(LanguageList.CardSearch);

        Assert.Equal("Polski", program.FindByCode("pl").Name);
        Assert.Equal("en-GB", program.FindByCode("xx").Code);
        Assert.Equal("en-GB", program.FindByCode(null).Code);
        Assert.Equal("en-GB", search.FindByCode("pl").Code);
        Assert.Equal("tr", program.FindByName("Türkçe").Code);
        Assert.Equal("zh", program.FindByName("Klingon").Code);
        Assert.Equal(3, program.IndexOf(new SupportedLanguage(LanguageId.Spanish)));
        Assert.Equal(1, search.IndexOf(new SupportedLanguage(LanguageId.Spanish)));
        Assert.Equal(-1, program.IndexOf(new SupportedLanguage(LanguageId.Japanese)));
    }

    [Fact]
    public void Only_spanish_and_british_english_are_marked_up_to_date()
    {
        Assert.Equal(
            ["es", "en-GB"],
            new SupportedLanguages(LanguageList.Program).Languages.Where(language => language.IsUpToDate).Select(language => language.Code));
    }

    [Fact]
    public void Starting_language_is_spanish_by_jupklass()
    {
        var language = new SupportedLanguage();

        Assert.Equal("es", language.Code);
        Assert.Equal("Jupklass", language.Translator);
        Assert.False(language.IsUpToDate);
    }

    [Theory]
    [InlineData(1366, "en_usa_hd.png")]
    [InlineData(1920, "en_usa_fhd.png")]
    [InlineData(2560, "en_usa_fhd.png")]
    [InlineData(3840, "en_usa_uhd.png")]
    public void Flag_file_depends_on_the_screen_width(double width, string file)
    {
        Assert.Equal(file, new SupportedLanguage(LanguageId.EnglishUsa).FlagFileName(width));
    }

    /// <summary>Linux tells <c>Cat_fhd.png</c> from <c>cat_fhd.png</c>, so the names must match exactly.</summary>
    [Fact]
    public void Every_language_has_its_flags_under_the_exact_file_name()
    {
        HashSet<string> files = [.. Directory.EnumerateFiles(FlagsFolder()).Select(file => Path.GetFileName(file))];
        IEnumerable<SupportedLanguage> languages = new SupportedLanguages(LanguageList.Program).Languages
            .Concat(new SupportedLanguages(LanguageList.CardSearch).Languages);

        string[] missing =
        [
            .. languages
                .SelectMany(language => new[] { 1280.0, 1920.0, 3840.0 }.Select(language.FlagFileName))
                .Distinct()
                .Where(file => !files.Contains(file))
                .Order(),
        ];

        Assert.True(missing.Length == 0, "No flag file: " + string.Join(", ", missing));
    }
}
