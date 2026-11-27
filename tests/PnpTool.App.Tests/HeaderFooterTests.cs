using Avalonia.Controls;
using Avalonia.Threading;
using PnpTool.App.ViewModels;
using PnpTool.App.Views;
using PnpTool.Core;
using PnpTool.Core.Localization;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.App.Tests;

/// <summary>The window of the colour bar, registration marks, signature, page number and watermark.</summary>
[Collection(UserInterface.Name)]
public sealed class HeaderFooterTests(HeadlessFixture fixture)
{
    private static HeaderFooter Header() => new()
    {
        ShowCalibrationImage = true,
        CalibrationImageSize = new UnitPoint { X = Unit.FromPoint(120), Y = Unit.FromPoint(14.5) },
        ShowCalibrationMark = true,
        CalibrationMarkSize = Unit.FromPoint(9),
        CalibrationMarkPosition = new UnitPoint { X = Unit.FromMillimeter(5), Y = Unit.FromMillimeter(-2.5) },
        SignatureText = "Printed at home",
        ShowPageNumber = true,
        FirstPageNumber = 3,
        PageNumberPosition = PageNumberPosition.Right,
    };

    private void Run(Action test) => fixture.Run(LanguageId.EnglishUk, test);

    [Fact]
    public void Settings_come_back_as_they_were_when_nothing_is_touched()
    {
        Run(() =>
        {
            HeaderFooter header = Header();
            var watermark = new Watermark { Type = WatermarkType.OnPage, Path = "/tmp/mark.png" };

            var model = new HeaderFooterViewModel(header, watermark);

            Assert.Equal("120", model.ColorBarWidthText);
            Assert.Equal("14.5", model.ColorBarHeightText);
            Assert.Equal("9", model.MarkSizeText);      // the Windows program showed the height of the colour bar here
            Assert.Equal("5", model.MarkXText);
            Assert.Equal("-2.5", model.MarkYText);
            Assert.Equal(3, model.FirstPageNumber);
            Assert.Equal("Right", model.SelectedPosition.Text);
            Assert.True(model.WatermarkPathOffered);
            HeaderFooterResult result = model.Result();
            Assert.True(result.Header.IsEqualTo(header));
            Assert.True(result.Watermark.IsEqualTo(watermark));
            Assert.NotSame(header.CalibrationImageSize, result.Header.CalibrationImageSize);
        });
    }

    [Fact]
    public void What_is_typed_is_what_comes_back()
    {
        Run(() =>
        {
            var model = new HeaderFooterViewModel(new HeaderFooter(), new Watermark());
            Assert.False(model.WatermarkPathOffered);
            Assert.Equal(3, model.PositionOptions.Count);
            Assert.Equal(3, model.WatermarkOptions.Count);

            model.ShowColorBar = true;
            model.ColorBarWidthText = "200";
            model.ColorBarHeightText = "20";
            model.ShowMarks = true;
            model.MarkSizeText = "12.5";
            model.MarkXText = "-";
            model.MarkYText = "4";
            model.SignatureText = "me";
            model.ShowPageNumber = true;
            Assert.Equal(1, model.FirstPageNumber);     // numbering starts at 1 when it had no start
            model.FirstPageNumber = 7;
            model.SelectedPosition = model.PositionOptions[0];
            model.SelectedWatermark = model.WatermarkOptions.First(option => option.Value == WatermarkType.OnImage);
            model.WatermarkPath = "/tmp/w.png";

            HeaderFooterResult result = model.Result();
            Assert.True(result.Header.ShowCalibrationImage);
            Assert.Equal(200.0, result.Header.CalibrationImageSize.X.Point, 6);
            Assert.Equal(20.0, result.Header.CalibrationImageSize.Y.Point, 6);
            Assert.True(result.Header.ShowCalibrationMark);
            Assert.Equal(12.5, result.Header.CalibrationMarkSize.Point, 6);
            Assert.Equal(0.0, result.Header.CalibrationMarkPosition.X.Millimeter);
            Assert.Equal(4.0, result.Header.CalibrationMarkPosition.Y.Millimeter, 6);
            Assert.Equal("me", result.Header.SignatureText);
            Assert.Equal(7, result.Header.FirstPageNumber);
            Assert.Equal(PageNumberPosition.Left, result.Header.PageNumberPosition);
            Assert.Equal(WatermarkType.OnImage, result.Watermark.Type);
            Assert.Equal("/tmp/w.png", result.Watermark.Path);
        });
    }

    [Fact]
    public void Copies_of_the_header_do_not_share_their_sizes()
    {
        HeaderFooter header = Header();
        HeaderFooter copy = header.Clone();
        var settings = new ProjectSettings { HeaderFooter = header };
        var other = new ProjectSettings();
        other.CopyFrom(settings);

        copy.CalibrationImageSize.X = Unit.FromPoint(1);
        other.HeaderFooter.CalibrationMarkPosition.Y = Unit.FromMillimeter(40);

        Assert.Equal(120.0, header.CalibrationImageSize.X.Point, 6);
        Assert.Equal(-2.5, header.CalibrationMarkPosition.Y.Millimeter, 6);
        Assert.False(header.IsEqualTo(copy));
        Assert.True(header.IsEqualTo(Header()));
    }

    [Fact]
    public void Project_becomes_custom_only_when_something_changes()
    {
        Run(() =>
        {
            var model = new MainWindowViewModel();
            ProjectSettingsViewModel project = model.ProjectSettings;
            void Euro()
            {
                project.SelectedGroup = project.Groups.First(option => option.Group == SettingsGroup.Cards);
                project.SelectedStyle = project.Styles.First(style => style.FileName == "Cartas_Euro.pnps");
            }

            Euro();
            Func<HeaderFooterViewModel, HeaderFooterResult?> answer = _ => null;
            int asked = 0;
            model.AskHeaderFooter = header =>
            {
                asked++;
                return Task.FromResult(answer(header));
            };

            // Cancelled, and accepted as it was.
            HeadlessFixture.Wait(model.EditHeaderFooterAsync());
            answer = header => header.Result();
            HeadlessFixture.Wait(model.EditHeaderFooterAsync());
            Assert.Equal(2, asked);
            Assert.Equal(SettingsGroup.Cards, project.Settings.Group.Group);

            // A signature.
            answer = header =>
            {
                header.SignatureText = "signed";
                return header.Result();
            };
            HeadlessFixture.Wait(model.EditHeaderFooterAsync());
            Assert.Equal("signed", project.Settings.HeaderFooter.SignatureText);
            Assert.Equal(SettingsGroup.Custom, project.Settings.Group.Group);
            Assert.True(model.HasChanged);

            // Only the watermark: the Windows program left the style here.
            Euro();
            answer = header =>
            {
                header.SelectedWatermark = header.WatermarkOptions[2];
                header.WatermarkPath = "/tmp/w.png";
                return header.Result();
            };
            HeadlessFixture.Wait(model.EditHeaderFooterAsync());
            Assert.Equal(WatermarkType.OnPage, project.Settings.Watermark.Type);
            Assert.Equal("/tmp/w.png", project.Settings.Watermark.Path);
            Assert.Equal(SettingsGroup.Custom, project.Settings.Group.Group);

            // Only the size of the colour bar: the Windows program left the style here too.
            Euro();
            answer = header =>
            {
                header.ColorBarWidthText = "333";
                return header.Result();
            };
            HeadlessFixture.Wait(model.EditHeaderFooterAsync());
            Assert.Equal(333.0, project.Settings.HeaderFooter.CalibrationImageSize.X.Point, 6);
            Assert.Equal(SettingsGroup.Custom, project.Settings.Group.Group);
        });
    }

    [Fact]
    public void Window_shows_what_is_turned_on_and_accepts_or_cancels()
    {
        Run(() =>
        {
            var owner = new Window();
            owner.Show();
            var window = new HeaderFooterWindow(new HeaderFooterViewModel(new HeaderFooter(), new Watermark()));
            Task<HeaderFooterResult?> result = window.ShowDialog<HeaderFooterResult?>(owner);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Header / Footer", window.Title);
            Assert.Equal("Print color bar", window.FindControl<CheckBox>("ColorBarCheck")!.Content);
            Assert.False(window.FindControl<StackPanel>("ColorBarPanel")!.IsVisible);
            Assert.False(window.FindControl<StackPanel>("MarksPanel")!.IsVisible);
            Assert.False(window.FindControl<Avalonia.Controls.Grid>("PageNumberPanel")!.IsVisible);
            Assert.False(window.FindControl<TextBox>("WatermarkPathBox")!.IsVisible);

            window.Model.ShowColorBar = true;
            window.Model.ShowMarks = true;
            window.Model.ShowPageNumber = true;
            window.Model.SelectedWatermark = window.Model.WatermarkOptions[1];
            window.PickImage = () => Task.FromResult<string?>("/tmp/picked.png");
            HeadlessFixture.Wait(window.PickWatermarkAsync());
            Dispatcher.UIThread.RunJobs();
            Assert.True(window.FindControl<StackPanel>("MarksPanel")!.IsVisible);
            Assert.True(window.FindControl<Avalonia.Controls.Grid>("PageNumberPanel")!.IsVisible);
            Assert.Equal("/tmp/picked.png", window.FindControl<TextBox>("WatermarkPathBox")!.Text);
            var first = window.FindControl<PnpTool.Controls.NumericBox>("FirstPageBox")!;
            Assert.Equal(1, first.Value);
            first.Value = 5;
            Screenshots.Capture(window, "ventana-encabezado");

            // A cancelled file dialog keeps the image.
            window.PickImage = () => Task.FromResult<string?>(null);
            HeadlessFixture.Wait(window.PickWatermarkAsync());
            window.Accept();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(5, result.Result!.Header.FirstPageNumber);
            Assert.Equal("/tmp/picked.png", result.Result.Watermark.Path);
            Assert.Equal(WatermarkType.OnImage, result.Result.Watermark.Type);

            window = new HeaderFooterWindow(new HeaderFooterViewModel(new HeaderFooter(), new Watermark()));
            result = window.ShowDialog<HeaderFooterResult?>(owner);
            window.FindControl<PnpTool.Controls.OkCancelButtons>("Buttons")!.CancelButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Null(result.Result);
            owner.Close();
        });
    }
}
