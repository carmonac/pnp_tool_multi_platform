using PnpTool.Core;

namespace PnpTool.Core.Tests;

public sealed class CreditsTests
{
    private static IEnumerable<string> EveryText(bool spanish) =>
        Credits.About(spanish)
            .Concat(Credits.Terms(spanish))
            .Concat(Credits.AdobeProfiles(spanish))
            .Concat(Credits.Team(spanish).SelectMany(group => group.Names.Prepend(group.Title)))
            .Concat(Credits.Translators.SelectMany(group => group.Names.Prepend(group.Title)))
            .Concat(Credits.Patrons)
            .Append(Credits.Thanks(spanish));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void No_text_names_the_printing_company(bool spanish)
    {
        Assert.DoesNotContain(EveryText(spanish), text => text.Contains("soriano", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(EveryText(spanish), string.IsNullOrWhiteSpace);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_author_of_the_original_is_credited(bool spanish)
    {
        Assert.Contains(Credits.About(spanish), line => line.Contains("Jupklass"));
        Assert.Contains(Credits.About(spanish), line => line.Contains("20.11.1"));
        Assert.Equal(["Jupklass"], Credits.Team(spanish)[0].Names);
        Assert.EndsWith("https://github.com/carmonac/pnp_tool_multi_platform", Credits.SourceCode(spanish));
        Assert.Equal(5, Credits.Team(spanish).Count);
    }

    [Fact]
    public void The_names_of_the_original_pages_are_all_there()
    {
        Assert.Equal(13, Credits.Translators.Count);
        Assert.Equal(13, Credits.Translators.Select(group => group.Flag).Distinct().Count());
        Assert.Equal(20, Credits.Translators.Sum(group => group.Names.Count));
        Assert.Equal(15, Credits.Patrons.Count);
        Assert.Equal(Credits.Patrons.Count, Credits.Patrons.Distinct().Count());
    }

    [Fact]
    public void The_terms_say_nothing_is_sent_and_keep_the_rest()
    {
        IReadOnlyList<string> english = Credits.Terms(spanish: false);
        IReadOnlyList<string> spanish = Credits.Terms(spanish: true);
        Assert.Equal(english.Count, spanish.Count);
        Assert.DoesNotContain(english, line => line.Contains("is sent to a server"));
        Assert.DoesNotContain(spanish, line => line.Contains("se envian datos"));
        Assert.Contains(english, line => line.Contains("not affiliated with any printing company"));
        Assert.Contains(english, line => line.Contains("Affero"));
        Assert.Contains(Credits.AdobeProfiles(false), line => line.EndsWith(AppLinks.AdobeIccLicence, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_translator_flag_is_shipped()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "PnpTool.sln")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);
        string flags = Path.Combine(root.FullName, "assets", "images", "flags");
        Assert.All(Credits.Translators, group => Assert.True(File.Exists(Path.Combine(flags, group.Flag + ".png")), group.Flag));
    }
}
