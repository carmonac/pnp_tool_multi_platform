using Avalonia.Controls;
using Avalonia.Threading;
using ImageMagick;
using PnpTool.App.ViewModels;
using PnpTool.App.ViewModels.Ribbon;
using PnpTool.App.Views;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Pdf.Tools;

namespace PnpTool.App.Tests;

[Collection(UserInterface.Name)]
public sealed class PosterTests(HeadlessFixture fixture)
{
    /// <summary>A picture of 20 by 30 cm at 100 dots per inch.</summary>
    private static string Picture(string folder)
    {
        string path = Path.Combine(folder, "cartel.png");
        using var image = new MagickImage(new MagickColor("#3070B0"), 787, 1181) { Format = MagickFormat.Png };
        image.Density = new Density(100, 100, DensityUnit.PixelsPerInch);
        image.Write(path);
        return path;
    }

    [Fact]
    public void Summary_follows_the_settings_and_says_what_is_missing()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            MeasurementSettings.Current = UnitType.Millimeter;
            var poster = new PosterViewModel();
            Assert.Equal("No hay ninguna imagen elegida", poster.Summary);
            Assert.Null(poster.Plan);
            Assert.Equal(poster.Summary, poster.Problem());

            int changes = 0;
            poster.PlanChanged += () => changes++;
            poster.ImagePath = Picture(fixture.NewFolder());

            // A4 with 5 mm of margin prints 200 × 287; two sheets with 10 of overlap give 390.
            Assert.True(changes > 0);
            Assert.NotNull(poster.Plan);
            Assert.Equal((2, 3), (poster.Plan.Columns, poster.Plan.Rows));
            Assert.Equal(390.0, Unit.FromPoint(poster.Plan.PosterWidth).Millimeter, 2);
            Assert.StartsWith("2 × 3 = 6 hojas", poster.Summary);
            Assert.Null(poster.Problem());

            poster.Paper.Landscape = true;                       // 287 × 200 printed
            Assert.Equal((2, 5), (poster.Plan!.Columns, poster.Plan.Rows));

            poster.Paper.Landscape = false;
            poster.SelectedSizeMode = (int)PosterSizeMode.Width; // keeps the poster it had
            Assert.Equal(390.0, Unit.FromDisplayString(poster.SizeText).Millimeter, 1);
            Assert.Equal((2, 3), (poster.Plan!.Columns, poster.Plan.Rows));
            poster.SizeText = "150";
            Assert.Equal((1, 1), (poster.Plan!.Columns, poster.Plan.Rows));

            poster.SelectedSizeMode = (int)PosterSizeMode.Percent;
            Assert.Equal("100", poster.SizeText);
            Assert.Equal(200.0, Unit.FromPoint(poster.Plan!.PosterWidth).Millimeter, 0);
            poster.SizeText = "300";                              // 600 wide: 200 + 190 + 190 is 20 short
            Assert.Equal(4, poster.Plan!.Columns);

            poster.OverlapText = "400";
            Assert.Null(poster.Plan);
            Assert.Equal("El solape debe ser menor que la zona impresa de la hoja", poster.Summary);
        });
    }

    [Fact]
    public void Window_makes_the_poster_and_opens_it()
    {
        var opened = new List<string>();
        IPlatformServices original = Platform.Services;
        try
        {
            Platform.Services = new Recording(opened);
            fixture.Run(LanguageId.Spanish, () =>
            {
                MeasurementSettings.Current = UnitType.Millimeter;
                string folder = fixture.NewFolder();
                string file = Path.Combine(folder, "poster.pdf");
                var window = new PosterWindow();
                window.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("Poster", window.Title);
                Assert.True(window.FindControl<TextBlock>("ImageHint")!.IsVisible);
                Assert.Equal("Papel", window.FindControl<TextBlock>("PaperLabel")!.Text);

                int asked = 0;
                window.AskFile = () =>
                {
                    asked++;
                    return Task.FromResult<string?>(file);
                };
                Task<bool> none = window.CreateAsync();
                HeadlessFixture.Wait(none);
                Assert.False(none.Result);                         // no image yet
                Assert.Equal(0, asked);

                window.Model.ImagePath = Picture(folder);
                Dispatcher.UIThread.RunJobs();
                Assert.False(window.FindControl<TextBlock>("ImageHint")!.IsVisible);
                Assert.Same(window.Model.Plan, window.FindControl<PosterPreview>("Preview")!.Plan);
                Screenshots.Capture(window, "ventana-poster");

                Task<bool> made = window.CreateAsync();
                HeadlessFixture.Wait(made);
                Assert.True(made.Result);
                Assert.Equal(6, PdfRasterizer.PageCount(file));
                Assert.Equal(0.0, window.Model.Progress);
                window.Close();
            });
        }
        finally
        {
            Platform.Services = original;
        }

        Assert.Single(opened);
        Assert.EndsWith("poster.pdf", opened[0]);
    }

    [Fact]
    public void Tools_tab_has_the_poster_button()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var model = new MainWindowViewModel();
            PosterViewModel? shown = null;
            model.ShowPoster = poster =>
            {
                shown = poster;
                return Task.CompletedTask;
            };

            RibbonButtonViewModel button = model.Tabs[3].Groups.SelectMany(group => group.Buttons)
                .Single(candidate => candidate.Key == MainWindowViewModel.PosterButtonKey);
            Assert.Equal("Poster", button.Label);
            Assert.NotNull(button.Icon);
            button.Command.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(shown);
        });
    }

    private sealed class Recording(List<string> opened) : IPlatformServices
    {
        public void OpenUrl(string url) => opened.Add(url);

        public void OpenFile(string path) => opened.Add(path);

        public void OpenFolder(string path) => opened.Add(path);

        public void RevealFile(string path) => opened.Add(path);
    }
}
