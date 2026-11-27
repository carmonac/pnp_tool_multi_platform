using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ImageMagick;
using PnpTool.App.Services;
using PnpTool.App.ViewModels;
using PnpTool.App.ViewModels.Panel;
using PnpTool.App.Views;
using PnpTool.Controls;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Primitives;
using PnpTool.Imaging;

namespace PnpTool.App.Tests;

[Collection(UserInterface.Name)]
public sealed class ImagePanelTests(HeadlessFixture fixture)
{
    /// <summary>Writes a plain image of the given size and gives its path.</summary>
    private static string Picture(string folder, string name, uint width = 120, uint height = 168, string color = "#3060C0")
    {
        string path = Path.Combine(folder, name);
        using var image = new MagickImage(new MagickColor(color), width, height);
        image.Density = new Density(300);
        image.Write(path);
        return path;
    }

    private ImagePanelViewModel PanelWith(int cards, bool isDouble = false)
    {
        string folder = fixture.NewFolder();
        var panel = new ImagePanelViewModel { IsDouble = isDouble };
        string[] files = [.. Enumerable.Range(1, cards * (isDouble ? 2 : 1)).Select(i => Picture(folder, "card" + i + ".png"))];
        Task<(int Added, AppError Error)> adding = panel.AddImagesAsync(files);
        HeadlessFixture.Wait(adding);
        Assert.Equal(cards, adding.Result.Added);
        return panel;
    }

    private static bool[] Selected(ImagePanelViewModel panel) => [.. panel.Cards.Select(card => card.IsSelected)];

    // ---- Adding ----

    [Fact]
    public void Images_are_added_in_natural_order_with_copies_from_their_names()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            string[] files = [Picture(folder, "carta10.png"), Picture(folder, "carta2 x3.png"), Picture(folder, "carta1.png")];
            var panel = new ImagePanelViewModel();
            int projectChanges = 0;
            panel.ProjectChanged += (_, _) => projectChanges++;

            Task<(int Added, AppError Error)> adding = panel.AddImagesAsync(files);
            HeadlessFixture.Wait(adding);

            Assert.Equal(3, adding.Result.Added);
            Assert.Equal(ErrorKind.None, adding.Result.Error.Kind);
            Assert.Equal(["carta1.png", "carta2 x3.png", "carta10.png"], panel.Cards.Select(card => Path.GetFileName(card.Card.Front.Path)));
            Assert.Equal([1, 3, 1], panel.Cards.Select(card => card.Copies));
            Assert.Equal([0, 1, 2], panel.Cards.Select(card => card.Index));
            Assert.Equal("Paneles: 3 / Cartas: 5", panel.CountsText);
            Assert.Equal(1, projectChanges);

            // 120 x 168 pixels at 300 dpi, in the program's unit.
            string size = panel.Cards[0].Card.Front.Size.X.ToDisplayString() + "-" + panel.Cards[0].Card.Front.Size.Y.ToDisplayString();
            Assert.Equal("#1 " + size, panel.Cards[0].Title);
            Assert.Equal("#3 " + size, panel.Cards[2].Title);
            Assert.Equal("10.16-14.22", size);
        });
    }

    [Fact]
    public void With_backs_shown_files_are_taken_in_pairs()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            (string[] fronts, string[] backs, AppError error) = ImagePanelViewModel.SplitFiles(["c4.png", "c1.png", "c3.png", "c2.png"], isDouble: true);
            Assert.Equal(["c1.png", "c3.png"], fronts);
            Assert.Equal(["c2.png", "c4.png"], backs);
            Assert.False(error.HasError);

            (fronts, backs, error) = ImagePanelViewModel.SplitFiles(["c1.png", "c2.png", "c3.png"], isDouble: true);
            Assert.Empty(fronts);
            Assert.True(error.HasError);
            Assert.Equal(StringResources.Get("imagenes_par"), error.Message);

            (fronts, backs, error) = ImagePanelViewModel.SplitFiles(["c10.png", "c9.png"], isDouble: false);
            Assert.Equal(["c9.png", "c10.png"], fronts);
            Assert.Empty(backs);

            ImagePanelViewModel panel = PanelWith(2, isDouble: true);
            Assert.Equal(["card1.png", "card3.png"], panel.Cards.Select(card => Path.GetFileName(card.Card.Front.Path)));
            Assert.Equal(["card2.png", "card4.png"], panel.Cards.Select(card => Path.GetFileName(card.Card.Back.Path)));
            Assert.Contains(" @ ", panel.Cards[0].Title);
            Assert.Equal(190, panel.Cards[0].Width);
        });
    }

    [Fact]
    public void Cards_go_where_asked_and_blank_and_empty_cards_can_be_added()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(2);
            panel.BlankColor = new ArgbColor(10, 20, 30);

            panel.AddBlankCard(position: 1);
            panel.AddEmptyCard();
            panel.AddEmptyCard(position: 99);

            Assert.Equal(
                [ImageKind.Normal, ImageKind.Blank, ImageKind.Normal, ImageKind.Empty, ImageKind.Empty],
                panel.Cards.Select(card => card.Card.Front.ImageKind));
            Assert.Equal("255_10_20_30", panel.Cards[1].Card.Front.Path);
            Assert.Equal("255_10_20_30", panel.Cards[1].Card.Back.Path);
            Assert.Equal(ImageKind.Blank, panel.Cards[1].Card.Back.ImageKind);
            Assert.Equal([0, 1, 2, 3, 4], panel.Cards.Select(card => card.Index));
            Assert.Equal(5, panel.CardCount);

            Deck deck = panel.ToDeck();
            Assert.Equal(5, deck.Cards.Length);
            Assert.Equal("255_10_20_30", deck.Cards[1].Front.Path);
            Assert.Equal(1, deck.Cards[3].Copies);
        });
    }

    [Fact]
    public void A_missing_image_is_reported_and_shown_as_not_found()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            string missing = Path.Combine(folder, "gone.png");
            var panel = new ImagePanelViewModel();

            Task<(int Added, AppError Error)> adding = panel.AddImagesAsync([Picture(folder, "here.png"), missing]);
            HeadlessFixture.Wait(adding);
            HeadlessFixture.Wait(panel.ImagesLoaded);

            Assert.Equal(2, adding.Result.Added);
            Assert.True(adding.Result.Error.HasWarning);
            Assert.Equal(missing, adding.Result.Error.Message);
            Assert.NotNull(panel.Cards[0].FrontImage);
            Assert.NotNull(panel.Cards[1].FrontImage);
        });
    }

    // ---- Pictures ----

    [Fact]
    public void Pictures_are_small_and_follow_the_turns_of_the_face()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            var panel = new ImagePanelViewModel();
            HeadlessFixture.Wait(panel.AddImagesAsync([Picture(folder, "big.png", 600, 900)]));
            HeadlessFixture.Wait(panel.ImagesLoaded);
            CardPanelViewModel card = panel.Cards[0];

            Assert.Equal(96, panel.ThumbnailSize);
            Assert.Equal(new PixelSize(64, 96), card.FrontImage!.PixelSize);
            Assert.Null(card.BackImage);   // backs are not shown

            card.Card.Front.Rotation.AddAngle(90);
            card.RefreshImages();
            HeadlessFixture.Wait(card.ImagesLoaded);
            Assert.Equal(new PixelSize(96, 64), card.FrontImage!.PixelSize);

            panel.IsDouble = true;
            HeadlessFixture.Wait(panel.ImagesLoaded);
            Assert.NotNull(card.BackImage);   // the "no image" picture
            Assert.True(File.Exists(FaceImages.EmptyImagePath));
        });
    }

    [Fact]
    public void A_blank_face_is_drawn_in_its_colour()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            DisplayImage picture = FaceImages.Render(new CardFace("255_200_100_50", FaceType.Front, ImageKind.Blank), 96);

            Assert.Equal([50, 100, 200, 255], picture.Pixels[..4]);   // blue, green, red, alpha
            Assert.Equal(picture.Width * picture.Height * 4, picture.Pixels.Length);
            Assert.Equal(new PixelSize(picture.Width, picture.Height), FaceImages.ToBitmap(picture).PixelSize);
        });
    }

    // ---- Selecting cards ----

    [Fact]
    public void A_click_selects_one_card_and_a_second_click_unselects_it()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(4);

            panel.ClickCard(1, shift: false, command: false);
            Assert.Equal([false, true, false, false], Selected(panel));

            panel.ClickCard(3, shift: false, command: false);
            Assert.Equal([false, false, false, true], Selected(panel));

            panel.ClickCard(3, shift: false, command: false);
            Assert.Equal([false, false, false, false], Selected(panel));

            panel.ClickCard(9, shift: false, command: false);   // no such card
            Assert.Equal(0, panel.SelectedPanelCount);
        });
    }

    [Fact]
    public void The_command_key_adds_to_the_selection_and_takes_away_from_it()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(4);
            panel.Cards[2].Copies = 5;

            panel.ClickCard(0, shift: false, command: false);
            panel.ClickCard(2, shift: false, command: true);
            Assert.Equal([true, false, true, false], Selected(panel));
            Assert.Equal("Paneles: 4 / Cartas: 8   " + StringResources.Get("Elementos_Seleccionados") + ": 2 / 6", panel.CountsText);

            panel.ClickCard(0, shift: false, command: true);
            Assert.Equal([false, false, true, false], Selected(panel));

            // As in the Windows program, a plain click on a selected card unselects it and leaves the rest.
            panel.ClickCard(1, shift: false, command: true);
            panel.ClickCard(2, shift: false, command: false);
            Assert.Equal([false, true, false, false], Selected(panel));
        });
    }

    [Fact]
    public void Shift_selects_the_cards_up_to_the_last_one_clicked()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(6);

            panel.ClickCard(1, shift: false, command: false);
            panel.ClickCard(4, shift: true, command: false);
            Assert.Equal([false, true, true, true, true, false], Selected(panel));

            // Backwards, from the same starting card.
            panel.ClearCardSelection();
            panel.ClickCard(4, shift: false, command: false);
            panel.ClickCard(0, shift: true, command: false);
            Assert.Equal([true, true, true, true, true, false], Selected(panel));
        });
    }

    [Fact]
    public void Cards_and_faces_can_be_selected_by_range_and_unselected()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(4, isDouble: true);
            int counts = 0;
            panel.CountsChanged += (_, _) => counts++;

            panel.Select(1, 3, cards: true, FaceType.Back);
            Assert.Equal([false, true, true, false], Selected(panel));
            Assert.Equal([false, true, true, false], panel.Cards.Select(card => card.IsBackSelected));
            Assert.DoesNotContain(true, panel.Cards.Select(card => card.IsFrontSelected));
            Assert.Equal(2, panel.SelectedFaces.Count);
            Assert.True(counts > 0);

            panel.ClearCardSelection();
            Assert.Equal(0, panel.SelectedPanelCount);
            Assert.Equal(2, panel.SelectedFaces.Count);

            panel.Select(0, 99, cards: false, FaceType.Both);
            Assert.Equal(8, panel.SelectedFaces.Count);
            Assert.Equal(0, panel.SelectedPanelCount);

            panel.ClearSelection();
            Assert.Empty(panel.SelectedFaces);
            Assert.False(panel.Cards[0].Card.Front.Selected);
        });
    }

    // ---- Faces ----

    [Fact]
    public void Two_selected_faces_can_be_swapped()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(3, isDouble: true);
            string front0 = panel.Cards[0].Card.Front.Path;
            string back2 = panel.Cards[2].Card.Back.Path;

            Assert.False(panel.SwapFaces());
            panel.ClickFace(0, FaceType.Front);
            Assert.False(panel.CanSwapFaces);
            panel.ClickFace(2, FaceType.Back);
            Assert.True(panel.CanSwapFaces);
            Assert.True(panel.Cards[0].Card.Front.Selected);

            Assert.True(panel.SwapFaces());
            HeadlessFixture.Wait(panel.ImagesLoaded);

            Assert.Equal(back2, panel.Cards[0].Card.Front.Path);
            Assert.Equal(front0, panel.Cards[2].Card.Back.Path);
            Assert.Equal(FaceType.Front, panel.Cards[0].Card.Front.FaceType);
            Assert.Equal(FaceType.Back, panel.Cards[2].Card.Back.FaceType);
            Assert.True(panel.Cards[0].IsFrontSelected);
            Assert.True(panel.Cards[2].IsBackSelected);

            panel.ClickFace(1, FaceType.Front);
            Assert.False(panel.CanSwapFaces);   // three faces
            panel.ClickFace(1, FaceType.Front);
            Assert.True(panel.CanSwapFaces);
        });
    }

    [Fact]
    public void A_face_without_image_cannot_be_selected()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var panel = new ImagePanelViewModel();
            panel.AddEmptyCard();
            panel.AddBlankCard();

            panel.ClickFace(0, FaceType.Front);
            panel.ClickFace(1, FaceType.Front);

            Assert.False(panel.Cards[0].IsFrontSelected);
            Assert.True(panel.Cards[1].IsFrontSelected);
        });
    }

    // ---- Removing, copies, zoom ----

    [Fact]
    public void Selected_cards_are_deleted_and_the_rest_renumbered()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(5);
            string[] paths = [.. panel.Cards.Select(card => card.Card.Front.Path)];
            panel.ClickFace(1, FaceType.Front);
            panel.ClickFace(4, FaceType.Front);
            panel.ClickCard(1, shift: false, command: false);
            panel.ClickCard(3, shift: false, command: true);

            Assert.Equal(2, panel.DeleteSelected());

            Assert.Equal([paths[0], paths[2], paths[4]], panel.Cards.Select(card => card.Card.Front.Path));
            Assert.Equal([0, 1, 2], panel.Cards.Select(card => card.Index));
            Assert.StartsWith("#3 ", panel.Cards[2].Title);
            Assert.Equal("Paneles: 3 / Cartas: 3", panel.CountsText);
            Assert.Single(panel.SelectedFaces);   // the face of the deleted card is no longer counted
            Assert.True(File.Exists(paths[1]));   // the user's own files are never deleted
            Assert.Equal(0, panel.DeleteSelected());

            panel.Clear();
            Assert.Empty(panel.Cards);
            Assert.Empty(panel.SelectedFaces);
            Assert.Equal("Paneles: 0 / Cartas: 0", panel.CountsText);
        });
    }

    [Fact]
    public void Copies_change_the_number_of_cards()
    {
        fixture.Run(LanguageId.EnglishUk, () =>
        {
            ImagePanelViewModel panel = PanelWith(2);
            int changes = 0;
            panel.ProjectChanged += (_, _) => changes++;

            panel.Cards[0].Copies = 4;
            panel.Cards[1].Copies = 0;

            Assert.Equal(4, panel.CardCount);
            Assert.Equal(4, panel.Cards[0].Card.Copies);
            Assert.Equal(4, panel.ToDeck().Cards[0].Copies);
            Assert.Equal(2, changes);
            Assert.Equal(StringResources.Get("Paneles") + ": 2 / " + StringResources.Get("Cartas") + ": 4", panel.CountsText);
        });
    }

    [Fact]
    public void Zoom_changes_the_size_of_the_cards()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(1);
            CardPanelViewModel card = panel.Cards[0];
            var changed = new List<string?>();
            card.PropertyChanged += (_, change) => changed.Add(change.PropertyName);

            Assert.Equal((95, 155), (card.Width, card.Height));
            panel.Zoom = 200;
            Assert.Equal((190, 310), (card.Width, card.Height));
            Assert.Contains(nameof(CardPanelViewModel.Width), changed);

            panel.IsDouble = true;
            Assert.Equal(380, card.Width);
        });
    }

    [Fact]
    public void Long_paths_are_shortened_in_the_larger_view()
    {
        string path = "/Users/someone/Documents/games/" + new string('a', 60) + "/card-number-one.png";

        Assert.Equal("/short/path.png", CardZoomPanel.ShortPath("/short/path.png"));
        Assert.Equal(53, CardZoomPanel.ShortPath(path).Length);
        Assert.StartsWith("/Users/someone/Documents/", CardZoomPanel.ShortPath(path));
        Assert.EndsWith("card-number-one.png", CardZoomPanel.ShortPath(path));
    }

    // ---- The view ----

    private static (Window Window, ImagePanel View) Show(ImagePanelViewModel panel, int width = 700, int height = 500)
    {
        var view = new ImagePanel { DataContext = panel };
        var window = new Window { Width = width, Height = height, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view);
    }

    private static List<CardPanel> CardsOf(ImagePanel view) => [.. view.GetVisualDescendants().OfType<CardPanel>()];

    private static Point CentreOf(Visual visual, Window window) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)!.Value;

    private static void Click(Window window, Point point, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.MouseDown(point, MouseButton.Left, modifiers);
        window.MouseUp(point, MouseButton.Left, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void Clicks_on_the_panel_select_cards_and_faces()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(5);
            HeadlessFixture.Wait(panel.ImagesLoaded);
            (Window window, ImagePanel view) = Show(panel);
            List<CardPanel> cards = CardsOf(view);
            Assert.Equal(5, cards.Count);
            Assert.Equal(panel.Cards[0].Title, cards[0].FindControl<TextBlock>("TitleLabel")!.Text);
            Assert.Equal(95, cards[0].Bounds.Width);
            Point Title(int index) => CentreOf(cards[index].FindControl<TextBlock>("TitleLabel")!, window);
            Point Face(int index) => CentreOf(cards[index].FindControl<Border>("FrontFace")!, window);
            RawInputModifiers command = Application.Current!.PlatformSettings!.HotkeyConfiguration.CommandModifiers == KeyModifiers.Meta
                ? RawInputModifiers.Meta
                : RawInputModifiers.Control;

            // The title selects the card and shows its tick.
            Click(window, Title(1));
            Assert.Equal([false, true, false, false, false], Selected(panel));
            Assert.True(cards[1].FindControl<Image>("CardCheck")!.IsVisible);
            Assert.False(cards[0].FindControl<Image>("CardCheck")!.IsVisible);

            Click(window, Title(3), command);
            Assert.Equal([false, true, false, true, false], Selected(panel));

            Click(window, Title(0));
            Click(window, Title(4), RawInputModifiers.Shift);
            Assert.Equal([true, true, true, true, true], Selected(panel));

            // The picture selects the face and leaves the cards as they are.
            Click(window, Face(2));
            Assert.True(panel.Cards[2].IsFrontSelected);
            Assert.True(cards[2].FindControl<Image>("FrontCheck")!.IsVisible);
            Assert.Equal(5, panel.SelectedPanelCount);

            // A click outside the cards unselects them, not the faces.
            Click(window, new Point(600, 450));
            Assert.Equal(0, panel.SelectedPanelCount);
            Assert.True(panel.Cards[2].IsFrontSelected);

            Screenshots.Capture(window, "panel-imagenes");
            window.Close();
        });
    }

    [Fact]
    public void The_copies_box_of_a_card_changes_its_copies_without_selecting_it()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(2);
            (Window window, ImagePanel view) = Show(panel);
            List<CardPanel> cards = CardsOf(view);
            NumericBox box = cards[1].FindControl<NumericBox>("CopiesBox")!;
            Assert.Equal(1, box.Value);

            Click(window, CentreOf(box.FindControl<RepeatButton>("UpButton")!, window));
            Assert.Equal(2, panel.Cards[1].Copies);
            Assert.Equal(3, panel.CardCount);
            Assert.Equal(0, panel.SelectedPanelCount);

            panel.Cards[0].Copies = 7;
            Assert.Equal(7, cards[0].FindControl<NumericBox>("CopiesBox")!.Value);
            window.Close();
        });
    }

    [Fact]
    public void Resting_on_a_face_shows_it_larger()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(2);
            (Window window, ImagePanel view) = Show(panel);
            List<CardPanel> cards = CardsOf(view);
            Popup popup = view.FindControl<Popup>("ZoomPopup")!;
            Point face = CentreOf(cards[0].FindControl<Border>("FrontFace")!, window);

            window.MouseMove(face);
            Dispatcher.UIThread.RunJobs();
            Assert.False(popup.IsOpen);   // not at once

            var watch = Stopwatch.StartNew();
            while (!popup.IsOpen && watch.Elapsed < TimeSpan.FromSeconds(5))
            {
                Thread.Sleep(20);
                Dispatcher.UIThread.RunJobs();
            }

            Assert.True(popup.IsOpen);
            Assert.True(watch.Elapsed >= ImagePanel.ZoomDelay - TimeSpan.FromMilliseconds(100));
            Assert.Same(cards[0], popup.PlacementTarget);

            window.MouseMove(new Point(600, 450));
            Dispatcher.UIThread.RunJobs();
            Assert.False(popup.IsOpen);
            window.Close();
        });
    }

    [Fact]
    public void Main_window_shows_the_cards_their_counts_and_the_zoom()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            AppSettings.StartMaximized = false;
            AppSettings.WindowSize = new PointI(1280, 800);
            string folder = fixture.NewFolder();
            string[] colors = ["#C03030", "#30A050", "#3060C0", "#C0A030", "#8040A0", "#209090"];
            string[] files = [.. Enumerable.Range(1, 12).Select(i => Picture(folder, "carta" + i + (i == 3 ? " x4" : string.Empty) + ".png", 300, 420, colors[i % 6]))];
            var model = new MainWindowViewModel { PickImages = _ => Task.FromResult<IReadOnlyList<string>>(files) };
            var window = new MainWindow { DataContext = model };
            model.PickImages = _ => Task.FromResult<IReadOnlyList<string>>(files);
            window.Show();
            Dispatcher.UIThread.RunJobs();

            HeadlessFixture.Wait(model.AddImagesAsync());
            HeadlessFixture.Wait(model.ImagePanel.ImagesLoaded);
            model.ImagePanel.AddBlankCard();
            model.ImagePanel.ClickCard(1, shift: false, command: false);
            model.ImagePanel.ClickCard(2, shift: false, command: true);
            HeadlessFixture.Wait(model.ImagePanel.ImagesLoaded);

            Assert.Equal("Paneles: 13 / Cartas: 16   " + StringResources.Get("Elementos_Seleccionados") + ": 2 / 5", window.FindControl<TextBlock>("CountsLabel")!.Text);
            Assert.Equal(13, CardsOf(window.FindControl<ImagePanel>("CardsPanel")!).Count);
            Assert.Equal(0, model.Progress);

            ZoomSlider slider = window.FindControl<ZoomSlider>("ZoomSlider")!;
            Assert.Equal((75.0, 300.0, 100.0), (slider.Minimum, slider.Maximum, slider.Value));
            Assert.Equal(95, model.ImagePanel.Cards[0].Width);
            slider.Step(1);
            Assert.Equal(125, model.ImagePanel.Zoom);
            Assert.Equal(119, model.ImagePanel.Cards[0].Width);
            slider.Value = 150;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Math.Round(95 * 1.5), CardsOf(window.FindControl<ImagePanel>("CardsPanel")!)[0].Bounds.Width);
            Screenshots.Capture(window, "ventana-con-cartas");
            window.Close();
        });
    }

    [Fact]
    public void Command_bar_buttons_act_on_the_panel()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var model = new MainWindowViewModel();
            ImagePanelViewModel panel = model.ImagePanel;
            RibbonCommand("Form_Principal_Boton_AnadirImagenBlanca").Execute(null);
            RibbonCommand("Form_Principal_Boton_AnadirImagenBlanca").Execute(null);
            RibbonCommand("Form_Principal_Boton_AnadirImagenVacia").Execute(null);
            Assert.Equal("Paneles: 3 / Cartas: 3", model.CountsText);

            System.Windows.Input.ICommand swap = RibbonCommand("Form_Principal_Boton_IntercambiarImagenes");
            Assert.False(swap.CanExecute(null));
            panel.ClickFace(0, FaceType.Front);
            panel.ClickFace(1, FaceType.Front);
            Assert.True(swap.CanExecute(null));

            RibbonCommand("Form_Principal_Boton_SeleccionarTodos").Execute(null);
            Assert.Equal(3, panel.SelectedPanelCount);
            Assert.Empty(panel.SelectedFaces);
            Assert.False(swap.CanExecute(null));

            RibbonCommand("Form_Principal_Boton_BorrarSeleccion").Execute(null);
            Assert.Equal(0, panel.SelectedPanelCount);

            panel.ClickCard(2, shift: false, command: false);
            RibbonCommand("Form_Principal_Boton_Eliminar").Execute(null);
            Assert.Equal("Paneles: 2 / Cartas: 2", model.CountsText);

            System.Windows.Input.ICommand RibbonCommand(string key) =>
                model.Tabs.SelectMany(tab => tab.Groups).SelectMany(group => group.Buttons).First(button => button.Key == key).Command;
        });
    }

    // ---- Many cards ----

    [Fact]
    public void Five_hundred_cards_are_shown_and_scrolled()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            string[] colors = ["#C03030", "#30A050", "#3060C0", "#C0A030", "#8040A0", "#209090"];
            string[] files = [.. Enumerable.Range(1, 500).Select(i => Picture(folder, "c" + i + ".jpg", 750, 1050, colors[i % 6]))];
            var panel = new ImagePanelViewModel();
            (Window window, ImagePanel view) = Show(panel, 1280, 800);
            GC.Collect();
            long before = GC.GetTotalMemory(forceFullCollection: true);

            var watch = Stopwatch.StartNew();
            HeadlessFixture.Wait(panel.AddImagesAsync(files), 180);
            TimeSpan added = watch.Elapsed;
            HeadlessFixture.Wait(panel.ImagesLoaded, 180);
            TimeSpan loaded = watch.Elapsed;

            watch.Restart();
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(window.CaptureRenderedFrame());
            TimeSpan firstFrame = watch.Elapsed;

            ScrollViewer scroller = view.FindControl<ScrollViewer>("Scroller")!;
            watch.Restart();
            const int Steps = 40;
            for (int i = 1; i <= Steps; i++)
            {
                scroller.Offset = new Vector(0, scroller.Extent.Height * i / Steps);
                Dispatcher.UIThread.RunJobs();
                Assert.NotNull(window.CaptureRenderedFrame());
            }

            double perFrame = watch.Elapsed.TotalMilliseconds / Steps;
            watch.Restart();
            panel.Zoom = 150;
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(window.CaptureRenderedFrame());
            TimeSpan zoomed = watch.Elapsed;
            long managed = GC.GetTotalMemory(forceFullCollection: true) - before;
            long pictures = panel.Cards.Sum(card => (long)card.FrontImage!.PixelSize.Width * card.FrontImage.PixelSize.Height * 4);

            TestContext.Current.SendDiagnosticMessage(
                "500 cards: read {0:F1} s, pictures {1:F1} s, first frame {2:F0} ms, scroll {3:F1} ms/frame, zoom {4:F0} ms, managed {5:F1} MB, pictures {6:F1} MB",
                added.TotalSeconds, loaded.TotalSeconds, firstFrame.TotalMilliseconds, perFrame, zoomed.TotalMilliseconds, managed / 1048576.0, pictures / 1048576.0);
            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "pnptool-500-cards.txt"),
                FormattableString.Invariant($"read {added.TotalSeconds:F1} s; pictures {loaded.TotalSeconds:F1} s; first frame {firstFrame.TotalMilliseconds:F0} ms; scroll {perFrame:F1} ms/frame; zoom {zoomed.TotalMilliseconds:F0} ms; managed {managed / 1048576.0:F1} MB; pictures {pictures / 1048576.0:F1} MB"));

            Assert.Equal(500, panel.PanelCount);
            Assert.InRange(CardsOf(view).Count, view.Columns, 100);   // only the rows in view have controls
            Assert.True(scroller.Extent.Height > scroller.Viewport.Height * 5);
            Assert.True(pictures < 20 * 1048576, "Pictures take " + pictures + " bytes");
            Assert.True(perFrame < 100, "A scrolled frame takes " + perFrame + " ms");
            Screenshots.Capture(window, "panel-500-cartas");
            window.Close();
        });
    }
}
