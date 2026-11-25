using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Projects;

namespace PnpTool.Core.Tests.Projects;

[Collection(GlobalState.Name)]
public sealed class StyleCatalogTests : IDisposable
{
    private readonly GlobalStateScope _scope = new();
    private readonly string _user = Directory.CreateTempSubdirectory("pnptool-styles-").FullName;

    public StyleCatalogTests()
    {
        AppPaths.Initialize(AppPaths.FindAssetsFolder(AppContext.BaseDirectory), _user);
        AppPaths.PrepareUserData();
        AppLanguage.Change(new SupportedLanguage(LanguageId.Spanish));
    }

    public void Dispose()
    {
        _scope.Dispose();
        Directory.Delete(_user, recursive: true);
    }

    [Fact]
    public void Groups_are_those_of_the_windows_program_without_the_printing_company()
    {
        IReadOnlyList<StyleGroupOption> groups = StyleCatalog.Groups();

        Assert.Equal(
            [
                SettingsGroup.Cards, SettingsGroup.Books, SettingsGroup.Tokens, SettingsGroup.Custom, SettingsGroup.Tabletop,
                SettingsGroup.CustomStyles, SettingsGroup.Dice, SettingsGroup.Dice7Pixel, SettingsGroup.SpanishDeck, SettingsGroup.AdvancedStyles,
            ],
            groups.Select(group => group.Group));
        Assert.Equal("Cartas", groups[0].Text);
        Assert.Equal("Cartas", groups[0].ToString());
        Assert.All(groups, group => Assert.DoesNotContain(StringResources.MissingPrefix, group.Text));
    }

    [Theory]
    [InlineData(SettingsGroup.Cards, 10)]
    [InlineData(SettingsGroup.Books, 6)]
    [InlineData(SettingsGroup.Tokens, 3)]
    [InlineData(SettingsGroup.Dice, 1)]
    [InlineData(SettingsGroup.SpanishDeck, 3)]
    public void Every_bundled_style_has_its_file(SettingsGroup group, int count)
    {
        IReadOnlyList<StyleOption> styles = StyleCatalog.Styles(group);

        Assert.Equal(count, styles.Count);
        Assert.All(styles, style => Assert.DoesNotContain(StringResources.MissingPrefix, style.Text));
        Assert.All(styles, style => Assert.True(File.Exists(StyleCatalog.PathOf(group, style.FileName)), style.FileName + " is missing"));
    }

    [Fact]
    public void Groups_without_files_and_the_users_own_styles()
    {
        Assert.Empty(StyleCatalog.Styles(SettingsGroup.Custom));
        Assert.Empty(StyleCatalog.Styles(SettingsGroup.Tabletop));
        Assert.Equal(12, StyleCatalog.Styles(SettingsGroup.Dice7Pixel).Count);
        Assert.Equal(string.Empty, StyleCatalog.PathOf(SettingsGroup.Dice7Pixel, string.Empty));

        // The user's folder starts with the style shipped as an example, and lists what is added to it.
        IReadOnlyList<StyleOption> own = StyleCatalog.Styles(SettingsGroup.CustomStyles);
        Assert.Contains(own, style => style.Text == "EstiloAvanzado" && style.FileName == "EstiloAvanzado.pnps");
        File.Copy(StyleCatalog.PathOf(SettingsGroup.Cards, "Cartas_Euro.pnps"), Path.Combine(AppPaths.CustomStylesFolder, "Mio.pnps"));
        Assert.Contains(StyleCatalog.Styles(SettingsGroup.CustomStyles), style => style.Text == "Mio");
        Assert.Equal(Path.Combine(AppPaths.CustomStylesFolder, "Mio.pnps"), StyleCatalog.PathOf(SettingsGroup.CustomStyles, "Mio.pnps"));

        IReadOnlyList<StyleOption> advanced = StyleCatalog.Styles(SettingsGroup.AdvancedStyles);
        Assert.Single(advanced);
        Assert.Equal("1.pnpa", advanced[0].FileName);
        Assert.NotEqual(string.Empty, advanced[0].Text);
        Assert.True(File.Exists(StyleCatalog.PathOf(SettingsGroup.AdvancedStyles, advanced[0].FileName)));
    }
}
