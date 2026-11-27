using Avalonia.Controls;
using Avalonia.Threading;
using ImageMagick;
using PnpTool.App.ViewModels;
using PnpTool.App.Views;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Projects;

namespace PnpTool.App.Tests;

/// <summary>The window of the other settings of a project.</summary>
[Collection(UserInterface.Name)]
public sealed class OtherSettingsTests(HeadlessFixture fixture)
{
    private void Run(Action test) => fixture.Run(LanguageId.EnglishUk, test);

    private static OtherSettings Sample() => new()
    {
        ImagePathMode = ImagePathMode.Absolute,
        PdfOutput = PdfOutputMode.MaximumSize,
        PdfMaximumSize = 40,
        GenerateFileId = true,
        FileIdSeed = "abc",
        Compression = CompressionType.Percentage,
        CompressionPercent = 60,
        StackedLayout = new StackedLayout(3, LayoutType.Stacked),
        ResizeFilter = FilterType.Mitchell,
    };

    [Fact]
    public void Settings_come_back_as_they_were_when_nothing_is_touched()
    {
        Run(() =>
        {
            OtherSettings settings = Sample();

            var model = new OtherSettingsViewModel(settings, SettingsGroup.Cards);

            Assert.Equal("RGB", model.SelectedColorSpace.Text);
            Assert.Equal("3", model.BlockSizeText);
            Assert.True(model.BlockSizeOffered);
            Assert.Equal("40", model.MaximumSizeText);
            Assert.True(model.MaximumSizeOffered);
            Assert.Equal("60", model.CompressionPercentText);
            Assert.True(model.CompressionPercentOffered);
            Assert.Equal(FilterType.Mitchell, model.ResizeFilter);
            Assert.Null(model.Problem());
            Assert.True(model.Result().IsEqualTo(settings));
        });
    }

    [Fact]
    public void What_is_chosen_is_what_comes_back()
    {
        Run(() =>
        {
            var model = new OtherSettingsViewModel(new OtherSettings(), SettingsGroup.Cards);
            Assert.False(model.BlockSizeOffered);
            Assert.False(model.MaximumSizeOffered);
            Assert.False(model.CompressionPercentOffered);
            Assert.Equal(2, model.LayoutOptions.Count);     // the list of Windows has no "by rows"
            Assert.Equal(3, model.PdfOutputOptions.Count);
            Assert.Equal(4, model.CompressionOptions.Count);
            Assert.All(
                model.LayoutOptions.Select(option => option.Text)
                    .Concat(model.PdfOutputOptions.Select(option => option.Text))
                    .Concat(model.CompressionOptions.Select(option => option.Text))
                    .Concat(model.PathOptions.Select(option => option.Text)),
                text => Assert.DoesNotContain(StringResources.MissingPrefix, text));

            model.SelectedPath = model.PathOptions.First(option => option.Value == ImagePathMode.Absolute);
            model.SelectedLayout = model.LayoutOptions[1];
            model.BlockSizeText = "4";
            model.SelectedPdfOutput = model.PdfOutputOptions.First(option => option.Value == PdfOutputMode.Separate);
            model.GenerateFileId = true;
            model.FileIdSeed = "seed";
            model.SelectedCompression = model.CompressionOptions.First(option => option.Value == CompressionType.Fast);
            model.ResizeFilter = FilterType.Cubic;

            OtherSettings result = model.Result();
            Assert.Equal(ImagePathMode.Absolute, result.ImagePathMode);
            Assert.Equal(LayoutType.Stacked, result.StackedLayout.LayoutType);
            Assert.Equal(4, result.StackedLayout.BlockSize);
            Assert.Equal(PdfOutputMode.Separate, result.PdfOutput);
            Assert.True(result.GenerateFileId);
            Assert.Equal("seed", result.FileIdSeed);
            Assert.Equal(CompressionType.Fast, result.Compression);
            Assert.Equal(FilterType.Cubic, result.ResizeFilter);
        });
    }

    [Fact]
    public void Order_by_rows_of_a_project_is_kept()
    {
        Run(() =>
        {
            var settings = new OtherSettings { StackedLayout = new StackedLayout(2, LayoutType.ByRows) };

            var model = new OtherSettingsViewModel(settings, SettingsGroup.Cards);

            Assert.Equal(3, model.LayoutOptions.Count);
            Assert.Equal(LayoutType.ByRows, model.SelectedLayout.Value);
            Assert.Equal(LayoutType.ByRows, model.Result().StackedLayout.LayoutType);
        });
    }

    [Fact]
    public void Percentage_and_pile_are_checked_for_what_is_chosen()
    {
        Run(() =>
        {
            // The Windows program checked the percentage only when the project already compressed by percentage.
            var model = new OtherSettingsViewModel(new OtherSettings(), SettingsGroup.Cards);
            model.SelectedCompression = model.CompressionOptions.First(option => option.Value == CompressionType.Percentage);
            Assert.Equal("100", model.CompressionPercentText);
            Assert.StartsWith("Compress file (", model.Problem());
            Assert.EndsWith("> 0, < 100", model.Problem());
            model.CompressionPercentText = "0";
            Assert.NotNull(model.Problem());
            model.CompressionPercentText = "75";
            Assert.Null(model.Problem());
            Assert.Equal(75.0, model.Result().CompressionPercent);

            // ... and refused another kind when it had a percentage out of range left.
            OtherSettings left = Sample();
            left.CompressionPercent = 100;
            model = new OtherSettingsViewModel(left, SettingsGroup.Cards);
            Assert.NotNull(model.Problem());
            model.SelectedCompression = model.CompressionOptions.First(option => option.Value == CompressionType.None);
            Assert.Null(model.Problem());

            model.BlockSizeText = "0";
            Assert.StartsWith("Stacked layout", model.Problem());
            model.SelectedLayout = model.LayoutOptions[0];
            Assert.Null(model.Problem());
        });
    }

    [Fact]
    public void Colour_space_brings_its_profile_and_a_profile_can_be_chosen()
    {
        Run(() =>
        {
            Assert.True(File.Exists(AppSettings.RgbIccPath), AppSettings.RgbIccPath);
            Assert.True(File.Exists(AppSettings.CmykIccPath), AppSettings.CmykIccPath);
            var model = new OtherSettingsViewModel(new OtherSettings(), SettingsGroup.Cards);
            Assert.True(model.ColorSpaceEnabled);
            string rgb = model.ProfileName;
            Assert.NotEqual("Icc not set", rgb);

            model.SelectedColorSpace = model.ColorSpaceOptions.First(option => option.Value == ProjectColorSpace.CMYK);
            Assert.Equal(AppSettings.CmykIccPath, model.ProfilePath);
            Assert.NotEqual(rgb, model.ProfileName);
            OtherSettings result = model.Result();
            Assert.Equal(ProjectColorSpace.CMYK, result.ColorSpace);
            Assert.Equal(AppSettings.CmykIccPath, result.ColorProfilePath);
            Assert.Equal(model.ProfileName, result.ColorProfile!.Description);

            // A profile of the user's.
            string folder = fixture.NewFolder();
            string mine = Path.Combine(folder, "mine.icc");
            File.Copy(AppSettings.RgbIccPath, mine);
            Assert.Null(model.UseProfileFile(mine));
            Assert.Equal(rgb, model.ProfileName);
            Assert.Equal(mine, model.Result().ColorProfilePath);

            // A file that is not a profile.
            string wrong = Path.Combine(folder, "wrong.icc");
            File.WriteAllText(wrong, "not a profile");
            Assert.StartsWith("wrong.icc", model.UseProfileFile(wrong));
            Assert.Equal(mine, model.ProfilePath);
            Assert.Equal(rgb, model.ProfileName);
        });
    }

    [Fact]
    public void Kind_of_project_decides_what_is_offered()
    {
        Run(() =>
        {
            var cards = new OtherSettingsViewModel(new OtherSettings(), SettingsGroup.Cards);
            Assert.True(cards.OutputOffered);
            Assert.True(cards.PdfOutputEnabled);
            Assert.True(cards.FileIdEnabled);

            foreach (SettingsGroup group in new[] { SettingsGroup.Tabletop, SettingsGroup.Dice })
            {
                var sheets = new OtherSettingsViewModel(Sample(), group);
                Assert.False(sheets.OutputOffered);
                Assert.Null(sheets.Problem());
            }

            var cmyk = new OtherSettings { ColorSpace = ProjectColorSpace.CMYK };
            var dice = new OtherSettingsViewModel(cmyk, SettingsGroup.Dice7Pixel);
            Assert.False(dice.ColorSpaceEnabled);
            Assert.Equal(ProjectColorSpace.RGB, dice.SelectedColorSpace.Value);
            Assert.Equal(ProjectColorSpace.CMYK, cmyk.ColorSpace);     // the settings given are left alone
        });
    }

    [Fact]
    public void Project_becomes_custom_only_when_something_changes()
    {
        Run(() =>
        {
            var model = new MainWindowViewModel();
            ProjectSettingsViewModel project = model.ProjectSettings;
            project.SelectedGroup = project.Groups.First(option => option.Group == SettingsGroup.Cards);
            project.SelectedStyle = project.Styles.First(style => style.FileName == "Cartas_Euro.pnps");
            Func<OtherSettingsViewModel, OtherSettings?> answer = _ => null;
            model.AskOtherSettings = other => Task.FromResult(answer(other));

            HeadlessFixture.Wait(model.EditOtherSettingsAsync());
            answer = other => other.Result();
            HeadlessFixture.Wait(model.EditOtherSettingsAsync());
            Assert.Equal(SettingsGroup.Cards, project.Settings.Group.Group);

            // The Windows program never made the project custom from this window.
            answer = other =>
            {
                other.SelectedLayout = other.LayoutOptions[1];
                other.BlockSizeText = "2";
                return other.Result();
            };
            HeadlessFixture.Wait(model.EditOtherSettingsAsync());
            Assert.Equal(LayoutType.Stacked, project.Settings.Other.StackedLayout.LayoutType);
            Assert.Equal(2, project.Settings.Other.StackedLayout.BlockSize);
            Assert.Equal(SettingsGroup.Custom, project.Settings.Group.Group);
            Assert.True(model.HasChanged);
        });
    }

    [Fact]
    public void Window_shows_what_is_chosen_and_accepts_or_says_what_is_wrong()
    {
        Run(() =>
        {
            var owner = new Window();
            owner.Show();
            var window = new OtherSettingsWindow(new OtherSettingsViewModel(Sample(), SettingsGroup.Cards));
            var said = new List<string>();
            window.ShowMessage = message =>
            {
                said.Add(message);
                return Task.CompletedTask;
            };
            Task<OtherSettings?> result = window.ShowDialog<OtherSettings?>(owner);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Other Parameters", window.Title);
            Assert.True(window.FindControl<TextBox>("BlockSizeBox")!.IsVisible);
            Assert.True(window.FindControl<TextBox>("MaximumSizeBox")!.IsVisible);
            Assert.True(window.FindControl<TextBox>("PercentBox")!.IsVisible);
            Assert.True(window.FindControl<TextBox>("SeedBox")!.IsVisible);
            Assert.Equal(FilterType.Mitchell, window.FindControl<PnpTool.Controls.FilterComboBox>("FilterBox")!.SelectedFilter);
            Screenshots.Capture(window, "ventana-otros-parametros");

            window.Model.CompressionPercentText = "100";
            HeadlessFixture.Wait(window.AcceptAsync());
            Dispatcher.UIThread.RunJobs();
            Assert.Single(said);
            Assert.False(result.IsCompleted);

            window.Model.CompressionPercentText = "50";
            window.FindControl<PnpTool.Controls.FilterComboBox>("FilterBox")!.SelectedFilter = FilterType.Box;
            string copy = Path.Combine(fixture.NewFolder(), "copy.icc");
            File.Copy(AppSettings.CmykIccPath, copy);
            window.PickProfile = () => Task.FromResult<string?>(copy);
            HeadlessFixture.Wait(window.PickProfileAsync());
            HeadlessFixture.Wait(window.AcceptAsync());
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(50.0, result.Result!.CompressionPercent);
            Assert.Equal(FilterType.Box, result.Result.ResizeFilter);
            Assert.Equal(copy, result.Result.ColorProfilePath);

            window = new OtherSettingsWindow(new OtherSettingsViewModel(new OtherSettings(), SettingsGroup.Tabletop));
            result = window.ShowDialog<OtherSettings?>(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.False(window.FindControl<Border>("OutputGroup")!.IsVisible);
            Assert.False(window.FindControl<Border>("CompressionGroup")!.IsVisible);
            window.FindControl<PnpTool.Controls.OkCancelButtons>("Buttons")!.CancelButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Null(result.Result);
            owner.Close();
        });
    }
}
