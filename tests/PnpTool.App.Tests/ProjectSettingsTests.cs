using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ImageMagick;
using PnpTool.App.ViewModels;
using PnpTool.App.ViewModels.Panel;
using PnpTool.App.Views;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using Grid = PnpTool.Core.Projects.Grid;

namespace PnpTool.App.Tests;

[Collection(UserInterface.Name)]
public sealed class ProjectSettingsTests(HeadlessFixture fixture)
{
    private static string Picture(string folder, string name)
    {
        string path = Path.Combine(folder, name);
        using var image = new MagickImage(new MagickColor("#3060C0"), 120, 168);
        image.Density = new Density(300);
        image.Write(path);
        return path;
    }

    private static StyleGroupOption Group(ProjectSettingsViewModel settings, SettingsGroup group) =>
        settings.Groups.First(option => option.Group == group);

    private static StyleOption Style(ProjectSettingsViewModel settings, string fileName) =>
        settings.Styles.First(option => option.FileName == fileName);

    // ---- Choosing a style ----

    [Fact]
    public void A_new_project_is_a_custom_one()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var settings = new ProjectSettingsViewModel();

            Assert.Equal(SettingsGroup.Custom, settings.SelectedGroup!.Group);
            Assert.Equal(SettingsGroup.Custom, settings.Settings.Group.Group);
            Assert.False(settings.HasStyles);
            Assert.Equal(10, settings.Groups.Count);
            Assert.Equal("Grupo", ProjectSettingsViewModel.GroupLabel.TrimEnd(':', ' '));
        });
    }

    [Fact]
    public void Choosing_a_group_loads_its_first_style()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var settings = new ProjectSettingsViewModel();
            int changes = 0;
            settings.SettingsChanged += (_, _) => changes++;

            settings.SelectedGroup = Group(settings, SettingsGroup.Cards);

            Assert.Equal(10, settings.Styles.Count);
            Assert.Equal("Cartas_Standar.pnps", settings.SelectedStyle!.FileName);
            Assert.EndsWith("Cartas_Standar.pnps", settings.StyleFile);
            Assert.Equal(63.5, settings.Settings.Grid.CardSize.X.Millimeter, 2);
            Assert.Equal(88.9, settings.Settings.Grid.CardSize.Y.Millimeter, 2);
            Assert.Equal(SettingsGroup.Cards, settings.Settings.Group.Group);
            Assert.Equal(0, settings.Settings.Group.SubgroupIndex);
            Assert.Equal(ProjectType.Card, settings.Settings.Group.ProjectType);
            Assert.Equal(1, changes);

            settings.SelectedStyle = Style(settings, "Cartas_Tarot.pnps");
            Assert.Equal(120.0, Math.Max(settings.Settings.Grid.CardSize.X.Millimeter, settings.Settings.Grid.CardSize.Y.Millimeter), 2);
            Assert.Equal(4, settings.Settings.Group.SubgroupIndex);
            Assert.Equal(2, changes);
        });
    }

    [Fact]
    public void Buttons_of_the_settings_tab_depend_on_the_kind_of_project()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var settings = new ProjectSettingsViewModel();

            settings.SelectedGroup = Group(settings, SettingsGroup.Cards);
            Assert.True(settings.CanEditLayout && settings.CanEditFront && settings.CanEditHeaderFooter && settings.CanEditOther);
            Assert.False(settings.CanEditBack);    // the standard style has no backs
            Assert.False(settings.CanEditBooks);

            settings.SelectedStyle = Style(settings, "Cartas_Gutterfold.pnps");
            Assert.True(settings.CanEditBack);

            settings.SelectedGroup = Group(settings, SettingsGroup.Books);
            Assert.True(settings.CanEditBooks);
            Assert.False(settings.CanEditBack);
            Assert.Equal(ProjectType.Book, settings.Settings.Group.ProjectType);
            settings.SelectedStyle = Style(settings, "Papel_LibretoA4.pnps");
            Assert.Equal(ProjectType.Booklet, settings.Settings.Group.ProjectType);

            settings.SelectedGroup = Group(settings, SettingsGroup.Dice);
            Assert.False(settings.CanEditHeaderFooter);
            Assert.False(settings.CanEditBack);

            settings.SelectedGroup = Group(settings, SettingsGroup.SpanishDeck);
            Assert.Equal(ProjectType.SpanishDeck, settings.Settings.Group.ProjectType);

            settings.SelectedGroup = Group(settings, SettingsGroup.Dice7Pixel);
            Assert.Equal(12, settings.Styles.Count);
            Assert.Equal(string.Empty, settings.StyleFile);
            Assert.Equal(ProjectType.Dice7Pixel, settings.Settings.Group.ProjectType);
            Assert.False(settings.CanEditLayout || settings.CanEditFront || settings.CanEditOther || settings.CanEditHeaderFooter);

            settings.SelectedGroup = Group(settings, SettingsGroup.Tabletop);
            Assert.False(settings.HasStyles);
            Assert.Equal(ProjectType.Tabletop, settings.Settings.Group.ProjectType);

            settings.SelectedGroup = Group(settings, SettingsGroup.AdvancedStyles);
            Assert.Equal(ProjectType.AdvancedStyles, settings.Settings.Group.ProjectType);
            Assert.NotEmpty(settings.Project.AdvancedTemplate.Cards);
        });
    }

    [Fact]
    public void A_style_with_two_faces_announces_the_change_with_the_settings_before()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var settings = new ProjectSettingsViewModel();
            settings.SelectedGroup = Group(settings, SettingsGroup.Cards);
            var announced = new List<(bool HadSharedBack, bool WasDouble, int Columns)>();
            settings.PanelTypeChanged += (shared, before) => announced.Add((shared, before.DoublePanel, before.PrintLayout.Columns));

            settings.SelectedStyle = Style(settings, "Cartas_Gutterfold.pnps");

            Assert.True(settings.Settings.DoublePanel);
            Assert.Equal(BackImageMode.DifferentBack, settings.Settings.PrintLayout.BackImage);
            Assert.Equal([(false, false, 3)], announced);
            Assert.False(settings.ShowsSharedBack);

            settings.SelectedStyle = Style(settings, "Cartas_Euro.pnps");
            Assert.False(settings.Settings.DoublePanel);
            Assert.Equal(2, announced.Count);
            Assert.True(announced[1].WasDouble);
        });
    }

    // ---- Changing settings ----

    [Fact]
    public void A_changed_layout_makes_the_project_custom_and_sizes_the_faces()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var settings = new ProjectSettingsViewModel();
            settings.SelectedGroup = Group(settings, SettingsGroup.Cards);
            var layout = new PrintLayout();
            layout.CopyFrom(settings.Settings.PrintLayout);
            var grid = new Grid();
            grid.CopyFrom(settings.Settings.Grid);

            // Accepting the window without changes leaves the style chosen.
            settings.ApplyLayout(layout, false, grid, settings.Settings.LockCardSize);
            Assert.Equal(SettingsGroup.Cards, settings.SelectedGroup!.Group);

            grid.CardSize = new UnitPoint { X = Unit.FromMillimeter(70), Y = Unit.FromMillimeter(100) };
            grid.Bleed = Unit.FromMillimeter(3);
            settings.Settings.Back.Effect = ImageEffectType.Mirror;
            settings.ApplyLayout(layout, false, grid, lockCardSize: true);

            Assert.Equal(SettingsGroup.Custom, settings.SelectedGroup!.Group);
            Assert.Equal(SettingsGroup.Custom, settings.Settings.Group.Group);
            Assert.False(settings.HasStyles);
            Assert.Equal(70.0, settings.Settings.Front.Size.X.Millimeter, 2);
            Assert.Equal(100.0, settings.Settings.Front.Size.Y.Millimeter, 2);
            Assert.Equal(64.0, settings.Settings.Back.Size.X.Millimeter, 2);    // less the bleed on each side
            Assert.Equal(94.0, settings.Settings.Back.Size.Y.Millimeter, 2);
        });
    }

    [Fact]
    public void Layout_with_backs_shows_where_the_shared_back_goes_and_announces_two_faces()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var settings = new ProjectSettingsViewModel();
            settings.SelectedGroup = Group(settings, SettingsGroup.Cards);
            var announced = new List<bool>();
            settings.PanelTypeChanged += (shared, _) => announced.Add(shared);
            var layout = new PrintLayout();
            layout.CopyFrom(settings.Settings.PrintLayout);
            var grid = new Grid();
            grid.CopyFrom(settings.Settings.Grid);
            Assert.False(settings.ShowsSharedBack);

            layout.DoubleSided = true;
            layout.BackImage = BackImageMode.SameBack;
            settings.ApplyLayout(layout, doublePanel: false, grid, true);
            Assert.True(settings.ShowsSharedBack);
            Assert.True(settings.CanEditBack);
            Assert.Empty(announced);

            layout.BackImage = BackImageMode.DifferentBack;
            settings.ApplyLayout(layout, doublePanel: true, grid, true);
            Assert.False(settings.ShowsSharedBack);
            Assert.Equal([true], announced);    // it had a shared back until now

            settings.Settings.Front.PaintBackground = true;
            settings.Settings.Front.BackgroundEffect = CardBackgroundType.SingleColor;
            Assert.Equal((true, false), settings.PaintsBackground);
        });
    }

    [Fact]
    public void A_style_is_saved_and_an_opened_project_shows_its_group_and_style()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var settings = new ProjectSettingsViewModel();
            settings.SelectedGroup = Group(settings, SettingsGroup.Tokens);
            settings.SelectedStyle = Style(settings, "Tokens_25.pnps");
            string file = Path.Combine(fixture.NewFolder(), "mine.pnps");

            settings.SaveStyle(file);

            var read = new ProjectSettings();
            Assert.False(ProjectFileStore.LoadStyle(file, ref read).Error.HasError);
            Assert.Equal(25.0, read.Grid.CardSize.X.Millimeter, 2);
            Assert.Equal(SettingsGroup.Tokens, read.Group.Group);
            Assert.Equal(2, read.Group.SubgroupIndex);

            var opened = new ProjectSettingsViewModel();
            int changes = 0;
            opened.SettingsChanged += (_, _) => changes++;
            opened.Load(read);
            Assert.Equal(SettingsGroup.Tokens, opened.SelectedGroup!.Group);
            Assert.Equal("Tokens_25.pnps", opened.SelectedStyle!.FileName);
            Assert.Equal(25.0, opened.Settings.Grid.CardSize.X.Millimeter, 2);
            Assert.Equal(1, changes);
        });
    }

    // ---- The main window ----

    private MainWindowViewModel ModelWith(int cards)
    {
        string folder = fixture.NewFolder();
        var model = new MainWindowViewModel();
        HeadlessFixture.Wait(model.ImagePanel.AddImagesAsync([.. Enumerable.Range(1, cards).Select(i => Picture(folder, "c" + i + ".png"))]));
        return model;
    }

    private static string[] Fronts(MainWindowViewModel model) => [.. model.ImagePanel.Cards.Select(card => Path.GetFileNameWithoutExtension(card.Card.Front.Path))];

    private static string[] Backs(MainWindowViewModel model) => [.. model.ImagePanel.Cards.Select(card => Path.GetFileNameWithoutExtension(card.Card.Back.Path))];

    [Fact]
    public void A_style_with_a_back_per_card_gives_the_cards_two_faces_and_undo_takes_it_back()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            MainWindowViewModel model = ModelWith(4);
            ProjectSettingsViewModel settings = model.ProjectSettings;
            settings.SelectedGroup = Group(settings, SettingsGroup.Cards);
            System.Windows.Input.ICommand backs = Command(model, "Form_Principal_Boton_CambiarImagenes_Traseras");
            Assert.False(backs.CanExecute(null));

            settings.SelectedStyle = Style(settings, "Cartas_Gutterfold.pnps");
            Dispatcher.UIThread.RunJobs();

            Assert.True(model.ImagePanel.IsDouble);
            Assert.Equal(["c1", "c2", "c3", "c4"], Fronts(model));    // the images are the fronts
            Assert.All(model.ImagePanel.Cards, card => Assert.Equal(ImageKind.Blank, card.Card.Back.ImageKind));
            Assert.True(backs.CanExecute(null));
            Assert.Contains(" @ ", model.ImagePanel.Cards[0].Title);
            Assert.True(Command(model, "Boton_Configurar_Cara_Trasera").CanExecute(null));

            model.Undo();
            Assert.False(model.ImagePanel.IsDouble);
            Assert.Equal(["c1", "c2", "c3", "c4"], Fronts(model));
            Assert.False(settings.Settings.DoublePanel);
            Assert.Equal("Cartas_Standar.pnps", settings.SelectedStyle!.FileName);
            Assert.False(backs.CanExecute(null));

            model.Redo();
            Assert.True(model.ImagePanel.IsDouble);
            Assert.True(settings.Settings.DoublePanel);
            Assert.Equal("Cartas_Gutterfold.pnps", settings.SelectedStyle!.FileName);
            Assert.Equal(4, model.ImagePanel.PanelCount);
        });
    }

    [Fact]
    public void Going_to_two_faces_asks_what_the_images_are()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            MainWindowViewModel model = ModelWith(4);
            ProjectSettingsViewModel settings = model.ProjectSettings;
            settings.SelectedGroup = Group(settings, SettingsGroup.Cards);
            var asked = new List<bool>();
            model.AskFaceChange = toDouble =>
            {
                asked.Add(toDouble);
                return Task.FromResult<FaceChangeChoice?>(FaceChangeChoice.Group);
            };
            var layout = new PrintLayout();
            layout.CopyFrom(settings.Settings.PrintLayout);
            var grid = new Grid();
            grid.CopyFrom(settings.Settings.Grid);
            layout.DoubleSided = true;
            layout.BackImage = BackImageMode.SameBack;

            settings.ApplyLayout(layout, doublePanel: true, grid, true);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal([true], asked);
            Assert.Equal(["c1", "c3"], Fronts(model));    // paired: front, back, front, back
            Assert.Equal(["c2", "c4"], Backs(model));

            // Back to one face, keeping the backs.
            model.AskFaceChange = _ => Task.FromResult<FaceChangeChoice?>(FaceChangeChoice.Backs);
            settings.ApplyLayout(layout, doublePanel: false, grid, true);
            Dispatcher.UIThread.RunJobs();
            Assert.False(model.ImagePanel.IsDouble);
            Assert.Equal(["c2", "c4"], Fronts(model));

            // Closing the question leaves the cards as they are.
            model.AskFaceChange = _ => Task.FromResult<FaceChangeChoice?>(null);
            settings.ApplyLayout(layout, doublePanel: true, grid, true);
            Dispatcher.UIThread.RunJobs();
            Assert.True(model.ImagePanel.IsDouble);
            Assert.Equal(["c2", "c4"], Fronts(model));
        });
    }

    [Fact]
    public void Shared_back_is_shown_when_the_project_uses_one_and_goes_into_the_project()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            MainWindowViewModel model = ModelWith(2);
            ProjectSettingsViewModel settings = model.ProjectSettings;
            settings.SelectedGroup = Group(settings, SettingsGroup.Cards);
            Assert.False(model.SharedBack.IsVisible);
            var layout = new PrintLayout();
            layout.CopyFrom(settings.Settings.PrintLayout);
            var grid = new Grid();
            grid.CopyFrom(settings.Settings.Grid);
            layout.DoubleSided = true;
            layout.BackImage = BackImageMode.SameBack;
            settings.ApplyLayout(layout, doublePanel: false, grid, true);
            Assert.True(model.SharedBack.IsVisible);

            string file = Picture(fixture.NewFolder(), "back.png");
            Assert.False(model.SharedBack.HasImage);
            Assert.False(model.SharedBack.SetImage(file + ".gone"));
            Assert.True(model.SharedBack.SetImage(file));
            HeadlessFixture.Wait(model.SharedBack.ImageLoaded);

            Assert.True(model.SharedBack.HasImage);
            Assert.NotNull(model.SharedBack.Image);
            Assert.Equal(FaceType.Back, model.SharedBack.Face.FaceType);
            Assert.Equal(file, settings.Settings.SharedBack.Path);

            model.SharedBack.Rotate(new RotationProperties(90, false, false));
            HeadlessFixture.Wait(model.SharedBack.ImageLoaded);
            Assert.Equal(90, settings.Settings.SharedBack.Rotation.Angle);
            Assert.True(model.SharedBack.Image!.PixelSize.Width > model.SharedBack.Image.PixelSize.Height);

            Project project = model.CurrentProject();
            Assert.Equal(2, project.Deck.Cards.Length);
            Assert.Equal(file, project.Settings.SharedBack.Path);
            Assert.True(project.Settings.PrintLayout.DoubleSided);

            model.SharedBack.Clear();
            Assert.False(model.SharedBack.HasImage);
            Assert.Null(model.SharedBack.Image);
            Assert.Equal(string.Empty, settings.Settings.SharedBack.Path ?? string.Empty);
        });
    }

    [Fact]
    public void Blank_colour_is_chosen_beside_the_button_that_adds_blank_cards()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var model = new MainWindowViewModel();
            var button = model.Tabs.SelectMany(tab => tab.Groups).SelectMany(group => group.Buttons).First(b => b.Key == "Form_Principal_Boton_AnadirImagenBlanca");
            Assert.True(button.HasSwatch);
            Assert.Equal(Colors.White, ((ISolidColorBrush)button.Swatch!).Color);
            var shown = new List<ArgbColor>();
            model.AskColor = color =>
            {
                shown.Add(color);
                return Task.FromResult<ArgbColor?>(new ArgbColor(20, 40, 60));
            };

            HeadlessFixture.Wait(model.ChooseBlankColorAsync());
            button.Command.Execute(null);

            Assert.Equal([ArgbColor.White], shown);
            Assert.Equal(Color.FromRgb(20, 40, 60), ((ISolidColorBrush)button.Swatch!).Color);
            Assert.Equal("255_20_40_60", model.ImagePanel.Cards[0].Card.Front.Path);

            model.AskColor = _ => Task.FromResult<ArgbColor?>(null);    // cancelled
            HeadlessFixture.Wait(model.ChooseBlankColorAsync());
            Assert.Equal(new ArgbColor(20, 40, 60), model.ImagePanel.BlankColor);
        });
    }

    [Fact]
    public void Save_style_button_writes_the_settings_where_the_user_says()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var model = new MainWindowViewModel();
            model.ProjectSettings.SelectedGroup = Group(model.ProjectSettings, SettingsGroup.Cards);
            string file = Path.Combine(fixture.NewFolder(), "saved.pnps");

            model.AskStyleFile = () => Task.FromResult<string?>(null);
            HeadlessFixture.Wait(model.SaveStyleAsync());
            Assert.False(File.Exists(file));

            model.AskStyleFile = () => Task.FromResult<string?>(file);
            HeadlessFixture.Wait(model.SaveStyleAsync());
            var read = new ProjectSettings();
            Assert.False(ProjectFileStore.LoadStyle(file, ref read).Error.HasError);
            Assert.Equal(63.5, read.Grid.CardSize.X.Millimeter, 2);
        });
    }

    private static System.Windows.Input.ICommand Command(MainWindowViewModel model, string key) =>
        model.Tabs.SelectMany(tab => tab.Groups).SelectMany(group => group.Buttons).First(button => button.Key == key).Command;

    // ---- Views ----

    [Fact]
    public void Settings_tab_shows_the_two_lists_and_the_window_the_shared_back()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            AppSettings.StartMaximized = false;
            AppSettings.WindowSize = new PointI(1280, 800);
            MainWindowViewModel model = ModelWith(6);
            var window = new MainWindow { DataContext = model };
            window.Show();
            model.SelectedTab = model.Tabs[2];
            Dispatcher.UIThread.RunJobs();

            List<ComboBox> lists = [.. window.GetVisualDescendants().OfType<ComboBox>().Where(list => list.Name is "GroupList" or "StyleList")];
            Assert.Equal(2, lists.Count);
            ComboBox groups = lists.First(list => list.Name == "GroupList");
            ComboBox styles = lists.First(list => list.Name == "StyleList");
            Assert.Equal(10, groups.ItemCount);
            Assert.False(styles.IsEnabled);    // a custom project has no styles to choose

            // Picking a group in the list, as the user does.
            groups.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(SettingsGroup.Cards, model.ProjectSettings.SelectedGroup!.Group);
            Assert.True(styles.IsEnabled);
            Assert.Equal(10, styles.ItemCount);
            Assert.Equal("Cartas_Standar.pnps", ((StyleOption)styles.SelectedItem!).FileName);

            Border back = window.FindControl<Border>("BackImageHost")!;
            Assert.False(back.IsVisible);
            var layout = new PrintLayout();
            layout.CopyFrom(model.ProjectSettings.Settings.PrintLayout);
            var grid = new Grid();
            grid.CopyFrom(model.ProjectSettings.Settings.Grid);
            layout.DoubleSided = true;
            layout.BackImage = BackImageMode.SameBack;
            model.ProjectSettings.ApplyLayout(layout, false, grid, true);
            model.SharedBack.SetImage(Picture(fixture.NewFolder(), "back.png"));
            HeadlessFixture.Wait(model.SharedBack.ImageLoaded);
            Dispatcher.UIThread.RunJobs();
            Assert.True(back.IsVisible);

            // As in the original, sides and backs alone do not make the project a custom one; rows do.
            Assert.Equal(SettingsGroup.Cards, ((StyleGroupOption)groups.SelectedItem!).Group);
            layout.Rows = 2;
            model.ProjectSettings.ApplyLayout(layout, false, grid, true);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(SettingsGroup.Custom, model.ProjectSettings.SelectedGroup!.Group);
            Assert.Equal(SettingsGroup.Custom, ((StyleGroupOption)groups.SelectedItem!).Group);
            Assert.False(styles.IsEnabled);
            Assert.True(model.ProjectSettings.Settings.PrintLayout.DoubleSided);
            Assert.True(back.IsVisible);

            SharedBackPanel panel = window.FindControl<SharedBackPanel>("SharedBackPanel")!;
            Assert.Equal(
                [
                    "ContextMenuStrip_BotonRotar_Izquierda", "ContextMenuStrip_BotonRotar_Derecha", "ContextMenuStrip_BotonRotar_Vertical",
                    "ContextMenuStrip_BotonRotar_Horizontal", "MenuContextualPanelImagen_AbrirCon", "MenuContextualPanelImagen_Explorador",
                ],
                panel.BuildMenu().Items.Cast<MenuItem>().Select(item => (string)item.Tag!));
            Screenshots.Capture(window, "ventana-configuracion");
            window.Close();
        });
    }

    [Fact]
    public void Face_change_window_offers_the_fourth_option_only_towards_two_faces()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var owner = new Window();
            owner.Show();
            var window = new FaceChangeWindow(toDouble: true);
            Task<FaceChangeChoice?> result = window.ShowDialog<FaceChangeChoice?>(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.True(window.FindControl<RadioButton>("MatchOption")!.IsVisible);
            Assert.Equal(FaceChangeChoice.Fronts, window.Choice);
            window.FindControl<RadioButton>("BothOption")!.IsChecked = true;
            Screenshots.Capture(window, "ventana-cambio-caras");
            window.Accept();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(FaceChangeChoice.Group, result.Result);

            window = new FaceChangeWindow(toDouble: false);
            result = window.ShowDialog<FaceChangeChoice?>(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.False(window.FindControl<RadioButton>("MatchOption")!.IsVisible);
            window.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.Null(result.Result);
            owner.Close();
        });
    }
}
