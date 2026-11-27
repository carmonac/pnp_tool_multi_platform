using System.Text;
using System.Xml;
using System.Xml.Linq;
using PnpTool.Core.Cards;
using PnpTool.Core.Files;
using PnpTool.Core.Paper;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.Core.Tests.Files;

/// <summary>
/// Files of versions 14 to 17 are upgraded when opened. No file written by those old programs
/// was at hand, so each test rewrites a file of today's version into the old shape, following
/// the classes the Windows program reads them with.
/// </summary>
[Collection(GlobalState.Name)]
public class OldFileVersionTests
{
    private const string Settings = "ConfiguracionProyectoVActual";

    private static Project SampleProject(string backImage)
    {
        var settings = new ProjectSettings();
        (AppError error, _) = ProjectFileStore.LoadStyle(TestFiles.Path("Cartas_Standar.pnps"), ref settings);
        Assert.False(error.HasError, error.Message);

        settings.PrintLayout.Rows = 2;
        settings.PrintLayout.Columns = 4;
        settings.PrintLayout.Paper.Type = PaperType.A3;
        settings.PrintLayout.Paper.Landscape = true;
        settings.PrintLayout.Paper.Size = new UnitPoint(Unit.FromMillimeter(420), Unit.FromMillimeter(297));
        settings.PrintLayout.DoubleSided = true;
        settings.PrintLayout.BackImage = BackImageMode.SameBack;
        settings.SharedBack = new CardFace(backImage, FaceType.Back, ImageKind.Normal);
        settings.Book.PerforationMarks.Type = PerforationMarkType.Outside;
        settings.Book.PerforationMarks.Count = 4;
        settings.Book.ImagePlacement = BookImagePlacement.TowardsSpine;
        settings.Book.Spine = BookSpineType.Right;
        settings.Other.GenerateFileId = true;
        settings.Other.FileIdSeed = "seed";
        settings.Other.PdfOutput = PdfOutputMode.MaximumSize;
        settings.Other.PdfMaximumSize = 12;
        settings.Other.CompressionPercent = 40.0;
        settings.BackRotation = 1.5;
        settings.LockCardSize = false;
        settings.Front.Size = new UnitPoint(Unit.FromMillimeter(59), Unit.FromMillimeter(92));

        var project = new Project { Settings = settings };
        project.Deck.Add(Path.Combine(Path.GetDirectoryName(backImage)!, "dragon x3.png"), ImageKind.Normal);
        return project;
    }

    /// <summary>Rewrites a file of today's version the way <paramref name="version"/> stored it.</summary>
    private static void MakeOld(string file, int version, string writtenBy = "20.6.0", bool onlyOneBack = false)
    {
        XDocument document = XDocument.Load(file);
        XElement root = document.Root!;
        root.Element("Version")!.Value = version.ToString();
        XElement settings = root.Element(Settings)!;

        // Up to 17 the paper's values sat beside rows and columns.
        XElement layout = settings.Element("DisposicionImpresionProyecto")!;
        XElement paper = layout.Element("Papel")!;
        layout.Add(new XElement("Horizontal", paper.Element("Horizontal")!.Value));
        layout.Add(new XElement("TamanoPapel", paper.Element("Tamano")!.Nodes()));
        layout.Add(new XElement("IndiceComboPapel", paper.Element("NombrePapel")!.Value));
        paper.Remove();

        if (version <= 16)
        {
            settings.Name = "ConfiguracionProyectoV" + version;
            settings.Element("RotacionTrasera")!.Remove();
            settings.Element("Bloquear_TamanoCarta")!.Remove();
            string[] kept = ["TipoRutaImagenes", "PdfPorSeparados", "PdfPorSeparadosTamano", "GenerarIDFichero", "IDFichero_Semilla", "ComprimirArchivo"];
            settings.Element("OtrosParametrosProyecto")!.Elements().Where(e => !kept.Contains(e.Name.LocalName)).Remove();
        }

        if (version <= 15)
        {
            settings.Add(new XElement("CheckBox_Trasera_100", onlyOneBack ? "true" : "false"));
        }

        if (version == 14)
        {
            XElement book = settings.Element("Configuracionlibro")!;
            settings.Add(book.Element("MarcasPerforacion"), book.Element("ImagenEnLibro"));
            book.Remove();
        }

        foreach (XComment comment in document.Nodes().OfType<XComment>().ToList())
        {
            comment.Remove();
        }

        document.AddFirst(new XComment("Fichero del programa PNP Tool " + writtenBy));
        var options = new XmlWriterSettings { Encoding = Encoding.Unicode, Indent = true, OmitXmlDeclaration = true };
        using XmlWriter writer = XmlWriter.Create(file, options);
        document.Save(writer);
    }

    private static int VersionOf(string file) => ProjectFileStore.ReadVersion(file).Version;

    [Theory]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    public void Old_project_opens_with_its_settings_and_is_saved_in_todays_version(int version)
    {
        using var temp = new TestFiles.TempFolder();
        string back = temp.File("back.png");
        string file = temp.File("deck.pnp");
        ProjectFileStore.SaveProject(file, SampleProject(back));
        MakeOld(file, version);

        (AppError error, Project project) = ProjectFileStore.LoadProject(file);

        Assert.False(error.HasError, error.Message);
        ProjectSettings settings = project.Settings;
        Assert.Equal(2, settings.PrintLayout.Rows);
        Assert.Equal(4, settings.PrintLayout.Columns);
        Assert.Equal(8, settings.PrintLayout.CardsPerSheet);
        Assert.Equal(PaperType.A3, settings.PrintLayout.Paper.Type);
        Assert.True(settings.PrintLayout.Paper.Landscape);
        Assert.Equal(420.0, settings.PrintLayout.Paper.Size.X.Millimeter, 6);
        Assert.True(settings.PrintLayout.DoubleSided);
        Assert.Equal(BackImageMode.SameBack, settings.PrintLayout.BackImage);
        Assert.Equal(Path.GetFullPath(back), Path.GetFullPath(settings.SharedBack.Path));
        Assert.Equal(92.0, settings.Front.Size.Y.Millimeter, 6);
        Assert.Equal(PerforationMarkType.Outside, settings.Book.PerforationMarks.Type);
        Assert.Equal(4, settings.Book.PerforationMarks.Count);
        Assert.Equal(BookImagePlacement.TowardsSpine, settings.Book.ImagePlacement);
        Assert.True(settings.Other.GenerateFileId);
        Assert.Equal("seed", settings.Other.FileIdSeed);
        Assert.Equal(PdfOutputMode.MaximumSize, settings.Other.PdfOutput);
        Assert.Equal(12, settings.Other.PdfMaximumSize);
        Assert.Equal(3, project.Deck.TotalCopies);

        // What only later versions stored comes from the file, or is the default.
        Assert.Equal(version >= 16 ? BookSpineType.Right : BookSpineType.Left, settings.Book.Spine);
        Assert.Equal(version == 17 ? 40.0 : 100.0, settings.Other.CompressionPercent);
        Assert.Equal(version == 17 ? 1.5 : 0.0, settings.BackRotation);
        Assert.Equal(version != 17, settings.LockCardSize);

        // The old file is kept, and the one in its place is of today's version and opens the same.
        string backup = temp.File("deck_back.pnp");
        Assert.Equal(version, VersionOf(backup));
        Assert.Equal(ProjectFileStore.StyleFileVersion, VersionOf(file));
        Assert.Equal(["deck.pnp", "deck_back.pnp"], Directory.GetFiles(temp.Root).Select(Path.GetFileName).Order(StringComparer.Ordinal));

        (error, Project reloaded) = ProjectFileStore.LoadProject(file);
        Assert.False(error.HasError, error.Message);
        Assert.Equal(ProjectFileStore.SerializeToString(project), ProjectFileStore.SerializeToString(reloaded));
        Assert.Equal(Path.GetFullPath(back), Path.GetFullPath(reloaded.Settings.SharedBack.Path));
        Assert.Equal(3, reloaded.Deck.TotalCopies);
    }

    [Theory]
    [InlineData(14, true)]
    [InlineData(15, true)]
    [InlineData(16, false)]
    public void Only_one_back_of_versions_14_and_15_becomes_a_kind_of_back_image(int version, bool expected)
    {
        using var temp = new TestFiles.TempFolder();
        string file = temp.File("deck.pnp");
        ProjectFileStore.SaveProject(file, SampleProject(temp.File("back.png")));
        MakeOld(file, version, onlyOneBack: true);

        (AppError error, Project project) = ProjectFileStore.LoadProject(file);

        Assert.False(error.HasError, error.Message);
        Assert.Equal(expected ? BackImageMode.OnlyOne : BackImageMode.SameBack, project.Settings.PrintLayout.BackImage);
    }

    [Theory]
    [InlineData("20.2.9", SettingsGroup.Custom, 0)]
    [InlineData("20.3.0", SettingsGroup.Books, 2)]
    [InlineData("20.11.1 - With Magic", SettingsGroup.Books, 2)]
    [InlineData("unknown", SettingsGroup.Custom, 0)]
    public void Group_is_reset_when_the_file_comes_from_before_the_groups_changed(string writtenBy, SettingsGroup group, int subgroup)
    {
        using var temp = new TestFiles.TempFolder();
        string file = temp.File("deck.pnp");
        Project sample = SampleProject(temp.File("back.png"));
        sample.Settings.Group.Group = SettingsGroup.Books;
        sample.Settings.Group.SubgroupIndex = 2;
        ProjectFileStore.SaveProject(file, sample);
        MakeOld(file, 17, writtenBy);

        (AppError error, Project project) = ProjectFileStore.LoadProject(file);

        Assert.False(error.HasError, error.Message);
        Assert.Equal(group, project.Settings.Group.Group);
        Assert.Equal(subgroup, project.Settings.Group.SubgroupIndex);
    }

    [Theory]
    [InlineData(14)]
    [InlineData(17)]
    public void Old_style_is_saved_in_todays_version_with_a_backup_beside_it(int version)
    {
        using var temp = new TestFiles.TempFolder();
        string file = temp.File("mine.pnps");
        ProjectSettings sample = SampleProject(temp.File("back.png")).Settings;
        sample.PrintLayout.BackImage = BackImageMode.NoBack;
        sample.SharedBack = new CardFace();
        ProjectFileStore.SaveStyle(file, sample);
        MakeOld(file, version);

        var settings = new ProjectSettings();
        (AppError error, bool upgraded) = ProjectFileStore.LoadStyle(file, ref settings);

        Assert.False(error.HasError, error.Message);
        Assert.True(upgraded);
        Assert.Equal(4, settings.PrintLayout.Columns);
        Assert.Equal(PaperType.A3, settings.PrintLayout.Paper.Type);
        Assert.Equal(version, VersionOf(temp.File("mine_backup.pnps")));
        Assert.Equal(ProjectFileStore.StyleFileVersion, VersionOf(file));

        var again = new ProjectSettings();
        (error, upgraded) = ProjectFileStore.LoadStyle(file, ref again);
        Assert.False(error.HasError, error.Message);
        Assert.False(upgraded);
        Assert.Equal(4, again.PrintLayout.Columns);
        Assert.Equal(4, again.Book.PerforationMarks.Count);
    }
}
