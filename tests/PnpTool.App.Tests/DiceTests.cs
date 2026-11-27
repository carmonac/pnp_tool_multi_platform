using Avalonia.Controls;
using Avalonia.Threading;
using ImageMagick;
using PnpTool.App.ViewModels;
using PnpTool.App.Views;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Projects;
using PnpTool.Pdf.Tabletop;

namespace PnpTool.App.Tests;

/// <summary>Templates of "seven pixels" dice from the main window.</summary>
[Collection(UserInterface.Name)]
public sealed class DiceTests(HeadlessFixture fixture)
{
    private static string Picture(string folder, string name, string color = "#3060C0")
    {
        string path = Path.Combine(folder, name);
        using var image = new MagickImage(new MagickColor(color), 60, 60);
        image.Write(path);
        return path;
    }

    private sealed class Recording(List<string> opened) : IPlatformServices
    {
        public void OpenUrl(string url) => opened.Add("url " + url);

        public void OpenFile(string path) => opened.Add("file " + path);

        public void OpenFolder(string path) => opened.Add("folder " + path);

        public void RevealFile(string path) => opened.Add("reveal " + path);
    }

    private void Run(Action<List<string>> test) =>
        fixture.Run(LanguageId.EnglishUk, () =>
        {
            IPlatformServices services = Platform.Services;
            var opened = new List<string>();
            Platform.Services = new Recording(opened);
            try
            {
                test(opened);
            }
            finally
            {
                AppSettings.LastFolder = string.Empty;
                Platform.Services = services;
            }
        });

    [Fact]
    public void Creating_the_output_of_a_dice_project_writes_a_template_for_each_die()
    {
        Run(opened =>
        {
            string folder = fixture.NewFolder();
            string output = fixture.NewFolder();
            var said = new List<string>();
            var model = new MainWindowViewModel
            {
                ShowMessage = message =>
                {
                    said.Add(message);
                    return Task.CompletedTask;
                },
            };
            ProjectSettingsViewModel project = model.ProjectSettings;
            project.SelectedGroup = project.Groups.First(option => option.Group == SettingsGroup.Dice7Pixel);
            project.SelectedStyle = project.Styles.First(style => style.Text == "D6");
            Assert.Equal(ProjectType.Dice7Pixel, project.Settings.Group.ProjectType);
            Assert.Equal(4, project.Settings.Group.SubgroupIndex);
            DiceViewModel? asked = null;
            model.AskDice = request =>
            {
                asked = request;
                request.Folder = output;
                request.FileName = "cube";
                return Task.FromResult<string?>(request.Result());
            };

            // Without images there is nothing to write.
            Assert.Null(HeadlessFixture.WaitFor(model.CreatePdfAsync(preview: false)));
            Assert.Equal("There are no images", Assert.Single(said));
            Assert.Null(asked);

            // Seven faces: a cube and the beginning of another.
            HeadlessFixture.Wait(model.ImagePanel.AddImagesAsync([Picture(folder, "one x2.png"), .. Enumerable.Range(2, 5).Select(i => Picture(folder, "f" + i + ".png"))]));
            Assert.Null(HeadlessFixture.WaitFor(model.CreatePdfAsync(preview: false)));

            Assert.NotNull(asked);
            Assert.StartsWith("D6   Image: 7 / 6   (2)", asked.Summary);
            Assert.Equal(["cube_0.png", "cube_1.png"], Directory.GetFiles(output).Select(Path.GetFileName).Order());
            Assert.Equal(["There are no images", "Process completed"], said);
            Assert.Equal(["folder " + output], opened);
            using (var template = new MagickImage(Path.Combine(output, "cube_0.png")))
            {
                Assert.Equal((uint)SevenPixelDice.Side, template.Width);
            }

            // Cancelling the window writes nothing.
            model.AskDice = _ => Task.FromResult<string?>(null);
            HeadlessFixture.Wait(model.CreatePdfAsync(preview: false));
            Assert.Single(opened);
            Assert.Equal(2, said.Count);
        });
    }

    [Fact]
    public void Window_asks_for_what_is_missing_and_gives_where_to_write()
    {
        Run(_ =>
        {
            var owner = new Window();
            owner.Show();
            var window = new DiceWindow(new DiceViewModel(new SevenPixelDice([], SevenPixelDie.D20, AppSettings.ColorWhite)));
            var said = new List<string>();
            window.ShowMessage = message =>
            {
                said.Add(message);
                return Task.CompletedTask;
            };
            Task<string?> result = window.ShowDialog<string?>(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Dice 7 Pixel", window.Title);
            Assert.Equal("Output", window.FindControl<TextBox>("FileNameBox")!.Text);
            Assert.StartsWith("D20", window.FindControl<TextBlock>("SummaryBlock")!.Text);

            HeadlessFixture.Wait(window.AcceptAsync());
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Data missing", Assert.Single(said));
            Assert.False(result.IsCompleted);

            string folder = fixture.NewFolder();
            window.PickFolder = () => Task.FromResult<string?>(folder);
            HeadlessFixture.Wait(window.PickFolderAsync());
            Dispatcher.UIThread.RunJobs();
            Screenshots.Capture(window, "ventana-dados");
            HeadlessFixture.Wait(window.AcceptAsync());
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Path.Combine(folder, "Output"), result.Result);

            window = new DiceWindow(new DiceViewModel(new SevenPixelDice([], SevenPixelDie.D20, AppSettings.ColorWhite)));
            result = window.ShowDialog<string?>(owner);
            window.FindControl<PnpTool.Controls.OkCancelButtons>("Buttons")!.CancelButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Null(result.Result);
            owner.Close();
        });
    }
}
