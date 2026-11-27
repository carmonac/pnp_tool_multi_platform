using Avalonia.Controls;
using Avalonia.Threading;
using ImageMagick;
using PnpTool.App.ViewModels;
using PnpTool.App.Views;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;

namespace PnpTool.App.Tests;

/// <summary>The settings of the program and what is asked the first time it starts.</summary>
[Collection(UserInterface.Name)]
public sealed class ProgramSettingsTests(HeadlessFixture fixture)
{
    /// <summary>Runs a test in English with the settings as they come, and puts settings, language and files back.</summary>
    private void Run(Action test) =>
        fixture.Run(LanguageId.EnglishUk, () =>
        {
            string rgb = AppSettings.RgbIccPath;
            string cmyk = AppSettings.CmykIccPath;
            void Clean()
            {
                AppSettings.Reset();
                AppSettings.RgbIccPath = rgb;
                AppSettings.CmykIccPath = cmyk;
                File.Delete(AppPaths.SettingsFile);
                AppLanguage.Change(new SupportedLanguage(LanguageId.EnglishUk));
            }

            Clean();
            try
            {
                test();
            }
            finally
            {
                Clean();
            }
        });

    [Fact]
    public void Settings_shown_are_those_of_the_program()
    {
        Run(() =>
        {
            AppSettings.MinimumDpi = 150;
            AppSettings.MaximumDpi = 600;
            AppSettings.TargetDpi = 300;
            AppSettings.PrinterMargin = new UnitPoint { X = Unit.FromMillimeter(4.5), Y = Unit.FromMillimeter(6) };
            AppSettings.DefaultAddImagesChoice = AddImagesResult.ManyFrontsOneBack;
            AppSettings.EmbedColorProfile = true;

            var model = new ProgramSettingsViewModel();

            Assert.Equal("en-GB", model.Language.Code);
            Assert.Equal(UnitType.Millimeter, model.SelectedUnit);
            Assert.Equal("150", model.MinimumDpiText);
            Assert.Equal("4.5", model.MarginXText);
            Assert.Equal(AddImagesResult.ManyFrontsOneBack, model.SelectedAddImages.Value);
            Assert.True(model.EmbedColorProfile);     // the Windows window always showed this one unticked
            Assert.Equal(5, model.AddImagesOptions.Count);
            Assert.Equal(5, model.ResolutionOptions.Count);
            Assert.Equal(4, model.UpdateOptions.Count);
            Assert.All(
                model.ResolutionOptions.Select(option => option.Text).Concat(model.AddImagesOptions.Select(option => option.Text)),
                text => Assert.DoesNotContain(StringResources.MissingPrefix, text));
            Assert.Null(model.Problem());
        });
    }

    [Fact]
    public void Accepting_puts_the_settings_to_use_and_saves_them()
    {
        Run(() =>
        {
            string temp = fixture.NewFolder();
            var model = new ProgramSettingsViewModel
            {
                MinimumDpiText = "100",
                MaximumDpiText = "900",
                TargetDpiText = "450",
                MarginXText = "4.5",      // the Windows program dropped the decimals
                MarginYText = "7",
                ShowPathOnHover = true,
                OpenPdfWhenDone = false,
                ShowCmykWarning = true,
                StartMaximized = true,
                WriteLogs = true,
                EmbedColorProfile = true,
                SmallFileSizeText = "12",
                PerformanceCountText = "50",
                TempFolder = temp,          // the Windows program checked it and did not keep it
            };
            model.SelectedUpdates = model.UpdateOptions.First(option => option.Channel == UpdateChannel.None);
            model.SelectedAddImages = model.AddImagesOptions.First(option => option.Value == AddImagesResult.Ask);

            ProgramSettingsChange change = model.Apply();

            Assert.Equal(new ProgramSettingsChange(Language: false, Unit: false, PanelResolution: false, DefaultProfiles: false, PathOnHover: true), change);
            Assert.Equal(100, AppSettings.MinimumDpi);
            Assert.Equal(900, AppSettings.MaximumDpi);
            Assert.Equal(450, AppSettings.TargetDpi);
            Assert.Equal(4.5, AppSettings.PrinterMargin.X.Millimeter, 6);
            Assert.Equal(7.0, AppSettings.PrinterMargin.Y.Millimeter, 6);
            Assert.True(AppSettings.ShowPathOnHover);
            Assert.False(AppSettings.OpenPdfWhenDone);
            Assert.True(AppSettings.ShowCmykWarning);
            Assert.True(AppSettings.StartMaximized);
            Assert.True(AppSettings.WriteLogs);
            Assert.True(AppSettings.EmbedColorProfile);
            Assert.Equal(12, AppSettings.SmallFileSizeKb);
            Assert.Equal(50, AppSettings.PerformanceWarningImageCount);
            Assert.Equal(UpdateChannel.None, AppSettings.UpdateNotifications);
            Assert.Equal(temp + Path.DirectorySeparatorChar, AppSettings.TempFolder);

            // Saved: read back from the file.
            Assert.True(File.Exists(AppPaths.SettingsFile));
            AppSettings.Reset();
            Assert.False(AppSettings.Load().HasError);
            Assert.Equal(450, AppSettings.TargetDpi);
            Assert.Equal(4.5, AppSettings.PrinterMargin.X.Millimeter, 6);
            Assert.Equal(temp + Path.DirectorySeparatorChar, AppSettings.TempFolder);
            Assert.True(AppSettings.EmbedColorProfile);
        });
    }

    [Fact]
    public void Language_units_and_quality_change_when_accepted()
    {
        Run(() =>
        {
            var said = new List<string>();
            var model = new ProgramSettingsViewModel();
            model.Notice += said.Add;
            model.Language = new SupportedLanguage(LanguageId.Spanish);
            model.SelectedUnit = UnitType.Inch;
            model.SelectedResolution = model.ResolutionOptions.First(option => option.Value == ImageResolutionType.LargeBitmap);
            Assert.Contains(said, message => message.StartsWith("Changing the image quality", StringComparison.Ordinal));
            Assert.Equal("en-GB", AppLanguage.Current.Code);      // nothing changes until it is accepted
            Assert.Equal(UnitType.Millimeter, MeasurementSettings.Current);

            ProgramSettingsChange change = model.Apply();

            Assert.True(change.Language);
            Assert.True(change.Unit);
            Assert.True(change.PanelResolution);
            Assert.Equal("es", AppLanguage.Current.Code);
            Assert.Equal("Idioma", StringResources.Get("Form_Setup_PnpTool_Label_Idioma"));
            Assert.Equal(UnitType.Inch, MeasurementSettings.Current);
            Assert.Equal("inch", LayoutViewModel.UnitName);
            Assert.Equal(ImageResolutionType.LargeBitmap, AppSettings.PanelResolution);
            Assert.Equal("1", Unit.FromMillimeter(25.4).ToDisplayString());
        });
    }

    [Fact]
    public void Unit_saved_in_the_settings_is_the_one_in_use_after_loading()
    {
        Run(() =>
        {
            AppSettings.MeasurementSystem = UnitType.Inch;
            AppSettings.Save();
            AppSettings.Reset();
            Assert.Equal(UnitType.Millimeter, MeasurementSettings.Current);

            Assert.False(AppSettings.Load().HasError);

            Assert.Equal(UnitType.Inch, MeasurementSettings.Current);
        });
    }

    [Fact]
    public void Settings_that_cannot_work_are_refused()
    {
        Run(() =>
        {
            var model = new ProgramSettingsViewModel { TempFolder = Path.Combine(fixture.NewFolder(), "missing") };
            Assert.StartsWith("Temporary files folder", model.Problem());
            model.TempFolder = fixture.NewFolder();
            Assert.Null(model.Problem());

            model.MinimumDpiText = "700";
            model.MaximumDpiText = "600";
            Assert.Equal("DPI MIN > DPI MAX", model.Problem());
            model.MinimumDpiText = "100";
            model.TargetDpiText = "50";
            Assert.NotNull(model.Problem());
            model.TargetDpiText = "601";
            Assert.NotNull(model.Problem());
            model.TargetDpiText = "300";
            Assert.Null(model.Problem());

            model.CmykProfile = Path.Combine(model.TempFolder, "none.icc");
            Assert.StartsWith("Default ICC Profiles", model.Problem());
        });
    }

    private static string Picture(string folder, string name)
    {
        string path = Path.Combine(folder, name);
        using var image = new MagickImage(new MagickColor("#3060C0"), 60, 84);
        image.Density = new Density(300);
        image.Write(path);
        return path;
    }

    [Fact]
    public void Main_window_follows_the_new_language_and_unit()
    {
        Run(() =>
        {
            string folder = fixture.NewFolder();
            var model = new MainWindowViewModel();
            ProjectSettingsViewModel project = model.ProjectSettings;
            project.SelectedGroup = project.Groups.First(option => option.Group == SettingsGroup.Cards);
            project.SelectedStyle = project.Styles.First(style => style.FileName == "Cartas_Standar.pnps");
            HeadlessFixture.Wait(model.ImagePanel.AddImagesAsync([Picture(folder, "one.png")]));
            model.SelectedTab = model.Tabs[2];
            Assert.Equal("New", model.Tabs[0].Groups[0].Buttons[0].Label);
            Assert.Equal("Group", project.GroupText);
            string title = model.ImagePanel.Cards[0].Title;
            Assert.Null(model.ImagePanel.Cards[0].FrontPathTip);
            int relocalized = 0;
            model.Relocalized += (_, _) => relocalized++;

            // Cancelled.
            model.AskProgramSettings = _ => Task.FromResult<ProgramSettingsChange?>(null);
            HeadlessFixture.Wait(model.EditProgramSettingsAsync());
            Assert.Equal(0, relocalized);

            model.AskProgramSettings = settings =>
            {
                settings.Language = new SupportedLanguage(LanguageId.Spanish);
                settings.SelectedUnit = UnitType.Inch;
                settings.ShowPathOnHover = true;
                return Task.FromResult<ProgramSettingsChange?>(settings.Apply());
            };
            HeadlessFixture.Wait(model.EditProgramSettingsAsync());

            Assert.Equal(1, relocalized);
            Assert.Equal("Nuevo", model.Tabs[0].Groups[0].Buttons[0].Label);
            Assert.Equal("Abrir", model.QuickAccess[2].ToolTipTitle);
            Assert.Same(model.Tabs[2], model.SelectedTab);
            Assert.Equal(StringResources.Get("Form_Setup_Label_Grupo"), project.GroupText);
            Assert.Equal("Cartas", project.Groups[0].Text);
            Assert.Equal(SettingsGroup.Cards, project.SelectedGroup!.Group);
            Assert.Equal("Cartas_Standar.pnps", project.SelectedStyle!.FileName);
            Assert.Equal(SettingsGroup.Cards, project.Settings.Group.Group);     // reading the lists again changes nothing
            Assert.StartsWith("Paneles: 1", model.CountsText);
            Assert.NotEqual(title, model.ImagePanel.Cards[0].Title);
            Assert.EndsWith("one.png", model.ImagePanel.Cards[0].FrontPathTip);
            Assert.False(model.HasChanged && project.Settings.Group.Group == SettingsGroup.Custom);

            // The buttons made again still work.
            int cards = model.ImagePanel.Cards.Count;
            model.Tabs[1].Groups[0].Buttons.First(button => button.Key == "Form_Principal_Boton_AnadirImagenVacia").Command.Execute(null);
            Assert.Equal(cards + 1, model.ImagePanel.Cards.Count);
        });
    }

    [Fact]
    public void New_default_profile_is_offered_to_the_project()
    {
        Run(() =>
        {
            string mine = Path.Combine(fixture.NewFolder(), "mine.icc");
            File.Copy(AppSettings.RgbIccPath, mine);
            var model = new MainWindowViewModel();
            var questions = new List<string>();
            bool? yes = false;
            model.Ask = (question, _) =>
            {
                questions.Add(question);
                return Task.FromResult(yes);
            };
            model.AskProgramSettings = settings =>
            {
                settings.RgbProfile = settings.RgbProfile == mine ? AppPaths.IccFolder + Path.DirectorySeparatorChar + Path.GetFileName(AppSettings.CmykIccPath) : mine;
                return Task.FromResult<ProgramSettingsChange?>(settings.Apply());
            };
            string before = model.ProjectSettings.Settings.Other.ColorProfilePath;

            // No: the project keeps its profile.
            HeadlessFixture.Wait(model.EditProgramSettingsAsync());
            Assert.Single(questions);
            Assert.Equal(before, model.ProjectSettings.Settings.Other.ColorProfilePath);
            Assert.Equal(mine, AppSettings.RgbIccPath);

            // Yes: it takes the new one.
            yes = true;
            AppSettings.RgbIccPath = before;
            HeadlessFixture.Wait(model.EditProgramSettingsAsync());
            Assert.Equal(mine, model.ProjectSettings.Settings.Other.ColorProfilePath);
            Assert.NotNull(model.ProjectSettings.Settings.Other.ColorProfile);
        });
    }

    [Fact]
    public void Window_shows_the_settings_and_accepts_or_says_what_is_wrong()
    {
        Run(() =>
        {
            var owner = new Window();
            owner.Show();
            var window = new ProgramSettingsWindow(new ProgramSettingsViewModel());
            var said = new List<string>();
            window.ShowMessage = message =>
            {
                said.Add(message);
                return Task.CompletedTask;
            };
            Task<ProgramSettingsChange?> result = window.ShowDialog<ProgramSettingsChange?>(owner);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("PNP Tool Preferences", window.Title);
            Assert.Equal("Language", window.FindControl<TextBlock>("LanguageHeading")!.Text);
            Assert.Equal("en-GB", window.FindControl<PnpTool.Controls.LanguageComboBox>("LanguageBox")!.SelectedLanguage.Code);
            Assert.Equal("Open PDF when finished", window.FindControl<CheckBox>("OpenPdfCheck")!.Content);
            Assert.Empty(said);
            Screenshots.Capture(window, "ventana-ajustes-programa");

            string temp = fixture.NewFolder();
            window.PickFolder = () => Task.FromResult<string?>(temp);
            HeadlessFixture.Wait(window.PickTempFolderAsync());
            string profile = Path.Combine(temp, "p.icc");
            File.Copy(AppSettings.CmykIccPath, profile);
            window.PickProfile = () => Task.FromResult<string?>(profile);
            HeadlessFixture.Wait(window.PickProfileAsync(cmyk: true));
            Assert.Equal(profile, window.FindControl<TextBox>("CmykProfileBox")!.Text);

            window.Model.MinimumDpiText = "9999";
            HeadlessFixture.Wait(window.AcceptAsync());
            Dispatcher.UIThread.RunJobs();
            Assert.Single(said);
            Assert.False(result.IsCompleted);

            window.Model.MinimumDpiText = "72";
            window.FindControl<PnpTool.Controls.UnitSelector>("UnitBox")!.SelectedUnit = UnitType.Inch;
            HeadlessFixture.Wait(window.AcceptAsync());
            Dispatcher.UIThread.RunJobs();
            Assert.True(result.Result!.Unit);
            Assert.True(result.Result.DefaultProfiles);
            Assert.Equal(profile, AppSettings.CmykIccPath);
            Assert.Equal(72, AppSettings.MinimumDpi);

            window = new ProgramSettingsWindow(new ProgramSettingsViewModel { MinimumDpiText = "1" });
            result = window.ShowDialog<ProgramSettingsChange?>(owner);
            window.FindControl<PnpTool.Controls.OkCancelButtons>("Buttons")!.CancelButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Null(result.Result);
            Assert.Equal(72, AppSettings.MinimumDpi);
            owner.Close();
        });
    }

    [Fact]
    public void First_start_asks_language_units_and_terms_before_anything_is_saved()
    {
        Run(() =>
        {
            Assert.True(FirstRunWindow.IsNeeded);
            Assert.Equal("es", FirstRunWindow.LanguageFor("es").Code);
            Assert.Equal("en-GB", FirstRunWindow.LanguageFor("xx").Code);

            var window = new FirstRunWindow();
            var said = new List<string>();
            window.ShowMessage = message =>
            {
                said.Add(message);
                return Task.CompletedTask;
            };
            int terms = 0;
            window.ShowTerms = () =>
            {
                terms++;
                return Task.CompletedTask;
            };
            int completed = 0;
            window.Completed += (_, _) => completed++;
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Button ok = window.FindControl<PnpTool.Controls.OkCancelButtons>("Buttons")!.OkButton;
            Assert.False(ok.IsEnabled);
            Screenshots.Capture(window, "ventana-primer-arranque");

            // Not accepted without the terms.
            window.Accept();
            Assert.False(window.Accepted);
            Assert.True(FirstRunWindow.IsNeeded);

            window.UseLanguage(new SupportedLanguage(LanguageId.Spanish));
            Assert.Equal("es", AppLanguage.Current.Code);
            Assert.Equal("Acepto los Terminos de Uso", window.FindControl<TextBlock>("TermsText")!.Text);
            HeadlessFixture.Wait(window.OpenTermsAsync());
            Assert.Equal(1, terms);

            window.FindControl<PnpTool.Controls.UnitSelector>("UnitBox")!.SelectedUnit = UnitType.Inch;
            window.TermsAccepted = true;
            Dispatcher.UIThread.RunJobs();
            Assert.True(ok.IsEnabled);
            window.Accept();
            Dispatcher.UIThread.RunJobs();

            Assert.True(window.Accepted);
            Assert.Equal(1, completed);
            Assert.False(window.IsVisible);
            Assert.False(FirstRunWindow.IsNeeded);
            AppSettings.Reset();
            Assert.False(AppSettings.Load().HasError);
            Assert.Equal("es", AppSettings.LanguageCode);
            Assert.Equal(UnitType.Inch, MeasurementSettings.Current);
        });
    }

    [Fact]
    public void First_start_closed_without_accepting_saves_nothing()
    {
        Run(() =>
        {
            var window = new FirstRunWindow();
            int completed = 0;
            window.Completed += (_, _) => completed++;
            window.Show();
            window.FindControl<PnpTool.Controls.OkCancelButtons>("Buttons")!.CancelButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.False(window.IsVisible);
            Assert.False(window.Accepted);
            Assert.Equal(0, completed);
            Assert.True(FirstRunWindow.IsNeeded);
        });
    }
}
