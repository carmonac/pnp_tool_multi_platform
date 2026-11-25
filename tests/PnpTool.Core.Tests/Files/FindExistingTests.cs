using PnpTool.Core.Files;

namespace PnpTool.Core.Tests.Files;

public sealed class FindExistingTests : IDisposable
{
    private readonly TestFiles.TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Existing_path_is_returned_as_it_is()
    {
        Directory.CreateDirectory(_temp.File("cards"));
        string file = _temp.File("cards", "ace.png");
        File.WriteAllText(file, string.Empty);

        Assert.Equal(file, PathHelper.FindExisting(file));
        Assert.Equal(_temp.File("cards"), PathHelper.FindExisting(_temp.File("cards")));
    }

    [Fact]
    public void Path_that_differs_only_in_letter_case_is_found()
    {
        Directory.CreateDirectory(_temp.File("cards"));
        string file = _temp.File("cards", "ace.png");
        File.WriteAllText(file, string.Empty);

        string found = PathHelper.FindExisting(_temp.File("Cards", "ACE.PNG"));

        // On a file system that ignores case the path already exists and comes back unchanged;
        // on Linux it is replaced by the real spelling. Either way it names the file.
        Assert.True(File.Exists(found), found);
        if (!File.Exists(_temp.File("CARDS", "ACE.PNG")))
        {
            Assert.Equal(file, found);
        }
    }

    [Fact]
    public void Missing_file_and_empty_path_come_back_unchanged()
    {
        string missing = _temp.File("nowhere", "ace.png");

        Assert.Equal(missing, PathHelper.FindExisting(missing));
        Assert.Equal(string.Empty, PathHelper.FindExisting(string.Empty));
        Assert.Equal(@"C:\cards\ace.png", PathHelper.FindExisting(@"C:\cards\ace.png"));
    }
}
