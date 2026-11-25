using PnpTool.Core.Files;

namespace PnpTool.Core.Tests.Files;

public sealed class RecentFilesTests : IDisposable
{
    private readonly TestFiles.TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private string List => _temp.File("LastPNPFiles.txt");

    private string Project(string name)
    {
        string path = _temp.File(name);
        File.WriteAllText(path, string.Empty);
        return path;
    }

    [Fact]
    public void Newest_project_comes_first_and_the_list_persists()
    {
        string a = Project("a.pnp");
        string b = Project("b.pnp");
        var recent = new RecentFiles(List);

        Assert.True(recent.Add(a));
        Assert.True(recent.Add(b));

        var reloaded = new RecentFiles(List);
        reloaded.Load();
        Assert.Equal([b, a], reloaded.Files);
        Assert.Equal(b, reloaded.PathAt(0));
        Assert.Equal(string.Empty, reloaded.PathAt(5));
    }

    [Fact]
    public void File_always_has_ten_lines()
    {
        new RecentFiles(List).Add(Project("a.pnp"));

        string[] lines = File.ReadAllLines(List);
        Assert.Equal(10, lines.Length);
        Assert.All(lines[1..], line => Assert.Equal(string.Empty, line));
    }

    [Fact]
    public void Opening_a_listed_project_again_moves_it_to_the_top()
    {
        string a = Project("a.pnp");
        string b = Project("b.pnp");
        string c = Project("c.pnp");
        var recent = new RecentFiles(List);
        recent.Add(a);
        recent.Add(b);
        recent.Add(c);

        recent.Add(a);

        Assert.Equal([a, c, b], recent.Files);
    }

    [Fact]
    public void Only_ten_projects_are_kept()
    {
        var recent = new RecentFiles(List);
        for (int i = 0; i < 12; i++)
        {
            recent.Add(Project(i + ".pnp"));
        }

        Assert.Equal(10, recent.Files.Count);
        Assert.EndsWith("11.pnp", recent.Files[0]);
        Assert.EndsWith("2.pnp", recent.Files[^1]);
    }

    [Fact]
    public void Only_project_files_are_listed()
    {
        var recent = new RecentFiles(List);

        Assert.False(recent.Add(Project("style.pnps")));

        Assert.Empty(recent.Files);
        Assert.False(File.Exists(List));
    }

    [Fact]
    public void Deleted_projects_leave_the_list()
    {
        string a = Project("a.pnp");
        string b = Project("b.pnp");
        var recent = new RecentFiles(List);
        recent.Add(a);
        recent.Add(b);
        File.Delete(a);
        File.Delete(b);

        Assert.True(recent.Add(a));
        Assert.Equal([b], recent.Files);

        recent.Load();
        Assert.Empty(recent.Files);
    }

    [Fact]
    public void Adding_a_missing_project_that_is_not_listed_changes_nothing()
    {
        string a = Project("a.pnp");
        var recent = new RecentFiles(List);
        recent.Add(a);

        Assert.True(recent.Add(_temp.File("ghost.pnp")));

        Assert.Equal([a], recent.Files);
    }

    [Fact]
    public void Reading_stops_at_the_first_empty_line()
    {
        string a = Project("a.pnp");
        string b = Project("b.pnp");
        File.WriteAllLines(List, [a, string.Empty, b]);
        var recent = new RecentFiles(List);

        recent.Load();

        Assert.Equal([a], recent.Files);
    }

    [Fact]
    public void Entries_can_be_removed()
    {
        string a = Project("a.pnp");
        string b = Project("b.pnp");
        var recent = new RecentFiles(List);
        recent.Add(a);
        recent.Add(b);

        recent.RemoveAt(0);
        recent.RemoveAt(7);

        Assert.Equal([a], recent.Files);
        Assert.Equal(a, File.ReadAllLines(List)[0]);
    }

    [Fact]
    public void Missing_list_file_gives_an_empty_list()
    {
        var recent = new RecentFiles(List);
        recent.Load();
        Assert.Empty(recent.Files);
    }

    [Theory]
    [InlineData("/home/ana/game.pnp", "/home/ana/game.pnp")]
    [InlineData("/home/ana/games/prototype/deck.pnp!", "/home/ana/games/prototype/deck.pnp!")] // 35 characters
    [InlineData("/home/ana/games/prototypes/cards.pnp", "/home/ana/......ames/prototypes/cards.pnp")]
    public void Long_paths_are_shortened_for_the_menu(string path, string shown)
    {
        Assert.Equal(shown, RecentFiles.DisplayText(path));
    }
}

public sealed class CustomColorsTests : IDisposable
{
    private readonly TestFiles.TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private string ColorFile => _temp.File("CustomColors.txt");

    [Fact]
    public void Colours_persist_newest_first()
    {
        var colors = new CustomColors(11, ColorFile);
        colors.Push("255_255_0_0");
        colors.Push("255_0_255_0");
        colors.Save();

        var reloaded = new CustomColors(11, ColorFile);
        reloaded.Load();

        Assert.Equal(["255_0_255_0", "255_255_0_0"], reloaded.Colors);
        Assert.Equal(["255_0_255_0", "255_255_0_0"], File.ReadAllLines(ColorFile));
    }

    [Fact]
    public void Oldest_colour_is_dropped_when_the_list_is_full()
    {
        var colors = new CustomColors(3, ColorFile);
        for (int i = 1; i <= 5; i++)
        {
            colors.Push($"255_{i}_0_0");
        }

        Assert.Equal(["255_5_0_0", "255_4_0_0", "255_3_0_0"], colors.Colors);
    }

    [Fact]
    public void Lines_that_are_not_colours_are_skipped_and_capacity_limits_reading()
    {
        File.WriteAllLines(ColorFile, ["255_1_1_1", string.Empty, "red", "255_2_2", "255_300_0_0", "255_2_2_2", "255_3_3_3"]);
        var colors = new CustomColors(2, ColorFile);

        colors.Load();

        Assert.Equal(["255_1_1_1", "255_2_2_2"], colors.Colors);
    }

    [Fact]
    public void Empty_or_missing_file_gives_no_colours()
    {
        var colors = new CustomColors(11, ColorFile);
        colors.Load();
        Assert.Empty(colors.Colors);

        File.WriteAllText(ColorFile, string.Empty);
        colors.Load();
        Assert.Empty(colors.Colors);
    }

    [Fact]
    public void Only_colours_can_be_pushed()
    {
        var colors = new CustomColors(11, ColorFile);
        Assert.Throws<ArgumentException>(() => colors.Push("blue"));
    }
}
