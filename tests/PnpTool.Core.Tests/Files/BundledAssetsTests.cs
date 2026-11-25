using System.Globalization;
using System.Text;
using PnpTool.Core.CustomDecks;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Projects;
using PnpTool.Core.Templates;

namespace PnpTool.Core.Tests.Files;

/// <summary>
/// Loads and saves again every style, template, deck and ticket shipped in <c>assets/</c> and
/// checks that nothing is lost or changed on the way, comparing the XML value by value.
/// </summary>
/// <remarks>
/// The bundled files were written by several older versions of PNP Tool for Windows, so they
/// hold a few elements that version 20.11 no longer reads, and lack a few it has added since.
/// Each test names those elements; anything else that differs fails the test.
/// </remarks>
public class BundledAssetsTests
{
    private static readonly string AssetsRoot = FindAssets();

    /// <summary>
    /// A length used to be written in every unit; 20.11 writes and reads points only.
    /// <see cref="Lengths_in_other_units_agree_with_the_points_kept"/> checks the dropped values.
    /// </summary>
    private static readonly string[] DerivedUnits = ["/Inch", "/Millimeter", "/Centimeter", "/Presentation"];

    private static string FindAssets()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "PnpTool.sln")))
            {
                return Path.Combine(folder.FullName, "assets");
            }
        }

        throw new DirectoryNotFoundException("assets/ not found above " + AppContext.BaseDirectory);
    }

    private static string Asset(string relative) => Path.Combine(AssetsRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Bundled files with one of the extensions, as paths relative to assets/ with forward slashes.</summary>
    private static List<string> BundledFiles(params string[] extensions) =>
    [
        .. Directory.EnumerateFiles(AssetsRoot, "*", SearchOption.AllDirectories)
            .Where(file => extensions.Contains(Path.GetExtension(file)))
            .Select(file => Path.GetRelativePath(AssetsRoot, file).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal),
    ];

    private static TheoryData<string> Bundled(params string[] extensions) => [.. BundledFiles(extensions)];

    public static TheoryData<string> Styles => Bundled(".pnps");

    public static TheoryData<string> Templates => Bundled(".pnpa");

    public static TheoryData<string> Decks => Bundled(".pnpm");

    public static TheoryData<string> Tickets => Bundled(".pnpt");

    public static TheoryData<string> AllProjectFiles => Bundled(".pnps", ".pnpa", ".pnpm", ".pnpt");

    /// <summary>
    /// Fails unless the saved file holds exactly the values of the original, apart from
    /// elements of older versions (<paramref name="obsolete"/>, only in the original) and
    /// elements added since (<paramref name="newer"/>, only in the saved file). The version
    /// number is not compared: every file is saved with the current one.
    /// </summary>
    private static void AssertSame(string original, string saved, string[] obsolete, string[] newer, string? notCompared = null)
    {
        List<string> differences = XmlComparison.Differences(
            original,
            saved,
            path => path.EndsWith("/Version", StringComparison.Ordinal) || (notCompared != null && path.EndsWith(notCompared, StringComparison.Ordinal)));
        differences.RemoveAll(line =>
            (line.StartsWith("lost", StringComparison.Ordinal) && (Mentions(line, obsolete) || Mentions(line, DerivedUnits)))
            || (line.StartsWith("added", StringComparison.Ordinal) && Mentions(line, newer)));

        Assert.True(differences.Count == 0, differences.Count + " differences:\n" + string.Join("\n", differences.Take(40)));
    }

    /// <summary>True when the element path of a difference line ends with one of the endings.</summary>
    private static bool Mentions(string line, string[] endings)
    {
        string path = XmlComparison.PathOf(line);
        return endings.Any(ending => path.EndsWith(ending, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_kind_of_file_is_bundled()
    {
        Assert.Equal(24, BundledFiles(".pnps").Count);
        Assert.Single(BundledFiles(".pnpa"));
        Assert.Equal(3, BundledFiles(".pnpm").Count);
        Assert.Single(BundledFiles(".pnpt"));
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public void Style_round_trips(string file)
    {
        using var temp = new TestFiles.TempFolder();
        var settings = new ProjectSettings();
        (AppError error, bool resave) = ProjectFileStore.LoadStyle(Asset(file), ref settings);
        Assert.False(error.HasError, error.Message);
        Assert.False(resave);

        string saved = temp.File("saved.pnps");
        ProjectFileStore.SaveStyle(saved, settings);

        // One bundled style names an ICC profile on its author's disk. A profile that cannot be
        // found is replaced by the default one, as in the original (none is configured in these
        // tests), so that path is not expected back.
        string storedProfile = XmlComparison.Leaves(Asset(file)).Single(leaf => leaf.Key.EndsWith("/sPefilDeColor", StringComparison.Ordinal)).Value;
        bool profileReplaced = storedProfile.Length > 0 && !File.Exists(storedProfile);
        if (profileReplaced)
        {
            Assert.Equal(string.Empty, settings.Other.ColorProfilePath);
        }

        AssertSame(
            Asset(file),
            saved,
            obsolete:
            [
                "/ParametrosDeEntradaProyecto/NombreProyecto",
                "/OtrosParametrosProyecto/ColorspaceProyecto", // renamed to Colorspace; RGB in every bundled style
                "/MaquetarApilado/MaquetarApilado",
                "/TamanoTotalImagenesEnDisco",
            ],
            newer:
            [
                "/ParametrosDeEntradaProyecto/NombreProyectoInput",
                "/ParametrosDeEntradaProyecto/NombreProyectoOutput",
                "/ParametrosDeEntradaProyecto/ImagenTrasera",
                "/ParametrosDeEntradaProyecto/FicheroEstilo",
                "/OtrosParametrosProyecto/Colorspace",
                "/OtrosParametrosProyecto/porcentajecompresion",
                "/OtrosParametrosProyecto/FiltroResizeMagick",
                "/OtrosParametrosProyecto/MaquetarApilado/TipoMaquetacion",
                "/OtrosParametrosProyecto/MaquetarApilado/TamanoBloque",
                "/Configuracionlibro/ImprimirBorde",
            ],
            notCompared: profileReplaced ? "/OtrosParametrosProyecto/sPefilDeColor" : null);
    }

    /// <summary>
    /// The colour space moved from <c>ColorspaceProyecto</c> to <c>Colorspace</c>, and 20.11 does
    /// not read the old element. That loses nothing only while every bundled style is RGB,
    /// the default.
    /// </summary>
    [Theory]
    [MemberData(nameof(Styles))]
    public void Style_uses_the_default_colour_space(string file)
    {
        foreach ((string path, string value) in XmlComparison.Leaves(Asset(file)))
        {
            if (path.EndsWith("/ColorspaceProyecto", StringComparison.Ordinal) || path.EndsWith("/Colorspace", StringComparison.Ordinal))
            {
                Assert.Equal("RGB", value);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void Advanced_template_round_trips(string file)
    {
        using var temp = new TestFiles.TempFolder();
        var template = new AdvancedTemplate();
        AppError error = ProjectFileStore.LoadAdvancedTemplate(Asset(file), ref template);
        Assert.False(error.HasError, error.Message);
        Assert.NotEmpty(template.Cards);

        // Templates are always saved into the advanced-styles folder under their own file name.
        string previousFolder = AppSettings.AdvancedStylesFolder;
        try
        {
            AppSettings.AdvancedStylesFolder = temp.Root + Path.DirectorySeparatorChar;
            ProjectFileStore.SaveAdvancedTemplate(template);
        }
        finally
        {
            AppSettings.AdvancedStylesFolder = previousFolder;
        }

        AssertSame(
            Asset(file),
            temp.File(template.FileName),
            obsolete: ["/PlantillaAvanzada/TamanoIgualListadoCartas"], // version 1 of the format
            newer: []);
    }

    [Theory]
    [MemberData(nameof(Decks))]
    public void Custom_deck_round_trips(string file)
    {
        using var temp = new TestFiles.TempFolder();
        var deck = new CustomDeck();
        AppError error = ProjectFileStore.LoadCustomDeck(Asset(file), ref deck);
        Assert.False(error.HasError, error.Message);
        Assert.NotEmpty(deck.Cards);

        string saved = temp.File("saved.pnpm");
        ProjectFileStore.SaveCustomDeck(saved, deck);

        AssertSame(Asset(file), saved, obsolete: [], newer: []);
    }

    /// <remarks>
    /// The bundled ticket stores pixel positions as <c>PosicionPixelesX</c>/<c>PosicionPixelesY</c>.
    /// Version 20.11 reads and writes them as <c>PosicionPixeles/X</c> and <c>/Y</c> instead, so
    /// it loads them as zero, and so does this port. The positions in points are kept.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Tickets))]
    public void Ticket_round_trips(string file)
    {
        using var temp = new TestFiles.TempFolder();
        var ticket = new TicketProjectSettings();
        AppError error = ProjectFileStore.LoadTicket(Asset(file), ref ticket);
        Assert.False(error.HasError, error.Message);

        string saved = temp.File("saved.pnpt");
        ProjectFileStore.SaveTicket(saved, ticket);

        AssertSame(
            Asset(file),
            saved,
            obsolete: ["/PosicionPixelesX", "/PosicionPixelesY"],
            newer: ["/PosicionPixeles/X", "/PosicionPixeles/Y"]);
    }

    /// <summary>
    /// Older files repeat each length in inches, millimetres, centimetres and screen units.
    /// Only the points are read, so the other values must say the same thing.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllProjectFiles))]
    public void Lengths_in_other_units_agree_with_the_points_kept(string file)
    {
        Dictionary<string, string> leaves = XmlComparison.Leaves(Asset(file));
        (string Unit, double PerPoint)[] units =
        [
            ("Inch", 1 / 72.0),
            ("Millimeter", 25.4 / 72.0),
            ("Centimeter", 2.54 / 72.0),
            ("Presentation", 96 / 72.0),
        ];

        foreach ((string path, string value) in leaves)
        {
            if (!path.EndsWith("/Point", StringComparison.Ordinal))
            {
                continue;
            }

            double points = double.Parse(value, CultureInfo.InvariantCulture);
            string parent = path[..^"Point".Length];
            foreach ((string unit, double perPoint) in units)
            {
                if (leaves.TryGetValue(parent + unit, out string? other))
                {
                    Assert.Equal(points * perPoint, double.Parse(other, CultureInfo.InvariantCulture), 9);
                }
            }
        }
    }

    /// <summary>
    /// Linux file systems tell <c>Uno.pnps</c> from <c>uno.pnps</c>; Windows and (usually) macOS
    /// do not. Everything a bundled deck points to must exist under exactly that name.
    /// </summary>
    [Theory]
    [MemberData(nameof(Decks))]
    public void Custom_deck_points_to_files_that_exist_with_the_same_letter_case(string file)
    {
        var deck = new CustomDeck();
        ProjectFileStore.LoadCustomDeck(Asset(file), ref deck);
        string deckFolder = Path.GetDirectoryName(Asset(file))!;

        var missing = new List<string>();
        if (!ExistsWithExactCase(Asset("styles/default"), deck.Style))
        {
            missing.Add("style " + deck.Style);
        }

        foreach (CustomCard card in deck.Cards)
        {
            foreach (string image in new[] { card.ImagePath, card.BackgroundImagePath })
            {
                if (image.Length > 0 && !ExistsWithExactCase(deckFolder, image))
                {
                    missing.Add(image);
                }
            }
        }

        Assert.True(missing.Count == 0, "Not found: " + string.Join(", ", missing.Distinct()));
    }

    /// <summary>
    /// Whether <paramref name="relative"/> (as stored in a file, with backslashes) names a file
    /// under <paramref name="root"/>, matching the letter case of every folder and of the file.
    /// </summary>
    private static bool ExistsWithExactCase(string root, string relative)
    {
        string current = root;
        string[] parts = relative.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            if (!Directory.Exists(current)
                || !Directory.EnumerateFileSystemEntries(current).Any(entry => Path.GetFileName(entry) == part))
            {
                return false;
            }

            current = Path.Combine(current, part);
        }

        return parts.Length > 0 && File.Exists(current);
    }

    /// <summary>What must not be redistributed, or is not needed, stays out of assets/.</summary>
    [Fact]
    public void Assets_hold_no_printing_company_branding_fonts_or_working_files()
    {
        string[] unwanted =
        [
            .. Directory.EnumerateFiles(AssetsRoot, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(AssetsRoot, path))
                .Where(path => path.Contains("soriano", StringComparison.OrdinalIgnoreCase)
                    || new[] { ".ttf", ".otf", ".psd", ".exe", ".dll" }.Contains(Path.GetExtension(path).ToLowerInvariant())
                    || Path.GetFileName(path) == ".DS_Store"),
        ];
        Assert.True(unwanted.Length == 0, "Unexpected: " + string.Join(", ", unwanted));

        foreach (string file in BundledFiles(".pnps", ".pnpa", ".pnpm", ".pnpt"))
        {
            Assert.DoesNotContain("soriano", File.ReadAllText(Asset(file), Encoding.Unicode), StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Files the original program opens by a name written in its code: the styles of its
    /// project-type lists, the two default ICC profiles and its stock images.
    /// </summary>
    [Theory]
    [InlineData("styles/default/Cartas_70.pnps")]
    [InlineData("styles/default/Cartas_80.pnps")]
    [InlineData("styles/default/Cartas_Espanola.pnps")]
    [InlineData("styles/default/Cartas_Espanola_Personalizado.pnps")]
    [InlineData("styles/default/Cartas_Euro.pnps")]
    [InlineData("styles/default/Cartas_Gutterfold.pnps")]
    [InlineData("styles/default/Cartas_MiniEuro.pnps")]
    [InlineData("styles/default/Cartas_MiniUsa.pnps")]
    [InlineData("styles/default/Cartas_Poker_Personalizado.pnps")]
    [InlineData("styles/default/Cartas_Standar.pnps")]
    [InlineData("styles/default/Cartas_StandarUsa.pnps")]
    [InlineData("styles/default/Cartas_Tarot.pnps")]
    [InlineData("styles/default/D6.pnps")]
    [InlineData("styles/default/Papel_A4280.pnps")]
    [InlineData("styles/default/Papel_A5200.pnps")]
    [InlineData("styles/default/Papel_LibretoA4.pnps")]
    [InlineData("styles/default/Papel_LibretoA5.pnps")]
    [InlineData("styles/default/Papel_Pocketfold.pnps")]
    [InlineData("styles/default/Papel_Pocketmod.pnps")]
    [InlineData("styles/default/Tokens_15.pnps")]
    [InlineData("styles/default/Tokens_20.pnps")]
    [InlineData("styles/default/Tokens_25.pnps")]
    [InlineData("icc/sRGB-IEC61966-2.1.icc")]
    [InlineData("icc/CoatedFOGRA39.icc")]
    [InlineData("images/blanco.jpg")]
    [InlineData("images/CartaBlanca.jpg")]
    [InlineData("images/Image_NotFound.jpg")]
    [InlineData("images/calibracion.jpg")]
    [InlineData("images/calibracion_marca.jpg")]
    [InlineData("images/color_null.png")]
    [InlineData("images/hexa.png")]
    [InlineData("images/vacia.png")]
    public void Files_the_program_opens_by_name_are_bundled(string file)
    {
        string[] parts = file.Split('/');
        Assert.True(ExistsWithExactCase(Asset(string.Join('/', parts[..^1])), parts[^1]), file);
    }
}
