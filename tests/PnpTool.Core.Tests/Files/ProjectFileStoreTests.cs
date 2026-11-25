using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;
using FilterType = ImageMagick.FilterType;
using PnpTool.Core.Cards;
using PnpTool.Core.CustomDecks;
using PnpTool.Core.Files;
using PnpTool.Core.Paper;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Core.Templates;

namespace PnpTool.Core.Tests.Files;

public class ProjectFileStoreTests
{
    private static ProjectSettings LoadStyle(string name)
    {
        var settings = new ProjectSettings();
        (AppError error, bool resave) = ProjectFileStore.LoadStyle(TestFiles.Path(name), ref settings);
        Assert.False(error.HasError, error.Message);
        Assert.False(resave);
        return settings;
    }

    private static string ToXml<T>(T value)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var writer = new StringWriter();
        serializer.Serialize(writer, value);
        return writer.ToString();
    }

    [Fact]
    public void Loads_the_standard_card_style_written_by_windows()
    {
        ProjectSettings style = LoadStyle("Cartas_Standar.pnps");

        Assert.Equal(63.5, style.Front.Size.X.Millimeter, 6);
        Assert.Equal(88.9, style.Front.Size.Y.Millimeter, 6);
        Assert.Equal(63.5, style.Grid.CardSize.X.Millimeter, 6);
        Assert.Equal(ImageEffectType.None, style.Front.Effect);
        Assert.Equal(20, style.Front.RoundedCornerSize);
        Assert.True(style.Front.Locked);
        Assert.Equal(CutLineType.Normal, style.Front.CutLine.LineType);
        Assert.Equal("255_0_0_0", style.Front.CutLine.Color);
        Assert.Equal(0.4, style.Front.CutLine.Thickness.Point, 6);
        Assert.Equal(3, style.Front.CutLine.Gap);
        Assert.Equal(PaperType.A4, style.PrintLayout.Paper.Type);
        Assert.Equal(210, style.PrintLayout.Paper.Size.X.Millimeter, 6);
        Assert.Equal(ProjectType.Card, style.Group.ProjectType);
        Assert.Equal(SettingsGroup.Custom, style.Group.Group);
        Assert.Equal(3, style.PrintLayout.Rows);
        Assert.Equal(3, style.PrintLayout.Columns);
        Assert.True(style.PrintLayout.CalculateAutomatically);
        Assert.Equal(BackImageMode.NoBack, style.PrintLayout.BackImage);
        Assert.Equal(1, style.HeaderFooter.FirstPageNumber);
        Assert.Equal(FaceType.None, style.FillFaces);
        Assert.Equal(ImagePathMode.Relative, style.Other.ImagePathMode);
        Assert.Equal(PdfOutputMode.SingleFile, style.Other.PdfOutput);
        Assert.Equal(FilterType.Lanczos, style.Other.ResizeFilter);
        Assert.Equal(ImageKind.Blank, style.BlankFace.ImageKind);
        Assert.Equal("255_255_255_255", style.BlankFace.Path);
        Assert.Equal("Arial", style.Font.FontFamily);
        Assert.Equal(FontSizeUnit.Point, style.Font.Unit);
        Assert.Equal(8f, style.Font.Size);
        Assert.Equal(13, style.Font.Height);
    }

    [Theory]
    [InlineData("Papel_LibretoA4.pnps", ProjectType.Booklet)]
    [InlineData("Cartas_Gutterfold.pnps", ProjectType.Gutterfold)]
    public void Loads_other_project_types(string file, ProjectType expected)
    {
        Assert.Equal(expected, LoadStyle(file).Group.ProjectType);
    }

    [Theory]
    [InlineData("Cartas_Standar.pnps")]
    [InlineData("Papel_LibretoA4.pnps")]
    [InlineData("Cartas_Gutterfold.pnps")]
    public void Style_survives_a_save_and_reload(string file)
    {
        using var temp = new TestFiles.TempFolder();
        ProjectSettings original = LoadStyle(file);
        string saved = temp.File("saved.pnps");

        ProjectFileStore.SaveStyle(saved, original);
        var reloaded = new ProjectSettings();
        AppError error = ProjectFileStore.LoadStyle(saved, ref reloaded).Error;

        Assert.False(error.HasError, error.Message);
        Assert.Equal(ToXml(original), ToXml(reloaded));
    }

    [Fact]
    public void Saved_files_have_the_windows_layout()
    {
        using var temp = new TestFiles.TempFolder();
        string saved = temp.File("saved.pnps");
        ProjectFileStore.SaveStyle(saved, LoadStyle("Cartas_Standar.pnps"));

        byte[] bytes = File.ReadAllBytes(saved);
        Assert.Equal([0xFF, 0xFE], bytes[..2]); // UTF-16 little-endian byte-order mark
        string text = Encoding.Unicode.GetString(bytes);
        Assert.Contains("<!--Fichero del programa PNP Tool ", text);

        XDocument document = XDocument.Load(saved);
        Assert.Equal("PNP_Tool_Estilo", document.Root!.Name.LocalName);
        Assert.Equal("18", document.Root.Element("Version")!.Value);
        Assert.NotNull(document.Root.Element("ConfiguracionProyectoVActual"));
    }

    /// <summary>
    /// Every element the Windows file contains must also be written by this program under the
    /// same name and at the same place, apart from the values old versions wrote redundantly.
    /// </summary>
    [Theory]
    [InlineData("Cartas_Standar.pnps", "saved.pnps")]
    [InlineData("Papel_LibretoA4.pnps", "saved.pnps")]
    public void Saved_style_uses_the_same_element_names_as_windows(string file, string savedName)
    {
        using var temp = new TestFiles.TempFolder();
        string saved = temp.File(savedName);
        ProjectFileStore.SaveStyle(saved, LoadStyle(file));

        HashSet<string> windows = ElementPaths(TestFiles.Path(file));
        HashSet<string> ours = ElementPaths(saved);

        // Written by the version that produced the test files, no longer by 20.11.
        string[] obsolete = ["/Inch", "/Millimeter", "/Centimeter", "/Presentation", "/OtrosParametrosProyecto/ColorspaceProyecto", "/ParametrosDeEntradaProyecto/NombreProyecto"];
        string[] missing = [.. windows.Where(path => !ours.Contains(path) && !obsolete.Any(path.EndsWith)).Order()];
        Assert.True(missing.Length == 0, "Not written: " + string.Join(", ", missing));
    }

    private static HashSet<string> ElementPaths(string file)
    {
        var paths = new HashSet<string>();
        void Walk(XElement element, string prefix)
        {
            string path = prefix + "/" + element.Name.LocalName;
            paths.Add(path);
            foreach (XElement child in element.Elements())
            {
                Walk(child, path);
            }
        }

        Walk(XDocument.Load(file).Root!, string.Empty);
        return paths;
    }

    [Fact]
    public void Loads_an_advanced_template()
    {
        var template = new AdvancedTemplate();
        AppError error = ProjectFileStore.LoadAdvancedTemplate(TestFiles.Path("Template1.pnpa"), ref template);

        Assert.False(error.HasError, error.Message);
        Assert.NotEmpty(template.Cards);
        AdvancedTemplateCard first = template.Cards[0];
        Assert.Equal(10.0, first.Location.X.Millimeter, 6);
        Assert.Equal(8.0, first.Location.Y.Millimeter, 6);
        Assert.Equal(120.0, first.Size.X.Millimeter, 6);
        Assert.Equal(70.0, first.Size.Y.Millimeter, 6);
        Assert.Equal(90, first.Rotation);
        Assert.Equal("#FFA9F14B", first.Color);

        AdvancedTemplate clone = template.Clone();
        Assert.NotSame(template.Cards[0], clone.Cards[0]);
        Assert.Equal(ToXml(template), ToXml(clone));
    }

    [Fact]
    public void Loads_a_bundled_custom_deck()
    {
        var deck = new CustomDeck();
        AppError error = ProjectFileStore.LoadCustomDeck(TestFiles.Path("Uno.pnpm"), ref deck);

        Assert.False(error.HasError, error.Message);
        Assert.Equal("Uno", deck.Name);
        Assert.Equal("uno.pnps", deck.Style);
        Assert.Equal(54, deck.Cards.Length);
        Assert.Equal("0 rojo", deck.Cards[0].NameSpanish);
        Assert.Equal("0 red", deck.Cards[0].NameEnglish);
        Assert.Equal(1, deck.Cards[0].Copies);
        Assert.Equal(@"\imagenes\uno\0_rojo.png", deck.Cards[0].ImagePath);
        Assert.Equal(4, deck.Cards[^1].Copies);
        Assert.Equal(108, deck.Cards.Sum(card => card.Copies));
    }

    [Fact]
    public void Custom_deck_survives_a_save_and_reload()
    {
        using var temp = new TestFiles.TempFolder();
        var deck = new CustomDeck();
        ProjectFileStore.LoadCustomDeck(TestFiles.Path("Uno.pnpm"), ref deck);
        string saved = temp.File("uno.pnpm");

        ProjectFileStore.SaveCustomDeck(saved, deck);
        var reloaded = new CustomDeck();
        ProjectFileStore.LoadCustomDeck(saved, ref reloaded);

        Assert.Equal(ToXml(deck), ToXml(reloaded));
        Assert.Equal("PNP_Tool_Mazo", XDocument.Load(saved).Root!.Name.LocalName);
    }

    [Fact]
    public void Sports_deck_round_trips_with_its_texts_and_fonts()
    {
        using var temp = new TestFiles.TempFolder();
        var deck = new SportsDeck { Name = "League", TemplateImagePath = "template.png", TeamTextVisible = false };
        deck.TeamText.Text = "Team";
        deck.TeamText.Position = new PointD(0.25, 0.5);
        deck.TeamText.Font = new FontSpec { FontFamily = "Verdana", FontSize = 14, FontWeight = 700, FontStyle = FontSlant.Italic, Color = "255_255_0_0" };
        deck.Add(new CustomCard { NameSpanish = "Jugador", Copies = 2 });
        string saved = temp.File("league.pnpm");

        ProjectFileStore.SaveCustomDeck(saved, deck);
        (AppError error, SportsDeck reloaded) = ProjectFileStore.LoadSportsDeck(saved, "MazoDeportivo");

        Assert.False(error.HasError, error.Message);
        Assert.Equal("League", reloaded.Name);
        Assert.False(reloaded.TeamTextVisible);
        Assert.Equal("Team", reloaded.TeamText.Text);
        Assert.Equal(0.25, reloaded.TeamText.Position.X);
        Assert.Equal("Verdana", reloaded.TeamText.Font.FontFamily);
        Assert.Equal(700, reloaded.TeamText.Font.FontWeight);
        Assert.Equal(FontSlant.Italic, reloaded.TeamText.Font.FontStyle);
        Assert.Equal("255_255_0_0", reloaded.TeamText.Font.Color);
        Assert.Equal(2, reloaded.Cards.Single().Copies);
    }

    [Fact]
    public void Loads_and_saves_a_ticket_project()
    {
        using var temp = new TestFiles.TempFolder();
        var ticket = new TicketProjectSettings();
        AppError error = ProjectFileStore.LoadTicket(TestFiles.Path("TicketDefecto.pnpt"), ref ticket);
        Assert.False(error.HasError, error.Message);
        Assert.Equal(10, ticket.PerforationLinePositions.Length);
        Assert.Equal(10, ticket.NumberPositions.Length);
        Assert.All(ticket.NumberPositions, Assert.NotNull);

        ticket.NumberFrom = 5;
        ticket.NumberTo = 250;
        ticket.NumberPositions[3].PixelPosition = new PointI(12, 34);
        string saved = temp.File("ticket.pnpt");
        ProjectFileStore.SaveTicket(saved, ticket);
        var reloaded = new TicketProjectSettings();
        error = ProjectFileStore.LoadTicket(saved, ref reloaded);

        Assert.False(error.HasError, error.Message);
        Assert.Equal(ToXml(ticket), ToXml(reloaded));
        Assert.Equal(34, reloaded.NumberPositions[3].PixelPosition.Y);
    }

    [Fact]
    public void Project_with_relative_paths_round_trips_and_stores_backslash_paths()
    {
        using var temp = new TestFiles.TempFolder();
        Directory.CreateDirectory(temp.File("images"));
        string front1 = temp.File("images", "dragon x3.png");
        string front2 = temp.File("images", "knight.png");
        string back = temp.File("back.png");
        string projectFile = temp.File("deck.pnp");

        var project = new Project { Settings = LoadStyle("Cartas_Standar.pnps") };
        project.Settings.PrintLayout.BackImage = BackImageMode.SameBack;
        project.Settings.SharedBack = new CardFace(back, FaceType.Back, ImageKind.Normal);
        project.Deck.Add(front1, ImageKind.Normal);
        project.Deck.Add(front2, ImageKind.Normal, back, ImageKind.Normal);
        project.Deck.Cards[1].Front.Rotation = new RotationProperties(90, flipHorizontal: true, flipVertical: false);
        Assert.Equal(4, project.Deck.TotalCopies);

        ProjectFileStore.SaveProject(projectFile, project);

        // In memory the full paths are back.
        Assert.Equal(front1, project.Deck.Cards[0].Front.Path);
        Assert.Equal(back, project.Deck.Cards[1].Back.OriginalPath);
        Assert.Equal(back, project.Settings.SharedBack.OriginalPath);
        Assert.False(File.Exists(projectFile[..^1] + "~"));

        // In the file they are relative, written the way Windows writes them.
        XDocument document = XDocument.Load(projectFile);
        Assert.Equal("PNP_Tool_Proyecto", document.Root!.Name.LocalName);
        XElement firstCard = document.Root.Element("Mazo")!.Element("Cartas")!.Elements("Carta").First();
        Assert.Equal(@"images\dragon x3.png", firstCard.Element("CaraFrontal")!.Element("Ruta")!.Value);
        Assert.Equal("3", firstCard.Element("NumerodeCopias")!.Value);
        Assert.Equal(@".\back.png", document.Root.Element("ConfiguracionProyectoVActual")!.Element("CaraTraseraIgual")!.Element("RutaOriginal")!.Value);
        Assert.Null(document.Root.Element("PlantillaAvanzada"));

        (AppError error, Project reloaded) = ProjectFileStore.LoadProject(projectFile);

        Assert.False(error.HasError, error.Message);
        Assert.Equal(2, reloaded.Deck.Cards.Length);
        Assert.Equal(4, reloaded.Deck.TotalCopies);
        Assert.Equal(Path.GetFullPath(front1), Path.GetFullPath(reloaded.Deck.Cards[0].Front.Path));
        Assert.Equal(Path.GetFullPath(back), Path.GetFullPath(reloaded.Deck.Cards[1].Back.OriginalPath));
        Assert.Equal(Path.GetFullPath(back), Path.GetFullPath(reloaded.Settings.SharedBack.Path));
        Assert.Equal(90, reloaded.Deck.Cards[1].Front.Rotation.Angle);
        Assert.True(reloaded.Deck.Cards[1].Front.Rotation.FlipHorizontal);
        Assert.Equal(ImageKind.Empty, reloaded.Deck.Cards[0].Back.ImageKind);
        Assert.Equal(63.5, reloaded.Settings.Front.Size.X.Millimeter, 6);
    }

    [Fact]
    public void Project_using_an_advanced_template_stores_the_template()
    {
        using var temp = new TestFiles.TempFolder();
        var project = new Project();
        project.Settings.Group.Group = SettingsGroup.AdvancedStyles;
        project.AdvancedTemplate.Name = "Tuck box";
        project.AdvancedTemplate.AddCard(new AdvancedTemplateCard { Rotation = 180, Size = new UnitPoint(Unit.FromMillimeter(50), Unit.FromMillimeter(70)) });
        string projectFile = temp.File("box.pnp");

        ProjectFileStore.SaveProject(projectFile, project);
        (AppError error, Project reloaded) = ProjectFileStore.LoadProject(projectFile);

        Assert.False(error.HasError, error.Message);
        Assert.Equal("Tuck box", reloaded.AdvancedTemplate.Name);
        Assert.Equal(180, reloaded.AdvancedTemplate.Cards.Single().Rotation);
        Assert.Equal(70.0, reloaded.AdvancedTemplate.Cards[0].Size.Y.Millimeter, 6);
    }

    [Theory]
    [InlineData(12, "under 12")]
    [InlineData(13, "under 14")]
    [InlineData(16, "must be upgraded")]
    [InlineData(19, "FicheroXML_MasModerno")]
    public void Project_files_of_other_versions_are_refused_with_a_message(int version, string expectedText)
    {
        using var temp = new TestFiles.TempFolder();
        string file = temp.File("old.pnp");
        File.WriteAllText(file, $"<PNP_Tool_Proyecto><Version>{version}</Version></PNP_Tool_Proyecto>", Encoding.Unicode);

        (AppError error, _) = ProjectFileStore.LoadProject(file);

        Assert.True(error.HasError);
        Assert.Contains(expectedText, error.Message);
    }

    [Fact]
    public void Missing_file_is_reported_not_thrown()
    {
        (AppError error, int version) = ProjectFileStore.ReadVersion("/nonexistent/file.pnp");

        Assert.True(error.HasError);
        Assert.Equal(0, version);
        Assert.Contains("not found", error.Message);
    }
}
