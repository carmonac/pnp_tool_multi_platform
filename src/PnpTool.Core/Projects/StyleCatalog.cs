using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Templates;

namespace PnpTool.Core.Projects;

/// <summary>A group of styles, as the first list of the settings tab shows it. Original: <c>ComboboxItemGrupoConfiguracion</c>.</summary>
public sealed record StyleGroupOption(string Text, SettingsGroup Group)
{
    public override string ToString() => Text;
}

/// <summary>
/// A style of a group, as the second list shows it. Original: <c>ComboboxItemSubGrupoConfiguracion</c>.
/// </summary>
/// <param name="Text">The name shown.</param>
/// <param name="FileName">The style file, or empty for entries that have none.</param>
/// <param name="ProjectType">The kind of project the style makes.</param>
public sealed record StyleOption(string Text, string FileName, ProjectType ProjectType)
{
    public override string ToString() => Text;
}

/// <summary>
/// The groups of styles and the styles of each, with the files they are stored in.
/// Original: <c>ComboboxItemGrupoConfiguracion.CargarTiposConfiguracion</c>,
/// <c>ComboboxItemSubGrupoConfiguracion.DevuelveListadoSubGrupos</c> and
/// <c>FuncionesGenerales.RutasEstilosDefecto</c>.
/// </summary>
/// <remarks>The group of the printing company and its style are left out.</remarks>
public static class StyleCatalog
{
    /// <summary>The groups, in the order of the original, with their names in the current language.</summary>
    public static IReadOnlyList<StyleGroupOption> Groups() =>
    [
        new(StringResources.Get("ComboBox_Grupo_Cartas"), SettingsGroup.Cards),
        new(StringResources.Get("ComboBox_Grupo_Libros"), SettingsGroup.Books),
        new(StringResources.Get("ComboBox_Grupo_Tokens"), SettingsGroup.Tokens),
        new(StringResources.Get("ComboBox_Grupo_Personalizado"), SettingsGroup.Custom),
        new(StringResources.Get("ComboBox_Grupo_Tabletop"), SettingsGroup.Tabletop),
        new(StringResources.Get("ComboBox_Grupo_Estilos_Personalizados"), SettingsGroup.CustomStyles),
        new(StringResources.Get("ComboBox_Grupo_Dados"), SettingsGroup.Dice),
        new("Dice - Seven Pixels", SettingsGroup.Dice7Pixel),
        new(StringResources.Get("ComboBox_Grupo_BarajaEspanola"), SettingsGroup.SpanishDeck),
        new(StringResources.Get("ShellMenuBarItem_Herramientas_PlantillasAvanzadas_Small"), SettingsGroup.AdvancedStyles),
    ];

    /// <summary>
    /// The styles of a group. The user's own styles and the advanced templates are the files
    /// found in their folders; the rest are fixed lists. A group without styles gives none.
    /// </summary>
    public static IReadOnlyList<StyleOption> Styles(SettingsGroup group) => group switch
    {
        SettingsGroup.Cards =>
        [
            new(StringResources.Get("Carta_estandar"), "Cartas_Standar.pnps", ProjectType.Card),
            new("Euro 59x92", "Cartas_Euro.pnps", ProjectType.Card),
            new(StringResources.Get("Carta_Mini_USA"), "Cartas_MiniUsa.pnps", ProjectType.Card),
            new(StringResources.Get("Carta_Mini_Euro"), "Cartas_MiniEuro.pnps", ProjectType.Card),
            new(StringResources.Get("Carta_Tarot_120"), "Cartas_Tarot.pnps", ProjectType.Card),
            new(StringResources.Get("Carta_Cuadrada_70"), "Cartas_70.pnps", ProjectType.Card),
            new(StringResources.Get("Carta_Cuadrada_80"), "Cartas_80.pnps", ProjectType.Card),
            new(StringResources.Get("Gutterfold"), "Cartas_Gutterfold.pnps", ProjectType.Card),
            new(StringResources.Get("standard_usa"), "Cartas_StandarUsa.pnps", ProjectType.Card),
            new(StringResources.Get("Carta_Baraja_Española"), "Cartas_Espanola.pnps", ProjectType.Card),
        ],
        SettingsGroup.SpanishDeck =>
        [
            new(StringResources.Get("Carta_Baraja_Española"), "Cartas_Espanola.pnps", ProjectType.SpanishDeck),
            new(StringResources.Get("Carta_Baraja_Española_Mascara"), "Cartas_Espanola_Personalizado.pnps", ProjectType.SpanishDeck),
            new(StringResources.Get("Carta_Baraja_poker_Mascara"), "Cartas_Poker_Personalizado.pnps", ProjectType.SpanishDeck),
        ],
        SettingsGroup.Books =>
        [
            new("A4 280x200 1x1", "Papel_A4280.pnps", ProjectType.Book),
            new("A5 200x140 1x1", "Papel_A5200.pnps", ProjectType.Book),
            new(StringResources.Get("Acabado_Libreto_A5_en_A4"), "Papel_LibretoA5.pnps", ProjectType.Booklet),
            new(StringResources.Get("Acabado_Libreto_A4_en_A3"), "Papel_LibretoA4.pnps", ProjectType.Booklet),
            new("Pocketmod", "Papel_Pocketmod.pnps", ProjectType.Pocketmod),
            new("Pocketfold", "Papel_Pocketfold.pnps", ProjectType.Pocketfold),
        ],
        SettingsGroup.Tokens =>
        [
            new("A4 15x15 12x18", "Tokens_15.pnps", ProjectType.Card),
            new("A4 20x20 10x14", "Tokens_20.pnps", ProjectType.Card),
            new("A4 25x25 8x11", "Tokens_25.pnps", ProjectType.Card),
        ],
        SettingsGroup.Dice => [new("D6", "D6.pnps", ProjectType.Card)],
        SettingsGroup.Dice7Pixel =>
        [
            .. new[] { "D2", "D2 Flat", "D3", "D4", "D6", "D8", "D10", "D12", "D16", "D20", "D24", "D30" }
                .Select(name => new StyleOption(name, string.Empty, ProjectType.Dice7Pixel)),
        ],
        SettingsGroup.CustomStyles =>
        [
            .. FilesIn(AppPaths.CustomStylesFolder, "*")
                .Select(file => new StyleOption(Path.GetFileNameWithoutExtension(file), Path.GetFileName(file), ProjectType.Card)),
        ],
        SettingsGroup.AdvancedStyles => [.. FilesIn(AppPaths.AdvancedStylesFolder, "*.pnpa").Select(AdvancedOption)],
        _ => [],
    };

    /// <summary>
    /// The full path of a style of a group, or empty for the groups whose entries have no
    /// file. Original: <c>RutasEstilosDefecto</c>.
    /// </summary>
    public static string PathOf(SettingsGroup group, string fileName) => group switch
    {
        SettingsGroup.Dice7Pixel or SettingsGroup.Tabletop or SettingsGroup.Custom => string.Empty,
        SettingsGroup.CustomStyles => Path.Combine(AppPaths.CustomStylesFolder, fileName),
        SettingsGroup.AdvancedStyles => Path.Combine(AppPaths.AdvancedStylesFolder, fileName),
        _ => Path.Combine(AppPaths.DefaultStylesFolder, fileName),
    };

    private static IEnumerable<string> FilesIn(string folder, string pattern) =>
        Directory.Exists(folder) ? Directory.GetFiles(folder, pattern).Order(StringComparer.OrdinalIgnoreCase) : [];

    /// <summary>An advanced template is listed by the name stored inside it.</summary>
    private static StyleOption AdvancedOption(string file)
    {
        var template = new AdvancedTemplate();
        AppError error = ProjectFileStore.LoadAdvancedTemplate(file, ref template);
        string name = error.HasError || string.IsNullOrEmpty(template.Name) ? Path.GetFileNameWithoutExtension(file) : template.Name;
        return new StyleOption(name, Path.GetFileName(file), ProjectType.Card);
    }
}
