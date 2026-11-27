using System.IO.Compression;
using PnpTool.Core.Files;

namespace PnpTool.Core.Tests.Files;

/// <summary>Images taken out of compressed files.</summary>
public class ImageArchivesTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pnptool-archives-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static bool IsImage(string name) => name.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

    private string Zip(string name, params (string Entry, string Text)[] entries)
    {
        string path = Path.Combine(_folder, name);
        using ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach ((string entry, string text) in entries)
        {
            using var writer = new StreamWriter(zip.CreateEntry(entry).Open());
            writer.Write(text);
        }

        return path;
    }

    [Fact]
    public void Compressed_files_are_told_by_their_extension()
    {
        Assert.True(ImageArchives.IsArchive("cards.zip"));
        Assert.True(ImageArchives.IsArchive("/some/where/Cards.RAR"));
        Assert.True(ImageArchives.IsArchive("cards.7z"));
        Assert.True(ImageArchives.IsArchive("comic.cbz"));
        Assert.False(ImageArchives.IsArchive("cards.png"));
        Assert.False(ImageArchives.IsArchive("cards"));
        Assert.False(ImageArchives.IsArchive(null));
    }

    [Fact]
    public void Images_go_to_a_folder_named_after_the_file_with_their_folders()
    {
        string zip = Zip("deck.zip", ("one.png", "1"), ("notes.txt", "no"), ("backs/two.png", "2"), ("__MACOSX/._one.png", "x"));

        (string[] images, AppError error) = ImageArchives.Extract(zip, IsImage);

        string folder = Path.Combine(_folder, "deck");
        Assert.False(error.HasError, error.Message);
        Assert.Equal([Path.Combine(folder, "one.png"), Path.Combine(folder, "backs", "two.png")], images);
        Assert.Equal("1", File.ReadAllText(images[0]));
        Assert.Equal("2", File.ReadAllText(images[1]));
        Assert.False(File.Exists(Path.Combine(folder, "notes.txt")));
    }

    [Fact]
    public void Names_that_point_outside_the_folder_are_not_written()
    {
        string zip = Zip("sly.zip", ("../escaped.png", "x"), ("in/../../escaped2.png", "x"), ("fine.png", "ok"));

        (string[] images, AppError error) = ImageArchives.Extract(zip, IsImage);

        Assert.False(error.HasError, error.Message);
        Assert.Equal([Path.Combine(_folder, "sly", "fine.png")], images);
        Assert.False(File.Exists(Path.Combine(_folder, "escaped.png")));
        Assert.False(File.Exists(Path.Combine(_folder, "escaped2.png")));
    }

    [Fact]
    public void Files_already_taken_out_are_left_as_they_are()
    {
        string zip = Zip("deck.zip", ("one.png", "from the zip"));
        string taken = ImageArchives.Extract(zip, IsImage).Images.Single();
        File.WriteAllText(taken, "changed by the user");

        (string[] images, AppError error) = ImageArchives.Extract(zip, IsImage);

        Assert.False(error.HasError, error.Message);
        Assert.Equal([taken], images);
        Assert.Equal("changed by the user", File.ReadAllText(taken));
    }

    [Fact]
    public void Folder_gets_another_name_when_a_file_has_it()
    {
        string zip = Zip("deck.zip", ("one.png", "1"));
        File.WriteAllText(Path.Combine(_folder, "deck"), "a file, not a folder");

        (string[] images, AppError error) = ImageArchives.Extract(zip, IsImage);

        Assert.False(error.HasError, error.Message);
        Assert.Equal([Path.Combine(_folder, "deck_images", "one.png")], images);
    }

    [Fact]
    public void File_that_is_not_what_it_says_gives_an_error_that_names_it()
    {
        string path = Path.Combine(_folder, "broken.zip");
        File.WriteAllText(path, "this is not a zip");

        (string[] images, AppError error) = ImageArchives.Extract(path, IsImage);

        Assert.True(error.HasError);
        Assert.Contains("broken.zip", error.Message);
        Assert.Empty(images);
    }
}
