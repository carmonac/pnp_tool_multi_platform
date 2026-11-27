using System.IO.Compression;
using Avalonia.Controls;
using ImageMagick;
using PnpTool.App.ViewModels;
using PnpTool.App.Views;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;

namespace PnpTool.App.Tests;

/// <summary>
/// Adding images: the question about fronts and backs, folders, compressed files and the
/// warnings.
/// </summary>
[Collection(UserInterface.Name)]
public sealed class AddImagesTests(HeadlessFixture fixture)
{
    private static string Picture(string folder, string name)
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, name);
        using var image = new MagickImage(new MagickColor("#3060C0"), 60, 84);
        image.Write(path);
        return path;
    }

    /// <summary>What the model asked and was answered, in place of the windows.</summary>
    private sealed class Answers
    {
        /// <summary>The files each file dialog gives, in turn.</summary>
        public Queue<string[]> Picks { get; } = new();

        /// <summary>Whether each file dialog allowed several files.</summary>
        public List<bool> Multiple { get; } = [];

        public AddImagesAnswer? FrontsAndBacks { get; set; }

        public int TimesAsked { get; set; }

        public bool? YesNo { get; set; }

        public List<string> Questions { get; } = [];

        public List<string> Said { get; } = [];

        public string? Folder { get; set; }

        public List<string[]> FoldersToProcess { get; } = [];
    }

    private static (MainWindowViewModel Model, Answers Answers) Start(bool twoFaces)
    {
        var answers = new Answers();
        var model = new MainWindowViewModel
        {
            PickImages = multiple =>
            {
                answers.Multiple.Add(multiple);
                return Task.FromResult<IReadOnlyList<string>>(answers.Picks.Count == 0 ? [] : answers.Picks.Dequeue());
            },
            AskFrontsAndBacks = () =>
            {
                answers.TimesAsked++;
                return Task.FromResult(answers.FrontsAndBacks);
            },
            Ask = (question, _) =>
            {
                answers.Questions.Add(question);
                return Task.FromResult(answers.YesNo);
            },
            ShowMessage = message =>
            {
                answers.Said.Add(message);
                return Task.CompletedTask;
            },
            AskFolder = () => Task.FromResult(answers.Folder),
            ShowProcessFolders = folders =>
            {
                answers.FoldersToProcess.Add([.. folders.Folders]);
                return Task.FromResult<bool?>(null);
            },
        };
        model.ImagePanel.IsDouble = twoFaces;
        return (model, answers);
    }

    private static string[] Fronts(MainWindowViewModel model) => [.. model.ImagePanel.Cards.Select(card => Path.GetFileName(card.Card.Front.Path))];

    private static string[] Backs(MainWindowViewModel model) =>
        [.. model.ImagePanel.Cards.Select(card => card.Card.Back.ImageKind == ImageKind.Empty ? "-" : Path.GetFileName(card.Card.Back.Path))];

    /// <summary>Runs a test with the program's settings as they come and puts them back.</summary>
    private void Run(Action test) =>
        fixture.Run(LanguageId.EnglishUk, () =>
        {
            AppSettings.Reset();
            try
            {
                test();
            }
            finally
            {
                AppSettings.Reset();
            }
        });

    [Fact]
    public void Window_gives_the_way_chosen_and_whether_to_keep_it()
    {
        Run(() =>
        {
            var window = new AddImagesWindow();
            window.Show();
            Assert.Equal("Add Images", window.Title);
            Assert.Equal(AddImagesResult.OneFrontOneBack, window.Choice);
            Assert.Equal("Pairs Front/Back (1:1)", window.FindControl<RadioButton>("PairsOption")!.Content);
            Screenshots.Capture(window, "add-images");

            window.FindControl<RadioButton>("ManyFrontsOption")!.IsChecked = true;
            Assert.Equal(AddImagesResult.ManyFrontsOneBack, window.Choice);
            window.FindControl<RadioButton>("ManyBacksOption")!.IsChecked = true;
            Assert.Equal(AddImagesResult.OneFrontManyBacks, window.Choice);
            window.FindControl<RadioButton>("ManyBothOption")!.IsChecked = true;
            Assert.Equal(AddImagesResult.ManyFrontsManyBacks, window.Choice);
            window.Close();
        });
    }

    [Fact]
    public void With_one_face_the_files_are_added_without_questions()
    {
        Run(() =>
        {
            string folder = fixture.NewFolder();
            (MainWindowViewModel model, Answers answers) = Start(twoFaces: false);
            answers.Picks.Enqueue([Picture(folder, "b.png"), Picture(folder, "a.png")]);

            HeadlessFixture.Wait(model.AddImagesAsync());

            Assert.Equal(["a.png", "b.png"], Fronts(model));
            Assert.Equal(0, answers.TimesAsked);
            Assert.Equal([true], answers.Multiple);
            Assert.Empty(answers.Said);
        });
    }

    [Fact]
    public void Many_fronts_and_one_back_are_chosen_one_after_the_other()
    {
        Run(() =>
        {
            string folder = fixture.NewFolder();
            (MainWindowViewModel model, Answers answers) = Start(twoFaces: true);
            answers.FrontsAndBacks = new AddImagesAnswer(AddImagesResult.ManyFrontsOneBack, Remember: false);
            answers.Picks.Enqueue([Picture(folder, "f2.png"), Picture(folder, "f1.png"), Picture(folder, "f3.png")]);
            answers.Picks.Enqueue([Picture(folder, "back.png")]);

            HeadlessFixture.Wait(model.AddImagesAsync());

            Assert.Equal(["f1.png", "f2.png", "f3.png"], Fronts(model));
            Assert.Equal(["back.png", "back.png", "back.png"], Backs(model));
            Assert.Equal([true, false], answers.Multiple);
            Assert.Equal(AddImagesResult.Ask, AppSettings.DefaultAddImagesChoice);
        });
    }

    [Fact]
    public void Pairs_are_chosen_in_one_go_and_an_odd_file_has_no_back()
    {
        Run(() =>
        {
            string folder = fixture.NewFolder();
            (MainWindowViewModel model, Answers answers) = Start(twoFaces: true);
            answers.FrontsAndBacks = new AddImagesAnswer(AddImagesResult.OneFrontOneBack, Remember: false);
            answers.Picks.Enqueue([Picture(folder, "1.png"), Picture(folder, "2.png"), Picture(folder, "3.png")]);

            HeadlessFixture.Wait(model.AddImagesAsync());

            Assert.Equal(["1.png", "3.png"], Fronts(model));
            Assert.Equal(["2.png", "-"], Backs(model));
            Assert.Equal([true], answers.Multiple);
            Assert.Empty(answers.Said);
        });
    }

    [Fact]
    public void Every_front_goes_with_every_back()
    {
        Run(() =>
        {
            string folder = fixture.NewFolder();
            (MainWindowViewModel model, Answers answers) = Start(twoFaces: true);
            answers.FrontsAndBacks = new AddImagesAnswer(AddImagesResult.ManyFrontsManyBacks, Remember: false);
            answers.Picks.Enqueue([Picture(folder, "f1.png"), Picture(folder, "f2.png"), Picture(folder, "f3.png")]);
            answers.Picks.Enqueue([Picture(folder, "b1.png"), Picture(folder, "b2.png")]);

            HeadlessFixture.Wait(model.AddImagesAsync());

            Assert.Equal(["f1.png", "f2.png", "f3.png", "f1.png", "f2.png", "f3.png"], Fronts(model));
            Assert.Equal(["b1.png", "b1.png", "b1.png", "b2.png", "b2.png", "b2.png"], Backs(model));
        });
    }

    [Fact]
    public void Cancelling_the_question_or_either_file_dialog_adds_nothing()
    {
        Run(() =>
        {
            string folder = fixture.NewFolder();
            (MainWindowViewModel model, Answers answers) = Start(twoFaces: true);

            // The question.
            answers.FrontsAndBacks = null;
            HeadlessFixture.Wait(model.AddImagesAsync());
            Assert.Empty(answers.Multiple);

            // The fronts.
            answers.FrontsAndBacks = new AddImagesAnswer(AddImagesResult.OneFrontManyBacks, Remember: true);
            HeadlessFixture.Wait(model.AddImagesAsync());
            Assert.Equal([false], answers.Multiple);

            // The backs.
            answers.Picks.Enqueue([Picture(folder, "front.png")]);
            HeadlessFixture.Wait(model.AddImagesAsync());

            Assert.Empty(model.ImagePanel.Cards);
            Assert.Equal(AddImagesResult.Ask, AppSettings.DefaultAddImagesChoice);  // kept only when it is carried through
        });
    }

    [Fact]
    public void Answer_kept_is_not_asked_again()
    {
        Run(() =>
        {
            string folder = fixture.NewFolder();
            (MainWindowViewModel model, Answers answers) = Start(twoFaces: true);
            answers.FrontsAndBacks = new AddImagesAnswer(AddImagesResult.OneFrontManyBacks, Remember: true);
            answers.Picks.Enqueue([Picture(folder, "front.png")]);
            answers.Picks.Enqueue([Picture(folder, "b1.png"), Picture(folder, "b2.png")]);
            HeadlessFixture.Wait(model.AddImagesAsync());
            Assert.Equal(AddImagesResult.OneFrontManyBacks, AppSettings.DefaultAddImagesChoice);

            answers.Picks.Enqueue([Picture(folder, "other.png")]);
            answers.Picks.Enqueue([Picture(folder, "b3.png")]);
            HeadlessFixture.Wait(model.AddImagesAsync(position: 0));

            Assert.Equal(1, answers.TimesAsked);
            Assert.Equal(["other.png", "front.png", "front.png"], Fronts(model));
            Assert.Equal(["b3.png", "b1.png", "b2.png"], Backs(model));
        });
    }

    [Fact]
    public void Menu_of_the_cards_adds_images_the_same_way()
    {
        Run(() =>
        {
            string folder = fixture.NewFolder();
            (MainWindowViewModel model, Answers answers) = Start(twoFaces: true);
            answers.FrontsAndBacks = new AddImagesAnswer(AddImagesResult.ManyFrontsOneBack, Remember: false);
            answers.Picks.Enqueue([Picture(folder, "f1.png")]);
            answers.Picks.Enqueue([Picture(folder, "back.png")]);

            Assert.NotNull(model.ImagePanel.RequestImages);
            HeadlessFixture.Wait(model.ImagePanel.RequestImages(-1));

            Assert.Equal(1, answers.TimesAsked);
            Assert.Equal(["back.png"], Backs(model));
        });
    }

    [Fact]
    public void Folder_chosen_gives_its_images()
    {
        Run(() =>
        {
            string folder = fixture.NewFolder();
            Picture(folder, "c2.png");
            Picture(folder, "c10.png");
            Picture(folder, "c1.png");
            File.WriteAllText(Path.Combine(folder, "notes.txt"), "not an image");
            (MainWindowViewModel model, Answers answers) = Start(twoFaces: false);

            // Cancelled first.
            HeadlessFixture.Wait(model.AddFolderAsync());
            Assert.Empty(model.ImagePanel.Cards);

            answers.Folder = folder;
            HeadlessFixture.Wait(model.AddFolderAsync());

            Assert.Equal(["c1.png", "c2.png", "c10.png"], Fronts(model));
            Assert.Empty(answers.Said);
        });
    }

    private static string Zip(string folder, string name, params string[] images)
    {
        string source = Path.Combine(folder, "source-" + Path.GetFileNameWithoutExtension(name));
        string path = Path.Combine(folder, name);
        using ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (string image in images)
        {
            zip.CreateEntryFromFile(Picture(source, Path.GetFileName(image)), image);
        }

        return path;
    }

    [Fact]
    public void Compressed_file_chosen_or_dropped_gives_its_images()
    {
        Run(() =>
        {
            string folder = fixture.NewFolder();
            string zip = Zip(folder, "deck.zip", "c2.png", "c1.png", "more/c3.png");
            (MainWindowViewModel model, Answers answers) = Start(twoFaces: false);
            answers.Picks.Enqueue([zip]);

            HeadlessFixture.Wait(model.AddImagesAsync());

            Assert.Equal(["c1.png", "c2.png", "c3.png"], Fronts(model));
            Assert.All(model.ImagePanel.Cards, card => Assert.StartsWith(Path.Combine(folder, "deck") + Path.DirectorySeparatorChar, card.Card.Front.Path));
            Assert.All(model.ImagePanel.Cards, card => Assert.False(card.Card.Front.IsCorrupt));

            (int added, AppError error) = HeadlessFixture.WaitFor(model.ImagePanel.DropFilesAsync([zip, Picture(folder, "loose.png")]));
            Assert.Equal(4, added);
            Assert.False(error.HasError, error.Message);
            Assert.Empty(answers.Said);
        });
    }

    [Fact]
    public void Compressed_file_that_cannot_be_read_is_told_and_the_rest_is_added()
    {
        Run(() =>
        {
            string folder = fixture.NewFolder();
            string broken = Path.Combine(folder, "broken.zip");
            File.WriteAllText(broken, "not a zip");
            (MainWindowViewModel model, Answers answers) = Start(twoFaces: false);
            answers.Picks.Enqueue([broken, Picture(folder, "fine.png")]);

            HeadlessFixture.Wait(model.AddImagesAsync());

            Assert.Equal(["fine.png"], Fronts(model));
            Assert.Contains("broken.zip", Assert.Single(answers.Said));

            (int added, AppError error) = HeadlessFixture.WaitFor(model.ImagePanel.DropFilesAsync([broken]));
            Assert.Equal(0, added);
            Assert.True(error.HasError);
        });
    }

    [Fact]
    public void A_great_many_images_offer_the_folders_tool_first()
    {
        Run(() =>
        {
            string folder = fixture.NewFolder();
            string[] files = [Picture(folder, "1.png"), Picture(folder, "2.png"), Picture(folder, "3.png")];
            (MainWindowViewModel model, Answers answers) = Start(twoFaces: false);
            AppSettings.PerformanceWarningImageCount = 2;

            // Cancel: nothing happens.
            answers.Picks.Enqueue(files);
            answers.YesNo = null;
            HeadlessFixture.Wait(model.AddImagesAsync());
            Assert.Empty(model.ImagePanel.Cards);
            Assert.Contains("process folder", Assert.Single(answers.Questions));

            // Yes: the tool, starting with the folder of the images.
            answers.Picks.Enqueue(files);
            answers.YesNo = true;
            HeadlessFixture.Wait(model.AddImagesAsync());
            Assert.Empty(model.ImagePanel.Cards);
            Assert.Equal([folder], Assert.Single(answers.FoldersToProcess));

            // No: the images are added.
            answers.Picks.Enqueue(files);
            answers.YesNo = false;
            HeadlessFixture.Wait(model.AddImagesAsync());
            Assert.Equal(3, model.ImagePanel.Cards.Count);

            // Up to the number set there is no question.
            int asked = answers.Questions.Count;
            answers.Picks.Enqueue(files[..2]);
            HeadlessFixture.Wait(model.AddImagesAsync());
            Assert.Equal(5, model.ImagePanel.Cards.Count);
            Assert.Equal(asked, answers.Questions.Count);
        });
    }

    [Fact]
    public void Names_asking_for_too_many_copies_are_told_before_adding()
    {
        Run(() =>
        {
            string folder = fixture.NewFolder();
            (MainWindowViewModel model, Answers answers) = Start(twoFaces: false);
            answers.Picks.Enqueue([Picture(folder, "dragon x80.png"), Picture(folder, "elf x2.png"), Picture(folder, "orc x60.png")]);

            HeadlessFixture.Wait(model.AddImagesAsync());

            string said = Assert.Single(answers.Said);
            Assert.StartsWith("Modify # copies", said);
            Assert.Contains("dragon x80.png # >50", said);
            Assert.Contains("orc x60.png # >50", said);
            Assert.DoesNotContain("elf", said);
            Assert.Equal(3, model.ImagePanel.Cards.Count);
        });
    }

    [Fact]
    public void Images_in_another_colour_space_than_the_project_are_told_once()
    {
        Run(() =>
        {
            string folder = fixture.NewFolder();
            (MainWindowViewModel model, Answers answers) = Start(twoFaces: false);
            model.ProjectSettings.Settings.Other.ColorSpace = PnpTool.Core.Projects.ProjectColorSpace.CMYK;

            // The images of the tests are RGB.
            HeadlessFixture.Wait(model.ImagePanel.AddImagesAsync([Picture(folder, "1.png"), Picture(folder, "2.png")]));
            HeadlessFixture.Wait(model.ImagePanel.AddImagesAsync([Picture(folder, "3.png")]));

            Assert.StartsWith("There are RGB Images while the project is CMYK", Assert.Single(answers.Said));
            Assert.Equal(3, model.ImagePanel.Cards.Count);
        });
    }
}
