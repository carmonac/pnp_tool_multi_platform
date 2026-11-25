using PnpTool.Core.CustomDecks;
using PnpTool.Core.Files;
using PnpTool.Core.Primitives;

namespace PnpTool.Core.Tests.Files;

public class PathHelperTests
{
    private static readonly char S = Path.DirectorySeparatorChar;
    private static readonly bool OnWindows = OperatingSystem.IsWindows();

    private static string Local(string path) => path.Replace('/', S);

    [Fact]
    public void Paths_read_from_files_use_the_local_separator()
    {
        Assert.Equal(Local("images/uno/0_rojo.png"), PathHelper.FromFile(@"images\uno\0_rojo.png"));
        Assert.Equal(@"C:\cards\a.png", PathHelper.FromFile(@"C:\cards\a.png"));
        Assert.Equal(string.Empty, PathHelper.FromFile(null));
    }

    [Fact]
    public void Relative_paths_written_to_files_use_backslashes()
    {
        Assert.Equal(@"..\images\a.png", PathHelper.ToFile(Local("../images/a.png")));
        Assert.Equal(string.Empty, PathHelper.ToFile(string.Empty));
        if (!OnWindows)
        {
            Assert.Equal("/home/me/a.png", PathHelper.ToFile("/home/me/a.png"));
        }
    }

    [Theory]
    [InlineData(@".\a.png", "/projects/deck/./a.png")]
    [InlineData(@"images\a.png", "/projects/deck/images/a.png")]
    [InlineData(@"..\shared\a.png", "/projects/shared/a.png")]
    [InlineData(@"..\..\a.png", "/a.png")]
    public void Stored_relative_paths_resolve_against_the_project_folder(string stored, string expected)
    {
        if (OnWindows)
        {
            return; // The expectations are written for Unix-style roots.
        }

        Assert.Equal(Path.GetFullPath(expected), Path.GetFullPath(PathHelper.ResolveRelative("/projects/deck", stored)));
    }

    [Fact]
    public void Absolute_paths_are_left_alone_and_a_null_folder_gives_empty()
    {
        Assert.Equal(@"D:\art\a.png", PathHelper.ResolveRelative(Local("/projects/deck"), @"D:\art\a.png"));
        Assert.Equal(string.Empty, PathHelper.ResolveRelative(null, "a.png"));
        if (!OnWindows)
        {
            Assert.Equal("/art/a.png", PathHelper.ResolveRelative("/projects/deck", "/art/a.png"));
        }
    }

    [Fact]
    public void MakeRelative_always_contains_a_separator()
    {
        string root = OnWindows ? @"C:\projects" : "/projects";
        string project = Path.Combine(root, "deck", "deck.pnp");

        Assert.Equal($".{S}a b.png", PathHelper.MakeRelative(project, Path.Combine(root, "deck", "a b.png")));
        Assert.Equal(Local("images/a.png"), PathHelper.MakeRelative(project, Path.Combine(root, "deck", "images", "a.png")));
        Assert.Equal(Local("../shared/a.png"), PathHelper.MakeRelative(project, Path.Combine(root, "shared", "a.png")));
        Assert.Equal(string.Empty, PathHelper.MakeRelative(project, " "));
    }
}

public class SmallModelTests
{
    [Fact]
    public void AppError_accumulates_messages_and_keeps_the_worst_kind()
    {
        var error = new AppError { Message = "first" };
        error.Message = "second";
        error.Message = string.Empty;
        Assert.Equal("first" + Environment.NewLine + "second", error.Message);
        Assert.False(error.HasError);

        error.Merge(new AppError { Kind = ErrorKind.Warning, Message = "careful" });
        Assert.True(error.HasWarning);
        error.Merge(new AppError { Kind = ErrorKind.MissingData });
        Assert.True(error.HasError);
        error.Merge(new AppError { Kind = ErrorKind.Warning });
        Assert.Equal(ErrorKind.MissingData, error.Kind);
        Assert.EndsWith("careful", error.Message);
    }

    [Fact]
    public void FontSpec_file_form_matches_what_windows_writes()
    {
        var font = new FontSpec { FontFamily = "Arial", FontSize = 12, FontWeight = 700, FontStyle = FontSlant.Italic, TextDecorationType = TextDecorationType.Underline };

        Assert.Equal("Arial:12:Italic:Normal:Bold:Underline:0_0_0_0", font.ToSerializedString());

        FontSpec back = FontSpec.FromSerializedString("Times New Roman:9:Oblique:Condensed:SemiBold:Strikethrough:255_10_20_30");
        Assert.Equal("Times New Roman", back.FontFamily);
        Assert.Equal(9.0, back.FontSize);
        Assert.Equal(FontSlant.Oblique, back.FontStyle);
        Assert.Equal(3, back.FontStretch);
        Assert.Equal(600, back.FontWeight);
        Assert.Equal(TextDecorationType.Strikethrough, back.TextDecorationType);
        Assert.Equal("255_10_20_30", back.Color);
    }

    [Fact]
    public void FontSpec_keeps_the_reading_quirks_of_the_windows_version()
    {
        // A default font is written as weight "Normal" and decoration "None"...
        string written = new FontSpec().ToSerializedString();
        Assert.Equal("Arial:12:Normal:Normal:Normal:None:0_0_0_0", written);

        // ...which the Windows reader turns into Medium weight and a Baseline decoration.
        FontSpec read = FontSpec.FromSerializedString(written);
        Assert.Equal(500, read.FontWeight);
        Assert.Equal(TextDecorationType.Baseline, read.TextDecorationType);
    }

    [Fact]
    public void Card_set_rules()
    {
        Assert.Equal("K", CustomCardSet.CardLabel(13, CardSetType.Poker));
        Assert.Equal("10", CustomCardSet.CardLabel(10, CardSetType.Poker));
        Assert.Equal("12", CustomCardSet.CardLabel(10, CardSetType.Spanish));
        Assert.Equal("7", CustomCardSet.CardLabel(7, CardSetType.Spanish));

        Assert.Equal("255_255_0_0", CustomCardSet.SuitColor(Suit.Hearts));
        Assert.Equal("0_0_0_0", CustomCardSet.SuitColor(Suit.Spades));
        Assert.Equal(Suit.Coins, CustomCardSet.NextSuit(Suit.Clubs));
        Assert.Equal(Suit.Spades, CustomCardSet.NextSuit(Suit.Hearts));
        Assert.Equal(7.0, CustomCardSet.SymbolSize(Suit.Swords).Y.Millimeter, 6);
        Assert.Equal(5.0, CustomCardSet.SymbolSize(Suit.Hearts).Y.Millimeter, 6);

        (string mask, string image) = CustomCardSet.SuitImagePaths(Suit.Coins);
        Assert.EndsWith("mascara_Oros.png", mask);
        Assert.EndsWith("Oros.png", image);
        Assert.Equal(string.Empty, CustomCardSet.SuitImagePaths(Suit.Diamonds).Mask);
        Assert.EndsWith("Diamantes.png", CustomCardSet.SuitImagePaths(Suit.Diamonds).Image);
    }
}
