using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ImageMagick;
using PnpTool.App.ViewModels;
using PnpTool.App.ViewModels.Panel;
using PnpTool.App.Views;
using PnpTool.Controls;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Primitives;

namespace PnpTool.App.Tests;

[Collection(UserInterface.Name)]
public sealed class PanelInteractionTests(HeadlessFixture fixture)
{
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
        HeadlessFixture.Wait(panel.AddImagesAsync(files));
        return panel;
    }

    private static string[] Names(ImagePanelViewModel panel) =>
        [.. panel.Cards.Select(card => Path.GetFileNameWithoutExtension(card.Card.Front.Path))];

    // ---- Moving, cloning, deleting, copies ----

    [Fact]
    public void A_card_moves_to_a_position_and_the_others_shift()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(5);
            int changes = 0;
            panel.ProjectChanged += (_, _) => changes++;

            panel.Move(0, 3);
            Assert.Equal(["card2", "card3", "card4", "card1", "card5"], Names(panel));
            Assert.Equal([0, 1, 2, 3, 4], panel.Cards.Select(card => card.Index));

            panel.Move(4, 0);
            Assert.Equal(["card5", "card2", "card3", "card4", "card1"], Names(panel));

            panel.Move(1, 99);   // past the end
            Assert.Equal(["card5", "card3", "card4", "card1", "card2"], Names(panel));
            Assert.Equal(3, changes);

            panel.Move(2, 2);
            panel.Move(9, 0);
            Assert.Equal(3, changes);
            Assert.StartsWith("#5 ", panel.Cards[4].Title);
        });
    }

    [Fact]
    public void A_moved_card_keeps_its_selection_and_copies()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(3);
            panel.Cards[0].Copies = 4;
            panel.ClickCard(0, shift: false, command: false);
            panel.ClickFace(0, FaceType.Front);

            panel.Move(0, 2);

            Assert.True(panel.Cards[2].IsSelected);
            Assert.True(panel.Cards[2].IsFrontSelected);
            Assert.Equal(4, panel.Cards[2].Copies);
            Assert.Equal(1, panel.SelectedPanelCount);
            Assert.Single(panel.SelectedFaces);
        });
    }

    [Fact]
    public void A_clone_goes_before_its_card_and_is_a_card_of_its_own()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(3);
            panel.Cards[1].Copies = 3;
            panel.ClickCard(1, shift: false, command: false);

            panel.Clone(1);

            Assert.Equal(["card1", "card2", "card2", "card3"], Names(panel));
            Assert.Equal([1, 3, 3, 1], panel.Cards.Select(card => card.Copies));
            Assert.False(panel.Cards[1].IsSelected);   // the copy
            Assert.True(panel.Cards[2].IsSelected);    // the original
            Assert.NotSame(panel.Cards[1].Card, panel.Cards[2].Card);
            panel.Cards[1].Copies = 9;
            Assert.Equal(3, panel.Cards[2].Copies);
            Assert.Equal("Paneles: 4 / Cartas: 14", panel.CountsText.Split("   ")[0]);
        });
    }

    [Fact]
    public void One_card_can_be_deleted_whatever_is_selected()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(4);
            panel.ClickCard(0, shift: false, command: false);

            panel.DeleteAt(2);

            Assert.Equal(["card1", "card2", "card4"], Names(panel));
            Assert.Equal([0, 1, 2], panel.Cards.Select(card => card.Index));
            Assert.True(panel.Cards[0].IsSelected);
            panel.DeleteAt(7);
            Assert.Equal(3, panel.PanelCount);
        });
    }

    [Fact]
    public void Copies_are_given_to_the_card_and_to_the_selected_ones()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(5);
            panel.ClickCard(0, shift: false, command: false);
            panel.ClickCard(1, shift: false, command: true);

            panel.SetCopies(3, 6);

            Assert.Equal([6, 6, 1, 6, 1], panel.Cards.Select(card => card.Copies));
            Assert.Equal(20, panel.CardCount);
        });
    }

    // ---- Faces ----

    [Fact]
    public void A_face_is_turned_alone_unless_it_is_selected()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(3, isDouble: true);
            HeadlessFixture.Wait(panel.ImagesLoaded);
            string upright = panel.Cards[0].Title;

            panel.RotateFace(0, FaceType.Front, new RotationProperties(90, false, false));
            HeadlessFixture.Wait(panel.ImagesLoaded);

            Assert.Equal(90, panel.Cards[0].Card.Front.Rotation.Angle);
            Assert.Equal(0, panel.Cards[0].Card.Back.Rotation.Angle);
            Assert.Equal(0, panel.Cards[1].Card.Front.Rotation.Angle);
            Assert.NotEqual(upright, panel.Cards[0].Title);                          // width and height of the front swapped
            Assert.Equal(new PixelSize(96, 69), panel.Cards[0].FrontImage!.PixelSize);
            Assert.Equal(new PixelSize(69, 96), panel.Cards[1].FrontImage!.PixelSize);

            // Selected faces turn together, whichever of them the menu was opened on.
            panel.ClickFace(1, FaceType.Front);
            panel.ClickFace(2, FaceType.Back);
            panel.RotateFace(2, FaceType.Back, new RotationProperties(-90, false, false));
            Assert.Equal(270, panel.Cards[1].Card.Front.Rotation.Angle);
            Assert.Equal(270, panel.Cards[2].Card.Back.Rotation.Angle);
            Assert.Equal(0, panel.Cards[2].Card.Front.Rotation.Angle);
            Assert.Equal(90, panel.Cards[0].Card.Front.Rotation.Angle);

            panel.RotateFace(0, FaceType.Back, new RotationProperties(0, true, false));
            panel.RotateFace(0, FaceType.Back, new RotationProperties(0, false, true));
            Assert.True(panel.Cards[0].Card.Back.Rotation.FlipHorizontal);
            Assert.True(panel.Cards[0].Card.Back.Rotation.FlipVertical);
        });
    }

    [Fact]
    public void A_face_gets_another_image_and_stays_the_face_it_was()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(3, isDouble: true);
            string other = Picture(fixture.NewFolder(), "other x5.png", 200, 100);
            panel.Cards[0].Copies = 2;

            panel.ChangeFaceImage(0, FaceType.Back, other);

            CardFace back = panel.Cards[0].Card.Back;
            Assert.Equal(other, back.Path);
            Assert.Equal(FaceType.Back, back.FaceType);
            Assert.False(back.Selected);
            Assert.Equal(2, panel.Cards[0].Copies);   // the "x5" of the file name is for new cards only
            Assert.True(back.Size.X.Point > back.Size.Y.Point);
            Assert.Contains(" @ ", panel.Cards[0].Title);

            panel.ClickFace(1, FaceType.Front);
            panel.ClickFace(2, FaceType.Front);
            panel.ChangeFaceImage(2, FaceType.Front, other);
            Assert.Equal(other, panel.Cards[1].Card.Front.Path);
            Assert.Equal(other, panel.Cards[2].Card.Front.Path);
            Assert.True(panel.Cards[1].IsFrontSelected);
            Assert.True(panel.Cards[1].Card.Front.Selected);
            Assert.NotEqual(other, panel.Cards[0].Card.Front.Path);
        });
    }

    [Fact]
    public void A_face_can_be_made_blank()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(2);
            panel.BlankColor = new ArgbColor(1, 2, 3);

            panel.ChangeFaceToBlank(1, FaceType.Front);

            Assert.Equal(ImageKind.Blank, panel.Cards[1].Card.Front.ImageKind);
            Assert.Equal("255_1_2_3", panel.Cards[1].Card.Front.Path);
            Assert.Equal(FaceType.Front, panel.Cards[1].Card.Front.FaceType);
            Assert.Equal(ImageKind.Normal, panel.Cards[0].Card.Front.ImageKind);
        });
    }

    [Fact]
    public void One_image_goes_on_a_range_of_cards_or_on_the_selected_ones()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(5, isDouble: true);
            string file = Picture(fixture.NewFolder(), "shared-back.png");
            CardFace image = ImageInspector.Current.GetImageProperties(file);

            panel.ChangeImages(image, FaceType.Back, 1, 4);
            Assert.Equal([false, true, true, true, false], panel.Cards.Select(card => card.Card.Back.Path == file));
            Assert.All(panel.Cards, card => Assert.Equal(FaceType.Back, card.Card.Back.FaceType));
            Assert.All(panel.Cards, card => Assert.NotEqual(file, card.Card.Front.Path));

            panel.ClickFace(0, FaceType.Front);
            panel.ClickFace(4, FaceType.Front);
            panel.ClickFace(2, FaceType.Back);
            panel.ChangeSelectedImages(image, FaceType.Front);
            Assert.Equal([true, false, false, false, true], panel.Cards.Select(card => card.Card.Front.Path == file));
            Assert.True(panel.Cards[0].IsFrontSelected);
            Assert.Equal(3, panel.SelectedFaces.Count);

            panel.ChangeImages(image, FaceType.Front, -5, 99);   // limits outside the panel
            Assert.All(panel.Cards, card => Assert.Equal(file, card.Card.Front.Path));
        });
    }

    [Fact]
    public void A_background_colour_frames_the_picture()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(1);
            Assert.Null(panel.Cards[0].FrontBackground);

            panel.SetFaceBackground(0, FaceType.Front, new ArgbColor(200, 0, 0));
            Assert.Equal("255_200_0_0", panel.Cards[0].Card.Front.BackgroundColor);
            Assert.Equal(new ArgbColor(200, 0, 0), panel.Cards[0].FrontBackground);

            var view = new CardPanel { DataContext = panel.Cards[0] };
            Border face = view.FindControl<Border>("FrontFace")!;
            Assert.Equal(new Thickness(5), face.BorderThickness);

            panel.SetFaceBackground(0, FaceType.Front, ArgbColor.White);   // white means none
            Assert.Null(panel.Cards[0].FrontBackground);
            Assert.Equal(new Thickness(0), face.BorderThickness);
        });
    }

    // ---- Dropped files ----

    [Fact]
    public void Dropped_folders_give_their_images_and_other_files_are_left_out()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            string inner = Directory.CreateDirectory(Path.Combine(folder, "more")).FullName;
            string a = Picture(folder, "a10.png");
            string b = Picture(folder, "a2.png");
            string c = Picture(inner, "z.png");
            string d = Picture(inner, "y.png");
            string text = Path.Combine(folder, "notes.txt");
            File.WriteAllText(text, "not an image");

            string[] images = ImagePanelViewModel.ImagesAmong([a, inner, text, b]);

            Assert.Equal([b, a, d, c], images);   // "a2" before "a10", then the folder's images in order
        });
    }

    [Fact]
    public void Files_dropped_on_a_card_go_before_it_and_a_project_file_asks_to_be_opened()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(3);
            string folder = fixture.NewFolder();
            string[] dropped = [Picture(folder, "new1.png"), Picture(folder, "new2.png")];
            var asked = new List<string>();
            panel.ProjectFileDropped += asked.Add;

            Task<(int Added, AppError Error)> onCard = panel.DropFilesAsync(dropped, onCard: 1);
            HeadlessFixture.Wait(onCard);
            Assert.Equal(2, onCard.Result.Added);
            Assert.Equal(["card1", "new1", "new2", "card2", "card3"], Names(panel));

            HeadlessFixture.Wait(panel.DropFilesAsync([Picture(folder, "last.png")]));
            Assert.Equal("last", Names(panel)[^1]);

            string project = Path.Combine(folder, "game.pnp");
            File.WriteAllText(project, "x");
            Task<(int Added, AppError Error)> opened = panel.DropFilesAsync([project]);
            HeadlessFixture.Wait(opened);
            Assert.Equal([project], asked);
            Assert.Equal(0, opened.Result.Added);
            Assert.Equal(6, panel.PanelCount);
        });
    }

    // ---- Menus ----

    private static string[] Keys(ContextMenu menu) => [.. menu.Items.Cast<MenuItem>().Select(item => (string)item.Tag!)];

    private static void Choose(ContextMenu menu, string key)
    {
        menu.Items.Cast<MenuItem>().First(item => (string)item.Tag! == key).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void Menus_offer_what_the_windows_program_offers()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(2);
            panel.AddEmptyCard();
            var view = new ImagePanel { DataContext = panel };

            ContextMenu forPanel = view.BuildMenu(null, null);
            Assert.Equal(["Añadir_imagen_final", "Añadir_imagen_blanca_final", "Añadir_imagen_vacia_final"], Keys(forPanel));
            Assert.All(forPanel.Items.Cast<MenuItem>(), item => Assert.DoesNotContain(StringResources.MissingPrefix, (string)item.Header!));

            ContextMenu forCard = view.BuildMenu(panel.Cards[0], null);
            Assert.Equal(
                ["Añadir_imagen_aquí", "Añadir_imagen_blanca_aquí", "Añadir_imagen_vacia_aquí", "Clonar", "Mover_A", "Quitar_Carta", "MenuContextualPanelImagen_CambiarCopias"],
                Keys(forCard));
            Assert.All(forCard.Items.Cast<MenuItem>(), item => Assert.DoesNotContain(StringResources.MissingPrefix, (string)item.Header!));

            ContextMenu forImage = view.BuildMenu(panel.Cards[0], FaceType.Front);
            Assert.Equal(
                [
                    "Cambiar_Cara", "imagen_blanco", "ContextMenuStrip_BotonRotar_Izquierda", "ContextMenuStrip_BotonRotar_Derecha",
                    "ContextMenuStrip_BotonRotar_Vertical", "ContextMenuStrip_BotonRotar_Horizontal",
                    "MenuContextualPanelImagen_AbrirCon", "MenuContextualPanelImagen_Explorador",
                ],
                Keys(forImage));
            Assert.All(forImage.Items.Cast<MenuItem>(), item => Assert.DoesNotContain(StringResources.MissingPrefix, (string)item.Header!));

            // The colour is offered when the project paints a background behind that face.
            panel.BackgroundColorOffered = (Front: true, Back: false);
            Assert.Contains("Color", Keys(view.BuildMenu(panel.Cards[0], FaceType.Front)));
            Assert.DoesNotContain("Color", Keys(view.BuildMenu(panel.Cards[0], FaceType.Back)));

            // A face without image has the card's menu.
            Assert.Equal(Keys(forCard), Keys(view.BuildMenu(panel.Cards[2], FaceType.Front)));
        });
    }

    [Fact]
    public void Menu_items_act_on_the_card_they_were_opened_on()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            ImagePanelViewModel panel = PanelWith(3);
            var view = new ImagePanel { DataContext = panel };
            var opened = new List<string>();
            IPlatformServices services = Platform.Services;
            Platform.Services = new RecordingServices(opened);
            try
            {
                Choose(view.BuildMenu(panel.Cards[1], FaceType.Front), "ContextMenuStrip_BotonRotar_Derecha");
                Assert.Equal(90, panel.Cards[1].Card.Front.Rotation.Angle);
                Choose(view.BuildMenu(panel.Cards[1], FaceType.Front), "ContextMenuStrip_BotonRotar_Izquierda");
                Assert.Equal(0, panel.Cards[1].Card.Front.Rotation.Angle);
                Choose(view.BuildMenu(panel.Cards[1], FaceType.Front), "ContextMenuStrip_BotonRotar_Vertical");
                Assert.True(panel.Cards[1].Card.Front.Rotation.FlipVertical);
                Choose(view.BuildMenu(panel.Cards[1], FaceType.Front), "ContextMenuStrip_BotonRotar_Horizontal");
                Assert.True(panel.Cards[1].Card.Front.Rotation.FlipHorizontal);

                string path = panel.Cards[0].Card.Front.OriginalPath;
                Choose(view.BuildMenu(panel.Cards[0], FaceType.Front), "MenuContextualPanelImagen_AbrirCon");
                Choose(view.BuildMenu(panel.Cards[0], FaceType.Front), "MenuContextualPanelImagen_Explorador");
                Assert.Equal(["open " + path, "reveal " + path], opened);

                Choose(view.BuildMenu(panel.Cards[2], null), "Clonar");
                Assert.Equal(4, panel.PanelCount);
                Choose(view.BuildMenu(panel.Cards[0], null), "Añadir_imagen_blanca_aquí");
                Assert.Equal(ImageKind.Blank, panel.Cards[0].Card.Front.ImageKind);
                Choose(view.BuildMenu(panel.Cards[1], null), "Añadir_imagen_vacia_aquí");
                Assert.Equal(ImageKind.Empty, panel.Cards[1].Card.Front.ImageKind);
                Choose(view.BuildMenu(panel.Cards[0], null), "Quitar_Carta");
                Assert.Equal(ImageKind.Empty, panel.Cards[0].Card.Front.ImageKind);
                Choose(view.BuildMenu(panel.Cards[1], FaceType.Front), "imagen_blanco");
                Assert.Equal(ImageKind.Blank, panel.Cards[1].Card.Front.ImageKind);
                Choose(view.BuildMenu(null, null), "Añadir_imagen_vacia_final");
                Assert.Equal(ImageKind.Empty, panel.Cards[^1].Card.Front.ImageKind);
            }
            finally
            {
                Platform.Services = services;
            }
        });
    }

    private sealed class RecordingServices(List<string> opened) : IPlatformServices
    {
        public void OpenUrl(string url) => opened.Add("url " + url);

        public void OpenFile(string path) => opened.Add("open " + path);

        public void OpenFolder(string path) => opened.Add("folder " + path);

        public void RevealFile(string path) => opened.Add("reveal " + path);
    }

    // ---- Windows ----

    [Fact]
    public void Move_to_window_gives_a_position_within_the_panel()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var owner = new Window();
            owner.Show();

            var window = new MoveToWindow(12, copies: false);
            Task<int?> result = window.ShowDialog<int?>(owner);
            Dispatcher.UIThread.RunJobs();
            IntegerTextBox box = window.FindControl<IntegerTextBox>("ValueBox")!;
            Assert.Equal(StringResources.Get("Form_MoverA_Boton_Mover"), window.Title);
            Assert.Equal(2, box.MaxLength);
            box.Text = "40";
            window.Accept();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(12, result.Result);

            window = new MoveToWindow(12, copies: false);
            result = window.ShowDialog<int?>(owner);
            window.Accept();   // nothing typed
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, result.Result);

            window = new MoveToWindow(7, copies: true);
            result = window.ShowDialog<int?>(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("7", window.FindControl<IntegerTextBox>("ValueBox")!.Text);
            Assert.Equal(StringResources.Get("MenuContextualPanelImagen_CambiarCopias"), window.Title);
            window.FindControl<IntegerTextBox>("ValueBox")!.Text = "25";
            Screenshots.Capture(window, "ventana-copias");
            window.Accept();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(25, result.Result);

            window = new MoveToWindow(7, copies: true);
            result = window.ShowDialog<int?>(owner);
            window.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.Null(result.Result);
            owner.Close();
        });
    }

    [Fact]
    public void Select_window_gives_the_range_and_what_to_select()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var owner = new Window();
            owner.Show();
            var window = new SelectCardsWindow(20);
            Task<CardSelection?> result = window.ShowDialog<CardSelection?>(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("1", window.FindControl<IntegerTextBox>("FromBox")!.Text);
            Assert.Equal("20", window.FindControl<IntegerTextBox>("ToBox")!.Text);
            Assert.Equal(3, window.FindControl<ComboBox>("FaceList")!.ItemCount);

            window.FindControl<IntegerTextBox>("FromBox")!.Text = "9";
            window.FindControl<IntegerTextBox>("ToBox")!.Text = "4";   // the ends may come in either order
            window.FindControl<CheckBox>("CardsCheck")!.IsChecked = true;
            window.FindControl<CheckBox>("ImagesCheck")!.IsChecked = true;
            window.FindControl<ComboBox>("FaceList")!.SelectedIndex = 2;
            Screenshots.Capture(window, "ventana-seleccionar");
            window.Accept();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(new CardSelection(3, 9, true, FaceType.Back), result.Result);

            window = new SelectCardsWindow(20);
            result = window.ShowDialog<CardSelection?>(owner);
            window.Accept();   // nothing ticked
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new CardSelection(0, 20, false, FaceType.None), result.Result);
            owner.Close();
        });
    }

    [Fact]
    public void Change_images_window_wants_an_image_and_a_valid_range()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var owner = new Window();
            owner.Show();
            string file = Picture(fixture.NewFolder(), "back.png");
            var window = new ChangeImagesWindow(10, FaceType.Back, selectedFaces: 0);
            Task<ImageChange?> result = window.ShowDialog<ImageChange?>(owner);
            Dispatcher.UIThread.RunJobs();
            RadioButton selected = window.FindControl<RadioButton>("SelectedOption")!;
            IntegerTextBox from = window.FindControl<IntegerTextBox>("FromBox")!;
            IntegerTextBox to = window.FindControl<IntegerTextBox>("ToBox")!;

            Assert.Equal(StringResources.Get("Boton_Configurar_Cara_Trasera"), window.Title);
            Assert.False(selected.IsEnabled);   // nothing is selected in the panel
            Assert.Equal(ErrorKind.MissingData, window.Validate().Kind);
            window.Accept();
            Assert.False(result.IsCompleted);
            Assert.Equal(StringResources.Get("faltan_datos"), window.FindControl<TextBlock>("ErrorText")!.Text);

            Task<bool> loading = window.LoadImageAsync(file);
            HeadlessFixture.Wait(loading);
            Assert.True(loading.Result);
            Assert.NotNull(window.FindControl<Image>("Picture")!.Source);
            Task<bool> missing = window.LoadImageAsync(file + ".gone");
            HeadlessFixture.Wait(missing);
            Assert.False(missing.Result);

            from.Text = "0";
            Assert.True(window.Validate().HasError);
            from.Text = "6";
            to.Text = "5";
            Assert.True(window.Validate().HasError);
            to.Text = "11";
            Assert.True(window.Validate().HasError);
            from.Text = "3";
            to.Text = "8";
            Assert.False(window.Validate().HasError);
            Screenshots.Capture(window, "ventana-cambiar-imagenes");
            window.Accept();
            Dispatcher.UIThread.RunJobs();

            ImageChange change = result.Result!;
            Assert.Equal(file, change.Image.Path);
            Assert.Equal((FaceType.Back, false, 2, 8), (change.Face, change.OnSelected, change.From, change.To));

            // With faces selected the window starts on them.
            window = new ChangeImagesWindow(10, FaceType.Front, selectedFaces: 2);
            result = window.ShowDialog<ImageChange?>(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.True(window.FindControl<RadioButton>("SelectedOption")!.IsChecked);
            Assert.False(window.FindControl<Grid>("RangeFields")!.IsVisible);
            HeadlessFixture.Wait(window.LoadImageAsync(file));
            window.Accept();
            Dispatcher.UIThread.RunJobs();
            Assert.True(result.Result!.OnSelected);
            Assert.Equal(FaceType.Front, result.Result.Face);
            owner.Close();
        });
    }

    // ---- The command bar ----

    [Fact]
    public void Command_bar_changes_images_and_selects_through_its_windows()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var model = new MainWindowViewModel();
            System.Windows.Input.ICommand fronts = Command("Form_Principal_Boton_CambiarImagenes_Frontales");
            System.Windows.Input.ICommand backs = Command("Form_Principal_Boton_CambiarImagenes_Traseras");
            Assert.False(fronts.CanExecute(null));   // no cards yet
            Assert.False(backs.CanExecute(null));

            string folder = fixture.NewFolder();
            HeadlessFixture.Wait(model.ImagePanel.AddImagesAsync([.. Enumerable.Range(1, 4).Select(i => Picture(folder, "c" + i + ".png"))]));
            Assert.True(fronts.CanExecute(null));
            Assert.False(backs.CanExecute(null));    // backs are not shown
            model.ImagePanel.IsDouble = true;
            Assert.True(backs.CanExecute(null));

            string file = Picture(folder, "new-back.png");
            var asked = new List<(int Cards, FaceType Face, int Selected)>();
            model.AskImageChange = (cards, face, selected) =>
            {
                asked.Add((cards, face, selected));
                return Task.FromResult<ImageChange?>(new ImageChange(ImageInspector.Current.GetImageProperties(file), face, false, 1, 3));
            };
            HeadlessFixture.Wait(model.ChangeImagesAsync(FaceType.Back));
            Assert.Equal([(4, FaceType.Back, 0)], asked);
            Assert.Equal([false, true, true, false], model.ImagePanel.Cards.Select(card => card.Card.Back.Path == file));

            model.AskImageChange = (_, _, _) => Task.FromResult<ImageChange?>(null);   // cancelled
            HeadlessFixture.Wait(model.ChangeImagesAsync(FaceType.Front));
            Assert.All(model.ImagePanel.Cards, card => Assert.NotEqual(file, card.Card.Front.Path));

            model.AskSelection = cards => Task.FromResult<CardSelection?>(new CardSelection(1, cards, true, FaceType.Front));
            HeadlessFixture.Wait(model.SelectAsync());
            Assert.Equal([false, true, true, true], model.ImagePanel.Cards.Select(card => card.IsSelected));
            Assert.Equal(3, model.ImagePanel.SelectedFaces.Count);

            model.AskSelection = _ => Task.FromResult<CardSelection?>(new CardSelection(0, 4, false, FaceType.None));   // nothing ticked
            HeadlessFixture.Wait(model.SelectAsync());
            Assert.Equal(3, model.ImagePanel.SelectedPanelCount);

            System.Windows.Input.ICommand Command(string key) =>
                model.Tabs.SelectMany(tab => tab.Groups).SelectMany(group => group.Buttons).First(button => button.Key == key).Command;
        });
    }
}
