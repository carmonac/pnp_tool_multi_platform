using Avalonia.Controls;
using Avalonia.Threading;
using ImageMagick;
using PnpTool.App.ViewModels;
using PnpTool.App.Views;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Pdf.Tabletop;

namespace PnpTool.App.Tests;

/// <summary>Sheets for Tabletop Simulator from the main window and from folders.</summary>
[Collection(UserInterface.Name)]
public sealed class TabletopTests(HeadlessFixture fixture)
{
    private static string Picture(string folder, string name, string color = "#3060C0")
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, name);
        using var image = new MagickImage(new MagickColor(color), 60, 84);
        image.Density = new Density(300);
        image.Write(path);
        return path;
    }

    /// <summary>Runs a test with the program's settings as they come and nothing opened on the machine.</summary>
    private void Run(Action<List<string>> test) =>
        fixture.Run(LanguageId.EnglishUk, () =>
        {
            IPlatformServices services = Platform.Services;
            var opened = new List<string>();
            Platform.Services = new Recording(opened);
            int dpi = AppSettings.TargetDpi;
            AppSettings.TargetDpi = 72;      // small sheets, quickly made
            try
            {
                test(opened);
            }
            finally
            {
                AppSettings.TargetDpi = dpi;
                AppSettings.LastFolder = string.Empty;
                Platform.Services = services;
            }
        });

    private sealed class Recording(List<string> opened) : IPlatformServices
    {
        public void OpenUrl(string url) => opened.Add("url " + url);

        public void OpenFile(string path) => opened.Add("file " + path);

        public void OpenFolder(string path) => opened.Add("folder " + path);

        public void RevealFile(string path) => opened.Add("reveal " + path);
    }

    private static MainWindowViewModel Tabletop(List<string> said)
    {
        var model = new MainWindowViewModel
        {
            ShowMessage = message =>
            {
                said.Add(message);
                return Task.CompletedTask;
            },
        };
        ProjectSettingsViewModel project = model.ProjectSettings;
        project.SelectedGroup = project.Groups.First(option => option.Group == SettingsGroup.Tabletop);
        Assert.Equal(ProjectType.Tabletop, project.Settings.Group.ProjectType);
        project.Settings.Grid.CardSize = new UnitPoint { X = Unit.FromMillimeter(63.5), Y = Unit.FromMillimeter(88.9) };
        return model;
    }

    [Fact]
    public void Table_follows_what_is_typed_within_the_limits()
    {
        Run(_ =>
        {
            var settings = new ProjectSettings();
            settings.Grid.CardSize = new UnitPoint { X = Unit.FromMillimeter(63.5), Y = Unit.FromMillimeter(88.9) };
            var sheets = new TabletopSheets([], false, settings);
            var model = new TabletopViewModel(sheets, settings.Grid.CardSize);
            Assert.Equal("10", model.ColumnsText);
            Assert.Equal("7", model.RowsText);
            Assert.True(model.ApplyRestrictions);
            Assert.StartsWith("63.5 x 88.9 mm   10 x 7   Image: 1800 x 1764 px", model.Summary);

            model.ColumnsText = "25";
            model.RowsText = "1";
            Assert.Equal(10, model.Columns);
            Assert.Equal(2, model.Rows);
            model.ApplyRestrictions = false;
            Assert.Equal(25, model.Columns);
            Assert.Equal(1, model.Rows);
            Assert.Contains("25 x 1", model.Summary);

            Assert.Equal("Data missing", model.Problem());
            string folder = fixture.NewFolder();
            model.Folder = folder;
            Assert.NotNull(model.Problem());
            model.FileName = " deck ";
            Assert.Null(model.Problem());
            Assert.Equal(Path.Combine(folder, "deck"), model.Result());
            Assert.Equal(25, sheets.Columns);
            Assert.False(sheets.ApplyRestrictions);
        });
    }

    [Fact]
    public void Creating_the_output_of_a_tabletop_project_writes_its_sheets()
    {
        Run(opened =>
        {
            string folder = fixture.NewFolder();
            string output = fixture.NewFolder();
            var said = new List<string>();
            MainWindowViewModel model = Tabletop(said);
            TabletopViewModel? asked = null;
            model.AskTabletop = request =>
            {
                asked = request;
                request.ColumnsText = "2";
                request.RowsText = "2";
                request.Folder = output;
                request.FileName = "deck";
                return Task.FromResult<string?>(request.Result());
            };

            // Without cards there is nothing to write.
            Assert.Null(HeadlessFixture.WaitFor(model.CreatePdfAsync(preview: false)));
            Assert.Equal("There are no images", Assert.Single(said));
            Assert.Null(asked);

            HeadlessFixture.Wait(model.ImagePanel.AddImagesAsync([Picture(folder, "a x2.png", "#FF0000"), Picture(folder, "b.png", "#00FF00"), Picture(folder, "c.png"), Picture(folder, "d.png")]));
            Assert.Null(HeadlessFixture.WaitFor(model.CreatePdfAsync(preview: false)));

            Assert.NotNull(asked);
            Assert.Single(said);
            Assert.Equal(["deck_0_front.png", "deck_1_front.png"], Directory.GetFiles(output).Select(Path.GetFileName).Order());
            Assert.Equal(["folder " + output], opened);
            Assert.Equal(4, model.ImagePanel.Cards.Count);

            // Cancelling the window writes nothing.
            model.AskTabletop = _ => Task.FromResult<string?>(null);
            HeadlessFixture.Wait(model.CreatePdfAsync(preview: false));
            Assert.Single(opened);
        });
    }

    [Fact]
    public void Cards_without_size_cannot_be_laid_out()
    {
        Run(opened =>
        {
            string folder = fixture.NewFolder();
            var said = new List<string>();
            MainWindowViewModel model = Tabletop(said);
            model.ProjectSettings.Settings.Grid.CardSize = new UnitPoint();
            bool asked = false;
            model.AskTabletop = _ =>
            {
                asked = true;
                return Task.FromResult<string?>(null);
            };
            HeadlessFixture.Wait(model.ImagePanel.AddImagesAsync([Picture(folder, "a.png")]));

            Assert.Empty(HeadlessFixture.WaitFor(model.MakeTabletopAsync(model.CurrentProject())));

            Assert.Equal("Data missing", Assert.Single(said));
            Assert.False(asked);
            Assert.Empty(opened);
        });
    }

    [Fact]
    public void Cards_of_two_faces_give_sheets_of_fronts_and_of_backs()
    {
        Run(_ =>
        {
            string folder = fixture.NewFolder();
            string output = fixture.NewFolder();
            MainWindowViewModel model = Tabletop([]);
            model.ImagePanel.IsDouble = true;
            model.ProjectSettings.Settings.DoublePanel = true;
            model.AskTabletop = request =>
            {
                request.Folder = output;
                request.FileName = "two";
                return Task.FromResult<string?>(request.Result());
            };
            HeadlessFixture.Wait(model.ImagePanel.AddImagesAsync([Picture(folder, "1.png"), Picture(folder, "2.png"), Picture(folder, "3.png"), Picture(folder, "4.png")]));

            List<string> files = HeadlessFixture.WaitFor(model.MakeTabletopAsync(model.CurrentProject(), "offered"));

            Assert.Equal(["two_0_back.png", "two_0_front.png"], files.Select(Path.GetFileName));
            Assert.All(files, file => Assert.True(File.Exists(file)));
        });
    }

    [Fact]
    public void Folders_of_a_tabletop_project_give_sheets_named_after_them()
    {
        Run(opened =>
        {
            string root = fixture.NewFolder();
            string output = fixture.NewFolder();
            string elves = Path.Combine(root, "elves");
            string orcs = Path.Combine(root, "orcs");
            Picture(elves, "1.png");
            Picture(elves, "2.png");
            Picture(orcs, "1.png");
            var said = new List<string>();
            MainWindowViewModel model = Tabletop(said);
            var questions = new List<string>();
            model.Ask = (question, _) =>
            {
                questions.Add(question);
                return Task.FromResult<bool?>(true);
            };
            model.ShowProcessFolders = folders =>
            {
                folders.Folders.Add(elves);
                folders.Folders.Add(orcs);
                folders.OnePdfPerFolder = true;
                return Task.FromResult<bool?>(true);
            };
            var offered = new List<string>();
            model.AskTabletop = request =>
            {
                offered.Add(request.FileName);
                request.Folder = output;
                return Task.FromResult<string?>(request.Result());
            };

            List<string> made = HeadlessFixture.WaitFor(model.ProcessFoldersAsync());

            Assert.Empty(said);
            Assert.Empty(questions);      // no layout to confirm, no PDF to name
            Assert.Equal(["elves", "orcs"], offered);
            Assert.Equal(["elves_0_front.png", "orcs_0_front.png"], made.Select(Path.GetFileName));
            Assert.Equal(2, opened.Count);
        });
    }

    [Fact]
    public void Window_asks_for_what_is_missing_and_gives_where_to_write()
    {
        Run(_ =>
        {
            var owner = new Window();
            owner.Show();
            var settings = new ProjectSettings();
            settings.Grid.CardSize = new UnitPoint { X = Unit.FromMillimeter(63.5), Y = Unit.FromMillimeter(88.9) };
            var window = new TabletopWindow(new TabletopViewModel(new TabletopSheets([], false, settings), settings.Grid.CardSize));
            var said = new List<string>();
            window.ShowMessage = message =>
            {
                said.Add(message);
                return Task.CompletedTask;
            };
            Task<string?> result = window.ShowDialog<string?>(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Export to Tabletop Simulator", window.Title);
            Assert.Equal("Tabletop Restrictions", window.FindControl<CheckBox>("RestrictionsCheck")!.Content);
            Assert.Equal("10", window.FindControl<TextBox>("ColumnsBox")!.Text);

            HeadlessFixture.Wait(window.AcceptAsync());
            Dispatcher.UIThread.RunJobs();
            Assert.Single(said);
            Assert.False(result.IsCompleted);

            string folder = fixture.NewFolder();
            window.PickFolder = () => Task.FromResult<string?>(folder);
            HeadlessFixture.Wait(window.PickFolderAsync());
            window.Model.FileName = "deck";
            window.Model.RowsText = "3";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(folder, window.FindControl<TextBox>("FolderBox")!.Text);
            Assert.Contains("10 x 3", window.FindControl<TextBlock>("SummaryBlock")!.Text);
            Screenshots.Capture(window, "ventana-tabletop");
            HeadlessFixture.Wait(window.AcceptAsync());
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Path.Combine(folder, "deck"), result.Result);

            window = new TabletopWindow(new TabletopViewModel(new TabletopSheets([], false, settings), settings.Grid.CardSize));
            result = window.ShowDialog<string?>(owner);
            window.FindControl<PnpTool.Controls.OkCancelButtons>("Buttons")!.CancelButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Null(result.Result);
            owner.Close();
        });
    }
}
