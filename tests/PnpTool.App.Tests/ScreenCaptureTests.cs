using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ImageMagick;
using PnpTool.App.ViewModels;
using PnpTool.App.ViewModels.Ribbon;
using PnpTool.App.Views;
using PnpTool.Controls;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Primitives;

namespace PnpTool.App.Tests;

[Collection(UserInterface.Name)]
public sealed class ScreenCaptureTests(HeadlessFixture fixture)
{
    /// <summary>
    /// A screen that is red on its left half and blue on its right half; pictures are of the
    /// size of the area asked for. It can be told to fail.
    /// </summary>
    private sealed class FakeScreen : IScreenCapture
    {
        public List<ScreenArea> Areas { get; } = [];

        public string? Failure { get; set; }

        public AppError Capture(ScreenArea area, string file)
        {
            if (Failure != null)
            {
                return new AppError { Kind = ErrorKind.Error, Message = Failure };
            }

            Areas.Add(area);
            using var image = new MagickImage(new MagickColor("#FF0000"), (uint)area.Width, (uint)area.Height);
            using var right = new MagickImage(new MagickColor("#0000FF"), (uint)Math.Max(1, area.Width / 2), (uint)area.Height);
            image.Composite(right, area.Width / 2, 0, CompositeOperator.Over);
            image.Format = Path.GetExtension(file) == ".png" ? MagickFormat.Png : MagickFormat.Jpeg;
            image.Write(file);
            return new AppError();
        }
    }

    private static readonly ScreenArea[] TwoScreens = [new(0, 0, 800, 600, 1.0), new(800, 0, 400, 300, 2.0)];

    [Fact]
    public void The_area_is_the_whole_screen_at_first_and_never_leaves_it()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var capture = new ScreenCaptureViewModel(new FakeScreen());
            Assert.Null(capture.Area);
            capture.SetScreens(TwoScreens);

            Assert.Equal(["#0", "#1"], capture.ScreenNames);
            Assert.Equal(("800", "600", "0", "0"), (capture.WidthText, capture.HeightText, capture.OffsetXText, capture.OffsetYText));
            Assert.Equal(new ScreenArea(0, 0, 800, 600, 1.0), capture.Area);

            capture.OffsetXText = "100";                 // the width no longer fits
            capture.OffsetYText = "50";
            Assert.Equal(new ScreenArea(100, 50, 700, 550, 1.0), capture.Area);
            capture.WidthText = "200";
            capture.HeightText = "";
            Assert.Equal(new ScreenArea(100, 50, 200, 0, 1.0), capture.Area);
            Assert.Equal("La zona a capturar no tiene tamaño", capture.Problem());

            capture.SelectedScreen = 1;                  // back to the whole of the other screen
            Assert.Equal(new ScreenArea(800, 0, 400, 300, 2.0), capture.Area);
            capture.OffsetXText = "150";
            capture.WidthText = "100";
            Assert.Equal(new ScreenArea(950, 0, 100, 300, 2.0), capture.Area);
        });
    }

    [Fact]
    public void A_picture_is_taken_every_interval_and_numbered_after_those_in_the_folder()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            File.WriteAllText(Path.Combine(folder, "Capture_1.jpg"), "already there");
            var screen = new FakeScreen();
            var capture = new ScreenCaptureViewModel(screen) { Folder = string.Empty };
            var notices = new List<string>();
            capture.Notice += notices.Add;
            capture.SetScreens(TwoScreens);
            capture.WidthText = "200";
            capture.HeightText = "100";

            Assert.False(capture.Start());               // no folder
            Assert.Equal([StringResources.Get("Form_Dice7Pixels_Error_CarpetaDestino")], notices);
            capture.Tick();
            Assert.Empty(screen.Areas);

            capture.Folder = Path.Combine(folder, "nueva", "..");
            capture.Folder = folder;
            capture.IntervalText = "4";
            Assert.True(capture.Start());
            Assert.True(capture.IsRunning);

            var lights = new List<bool>();
            for (int second = 0; second < 8; second++)
            {
                capture.Tick();
                lights.Add(capture.AboutToCapture);
            }

            // Green for a second, red for two, the picture on the fourth; twice.
            Assert.Equal([false, true, true, false, false, true, true, false], lights);
            Assert.Equal(2, screen.Areas.Count);
            Assert.Equal(["Capture_0.jpg", "Capture_2.jpg"], capture.Taken.Select(Path.GetFileName));
            Assert.Equal("already there", File.ReadAllText(Path.Combine(folder, "Capture_1.jpg")));
            Assert.StartsWith("Capture: 2 T", capture.Status);

            capture.Stop();
            Assert.False(capture.IsRunning);
            Assert.EndsWith(" -- 2", capture.Status);
            capture.Tick();
            Assert.Equal(2, screen.Areas.Count);

            Assert.Empty(capture.ToImport);
            capture.Import = true;
            Assert.Equal(capture.Taken, capture.ToImport);
        });
    }

    [Fact]
    public void A_capture_that_fails_stops_the_run_and_a_preview_that_fails_only_says_so()
    {
        fixture.Run(LanguageId.EnglishUk, () =>
        {
            var screen = new FakeScreen { Failure = "not allowed" };
            var capture = new ScreenCaptureViewModel(screen) { Folder = fixture.NewFolder(), IntervalText = "1" };
            var notices = new List<string>();
            capture.Notice += notices.Add;
            capture.SetScreens(TwoScreens);

            Assert.False(capture.Preview());
            Assert.Equal("not allowed", capture.Status);
            Assert.Empty(notices);

            Assert.True(capture.Start());
            capture.Tick();
            Assert.False(capture.IsRunning);
            Assert.Equal(["not allowed"], notices);
            Assert.Empty(capture.Taken);

            screen.Failure = null;
            string? shown = null;
            capture.PreviewTaken += file => shown = file;
            Assert.True(capture.Preview());
            Assert.True(File.Exists(shown));
            Assert.Empty(capture.Taken);                 // a preview is not a capture
        });
    }

    [Fact]
    public void Window_shows_the_preview_and_the_tools_tab_adds_the_pictures_to_the_panel()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            var model = new ScreenCaptureViewModel(new FakeScreen()) { Folder = folder };
            model.SetScreens(TwoScreens);
            var window = new ScreenCaptureWindow(model);
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("AutoCaptura", window.Title);
            Assert.NotNull(window.FindControl<Image>("PreviewPicture")!.Source);
            Assert.False(window.FindControl<Button>("StopButton")!.IsEnabled);
            Assert.Equal("Intervalo (s)", model.IntervalLabel);
            model.Start();
            model.Tick();
            model.Tick();
            Dispatcher.UIThread.RunJobs();
            Assert.True(window.FindControl<Button>("StopButton")!.IsEnabled);
            Assert.False(window.FindControl<Border>("Light")!.Classes.Contains("red"));
            Screenshots.Capture(window, "ventana-autocaptura");
            window.Close();
            Assert.False(model.IsRunning);
            Assert.Single(model.Taken);

            var main = new MainWindowViewModel();
            main.ProjectSettings.SelectedGroup = main.ProjectSettings.Groups.First(option => option.Group == SettingsGroup.Cards);
            main.ShowScreenCapture = capture =>
            {
                capture.SetScreens(TwoScreens);
                capture.Folder = folder;
                capture.Import = true;
                Assert.False(capture.CaptureNow());      // the real screen is not asked in a test…
                return Task.CompletedTask;
            };
            RibbonButtonViewModel button = main.Tabs[3].Groups.SelectMany(group => group.Buttons)
                .Single(candidate => candidate.Key == "ShellMenuBarItem_Herramientas_AutoCaptura");
            Assert.NotNull(button.Icon);
            IScreenCapture original = ScreenCapture.Current;
            try
            {
                ScreenCapture.Current = new FakeScreen { Failure = "no" };
                button.Command.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(0, main.ImagePanel.PanelCount);

                ScreenCapture.Current = new FakeScreen();
                main.ShowScreenCapture = capture =>
                {
                    capture.SetScreens(TwoScreens);
                    capture.Folder = folder;
                    capture.Import = true;
                    Assert.True(capture.CaptureNow());
                    Assert.True(capture.CaptureNow());
                    return Task.CompletedTask;
                };
                Task run = main.ScreenCaptureAsync();
                HeadlessFixture.Wait(run);
                Assert.Equal(2, main.ImagePanel.PanelCount);
            }
            finally
            {
                ScreenCapture.Current = original;
            }
        });
    }

    [Fact]
    public void Eyedropper_gives_the_colour_under_a_point_of_the_picture()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string picture = Path.Combine(fixture.NewFolder(), "pantalla.png");
            new FakeScreen().Capture(new ScreenArea(0, 0, 400, 200), picture);
            var window = new EyeDropperWindow(picture);
            window.Show();
            Dispatcher.UIThread.RunJobs();
            double width = window.Bounds.Width;
            double height = window.Bounds.Height;

            Assert.Equal(new PixelPoint(100, 100), window.PictureAt(new Point(width / 4, height / 2)));
            Assert.Equal(new ArgbColor(255, 0, 0), window.ColorAt(new Point(width / 4, height / 2)));
            Assert.Equal(new ArgbColor(0, 0, 255), window.ColorAt(new Point(width * 3 / 4, height / 2)));
            Assert.Equal(new PixelPoint(399, 199), window.PictureAt(new Point(width + 50, height + 50)));

            window.MoveTo(new Point(width * 3 / 4, height / 2));
            Assert.Equal(new ArgbColor(0, 0, 255), window.Current);
            Screenshots.Capture(window, "cuentagotas");

            ArgbColor? picked = new ArgbColor(1, 2, 3);
            window.Closed += (_, _) => picked = window.Current;
            window.Pick(new Point(width * 3 / 4, 5));
            Dispatcher.UIThread.RunJobs();
            Assert.False(window.IsVisible);
        });
    }

    [Fact]
    public void Colour_window_takes_the_colour_picked_from_the_screen()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            IScreenCapture original = ScreenCapture.Current;
            try
            {
                var screen = new FakeScreen();
                ScreenCapture.Current = screen;
                var dialog = new ColorPickerDialog(new ArgbColor(10, 20, 30));
                dialog.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.NotNull(dialog.FindControl<Button>("EyeDropperButton"));

                string? shown = null;
                dialog.FindScreen = () => new ScreenArea(800, 0, 400, 300, 2.0);
                dialog.PickFromPicture = (picture, area) =>
                {
                    shown = picture;
                    Assert.True(File.Exists(picture));
                    Assert.Equal(800, area.X);
                    return Task.FromResult<ArgbColor?>(new ArgbColor(0, 0, 255));
                };

                Task<bool> picking = dialog.PickFromScreenAsync();
                HeadlessFixture.Wait(picking);
                Assert.True(picking.Result);
                Assert.Equal(new ArgbColor(0, 0, 255), dialog.Color);
                Assert.Equal([new ScreenArea(800, 0, 400, 300, 2.0)], screen.Areas);
                Assert.False(File.Exists(shown));         // the picture of the screen is not kept

                dialog.PickFromPicture = (_, _) => Task.FromResult<ArgbColor?>(null);   // Escape
                picking = dialog.PickFromScreenAsync();
                HeadlessFixture.Wait(picking);
                Assert.False(picking.Result);
                Assert.Equal(new ArgbColor(0, 0, 255), dialog.Color);

                screen.Failure = "PNP Tool is not allowed to record the screen";
                picking = dialog.PickFromScreenAsync();
                HeadlessFixture.Wait(picking);
                Assert.False(picking.Result);
                Assert.Equal(screen.Failure, dialog.FindControl<TextBlock>("EyeDropperMessage")!.Text);
                Screenshots.Capture(dialog, "ventana-color-cuentagotas");
                dialog.Close();
            }
            finally
            {
                ScreenCapture.Current = original;
            }
        });
    }
}
