using Avalonia.Controls;
using Avalonia.Threading;
using ImageMagick;
using PnpTool.App.ViewModels;
using PnpTool.App.Views;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.CustomDecks;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Imaging;
using PointD = PnpTool.Core.Primitives.PointD;

namespace PnpTool.App.Tests;

[Collection(UserInterface.Name)]
public sealed class DeckCreatorTests(HeadlessFixture fixture)
{
    private static string Picture(string folder, string name, string color = "#3060C0", uint width = 300, uint height = 420)
    {
        string path = Path.Combine(folder, name);
        using var image = new MagickImage(new MagickColor(color), width, height);
        image.Density = new Density(300);
        image.Write(path);
        return path;
    }

    /// <summary>A template for sports cards: a frame with a see-through window in the middle.</summary>
    private static string Template(string folder)
    {
        string path = Path.Combine(folder, "template.png");
        using var image = new MagickImage(new MagickColor("#202020"), 744, 1039);
        image.Density = new Density(300);
        image.Alpha(AlphaAction.Set);
        using var window = new MagickImage(MagickColors.Transparent, 544, 600);
        image.Composite(window, 100, 100, CompositeOperator.Copy);
        image.Write(path);
        return path;
    }

    /// <summary>The first cards of a deck shipped with the program, so that the tests are quick.</summary>
    private static CustomDeck FirstCards(string deckName, int count)
    {
        var deck = new CustomDeck();
        Assert.False(ProjectFileStore.LoadCustomDeck(Path.Combine(AppPaths.CustomDecksFolder, deckName + ".pnpm"), ref deck).HasError);
        deck.Cards = [.. deck.Cards.Take(count)];
        return deck;
    }

    private static double Difference(string first, string second)
    {
        using var a = new MagickImage(first);
        using var b = new MagickImage(second);
        return a.Compare(b, ErrorMetric.MeanAbsolute);
    }

    // ---- From a deck to a project ----

    [Fact]
    public void A_deck_of_the_program_becomes_a_double_sided_project_with_its_back()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            CustomDeck deck = FirstCards("Baraja Poker", 3);
            deck.BackImagePath = Picture(folder, "back.png", "#802020");
            deck.Cards[1].BackgroundImagePath = Picture(folder, "fondo.png", "#20A040");
            var shares = new List<double>();
            string output = Path.Combine(folder, "salida");

            (AppError error, Project project) = CustomDeckProjects.Build(deck, output, shares.Add);

            Assert.False(error.HasError, error.Message);
            Assert.Equal(3, project.Deck.Cards.Length);
            Assert.All(project.Deck.Cards, card => Assert.True(File.Exists(card.Front.Path), card.Front.Path));
            Assert.Equal(Path.Combine(output, deck.Cards[0].NameSpanish + ".png"), project.Deck.Cards[0].Front.Path);
            Assert.True(project.Settings.PrintLayout.DoubleSided);
            Assert.False(project.Settings.DoublePanel);
            Assert.Equal(deck.BackImagePath, project.Settings.SharedBack.Path);
            Assert.True(project.Settings.Grid.CardSize.X.Millimeter > 50.0);    // from the style of the deck
            Assert.Equal(3, shares.Count);
            using var plain = new MagickImage(project.Deck.Cards[0].Front.Path);
            using var onGreen = new MagickImage(project.Deck.Cards[1].Front.Path);
            Assert.Equal(plain.Width, onGreen.Width);
        });
    }

    [Fact]
    public void A_sports_deck_writes_on_every_card_the_name_of_its_own_player()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            string photo = Picture(folder, "foto.png", "#C0A020", 544, 600);
            var deck = new SportsDeck { TemplateImagePath = Template(folder), BackImagePath = Picture(folder, "back.png") };
            deck.TeamText.Text = "Equipo";
            deck.TeamText.Position = new PointD { X = 0.1, Y = 0.8 };
            deck.TeamText.Font.Color = "255_255_255_255";
            deck.PlayerText.Position = new PointD { X = 0.1, Y = 0.02 };
            deck.PlayerText.Font.Color = "255_255_255_255";
            foreach (string name in new[] { "Ana", "Wilhelmina", "Ana bis" })
            {
                deck.Add(new CustomCard { NameSpanish = name, BackgroundImagePath = photo });
            }

            (AppError error, Project project) = CustomDeckProjects.BuildSports(deck, Path.Combine(folder, "con"));
            Assert.False(error.HasError, error.Message);
            string[] cards = [.. project.Deck.Cards.Select(card => card.Front.Path)];
            Assert.Equal(3, cards.Length);
            Assert.True(Difference(cards[0], cards[1]) > 0.0);      // same picture, another name

            // The picture of the player shows through the window of the template.
            using (var first = new MagickImage(cards[0]))
            {
                IMagickColor<ushort> inWindow = first.GetPixels().GetPixel(372, 400).ToColor()!;
                Assert.True(inWindow.R > inWindow.B);
            }

            // Without the name of the team the foot of the card is plain.
            deck.TeamTextVisible = false;
            (_, Project without) = CustomDeckProjects.BuildSports(deck, Path.Combine(folder, "sin"));
            Assert.True(Difference(cards[0], without.Deck.Cards[0].Front.Path) > 0.0);

            // 63 by 88 mm faces with a mirrored border, on cells of 65 by 90, two millimetres apart.
            ProjectSettings settings = project.Settings;
            Assert.Equal(63.0, settings.Front.Size.X.Millimeter, 2);
            Assert.Equal(ImageEffectType.Mirror, settings.Front.Effect);
            Assert.Equal(90.0, settings.Grid.CardSize.Y.Millimeter, 2);
            Assert.Equal(2.0, settings.Grid.Spacing.X.Millimeter, 2);
            Assert.Equal(1.0, settings.Grid.Bleed.Millimeter, 2);
            Assert.Equal(CutLineType.Normal, settings.Front.CutLine.LineType);
            Assert.True(settings.PrintLayout.DoubleSided);
            AppError check = SettingsValidator.Check(settings, 3);
            Assert.False(check.HasError, check.Message);
        });
    }

    // ---- The creator of custom decks ----

    [Fact]
    public void Creator_lists_the_decks_and_gives_backgrounds_and_a_back()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            var creator = new CustomDeckViewModel();
            var notices = new List<string>();
            creator.Notice += notices.Add;
            Assert.Equal(["Baraja Poker", "Baraja_Espanola", "Uno"], creator.DeckNames);
            Assert.False(creator.HasDeck);
            Assert.Equal(StringResources.Get("faltan_datos"), creator.Problem());

            creator.SelectedDeckName = "Baraja_Espanola";

            Assert.True(creator.HasDeck);
            Assert.Equal(41, creator.Items.Count);                      // forty cards and the back
            Assert.Equal("As de Oros", creator.Items[0]);
            Assert.Equal(0, creator.SelectedIndex);
            Assert.True(File.Exists(creator.PicturePath), creator.PicturePath);
            Assert.Equal(string.Empty, creator.BackgroundPath);
            Assert.Equal(StringResources.Get("falta_imagen_trasera"), creator.Problem());

            string background = Picture(folder, "fondo.png");
            creator.SetImage(background);
            Assert.Equal(background, creator.BackgroundPath);
            Assert.Equal(background, creator.Deck.Cards[0].BackgroundImagePath);

            creator.SelectedIndex = 40;                                 // the back
            Assert.True(creator.BackSelected);
            Assert.Equal(string.Empty, creator.PicturePath);
            string back = Picture(folder, "back.png");
            creator.SetImage(back);
            Assert.Equal(back, creator.PicturePath);
            Assert.Equal(back, creator.Deck.BackImagePath);
            Assert.Null(creator.Problem());

            // Saved and opened again, it keeps what was chosen.
            string saved = Path.Combine(folder, "mi baraja.pnpm");
            creator.Save(saved);
            var again = new CustomDeckViewModel();
            again.Open(saved);
            Assert.Equal(41, again.Items.Count);
            Assert.Equal(back, again.Deck.BackImagePath);
            Assert.Equal(background, again.Deck.Cards[0].BackgroundImagePath);
            Assert.Null(again.SelectedDeckName);

            string broken = Path.Combine(folder, "rota.pnpm");
            File.WriteAllText(broken, "not a deck");
            again.Open(broken);
            Assert.Empty(notices);
            Assert.Equal(41, again.Items.Count);                        // what was open stays
        });
    }

    [Fact]
    public void Card_names_follow_the_language_of_the_program()
    {
        fixture.Run(LanguageId.EnglishUk, () =>
        {
            var creator = new CustomDeckViewModel { SelectedDeckName = "Baraja_Espanola" };
            Assert.Equal("Ace of Gold", creator.Items[0]);
        });
    }

    // ---- The creator of sports decks ----

    [Fact]
    public void Sports_creator_keeps_players_texts_and_fonts_and_saves_them()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            var creator = new SportsDeckViewModel();
            Assert.Equal(SportsDeckViewModel.SamplePlayer, creator.PlayerName);
            Assert.Equal(StringResources.Get("falta_imagen_trasera"), creator.Problem());
            creator.BackPath = Picture(folder, "back.png");
            Assert.Equal(StringResources.Get("faltan_datos"), creator.Problem());
            creator.TemplatePath = Template(folder);
            Assert.EndsWith("=0", creator.Problem());

            creator.AddPlayer("Ana");
            creator.AddPlayer(" ");                                     // no name, no player
            creator.AddPlayer("Berta");
            Assert.Equal(["Ana", "Berta"], creator.Players);
            Assert.Equal(1, creator.SelectedIndex);
            Assert.Equal("Berta", creator.PlayerName);
            Assert.Null(creator.Problem());
            string photo = Picture(folder, "berta.png");
            creator.SetPlayerPicture(photo);
            Assert.Equal(photo, creator.PlayerPicturePath);

            creator.TeamName = "Las Leonas";
            creator.TeamVisible = false;
            creator.PlaceText(team: true, 0.25, 1.4);                   // kept on the card
            creator.PlaceText(team: false, 0.5, 0.1);
            int textChanges = 0;
            creator.TextsChanged += (_, _) => textChanges++;
            creator.SetFont(team: false, new FontSpec { FontFamily = "Liberation Serif", FontSize = 20.0 });
            Assert.Equal(1, textChanges);

            string saved = Path.Combine(folder, "equipo.pnpm");
            creator.Save(saved);
            var again = new SportsDeckViewModel();
            again.Open(saved);
            Assert.Equal(["Ana", "Berta"], again.Players);
            Assert.Equal("Las Leonas", again.TeamName);
            Assert.False(again.TeamVisible);
            Assert.Equal(creator.TemplatePath, again.TemplatePath);
            Assert.Equal(creator.BackPath, again.BackPath);
            Assert.Equal(0.25, again.Deck.TeamText.Position.X, 3);
            Assert.Equal(1.0, again.Deck.TeamText.Position.Y, 3);
            Assert.Equal(0.5, again.Deck.PlayerText.Position.X, 3);
            Assert.Equal(20.0, again.Deck.PlayerText.Font.FontSize, 3);
            Assert.Equal(photo, again.Deck.Cards[1].BackgroundImagePath);
            Assert.Equal(0, again.SelectedIndex);

            again.RemoveSelected();
            Assert.Equal(["Berta"], again.Players);
            Assert.Equal("Berta", again.PlayerName);
            Assert.Single(again.Deck.Cards);
            again.RemoveSelected();
            Assert.Equal(-1, again.SelectedIndex);
            Assert.Equal(SportsDeckViewModel.SamplePlayer, again.PlayerName);
        });
    }

    // ---- From the main window ----

    [Fact]
    public void The_creator_makes_the_pdf_and_leaves_the_project_beside_the_images()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            CustomDeck deck = FirstCards("Uno", 4);
            deck.BackImagePath = Picture(folder, "back.png");
            string small = Path.Combine(folder, "cuatro.pnpm");
            ProjectFileStore.SaveCustomDeck(small, deck);
            string output = Path.Combine(folder, "mazo");
            string pdf = Path.Combine(folder, "mazo.pdf");
            var said = new List<string>();
            var names = new List<string>();
            int shown = 0;
            var model = new MainWindowViewModel
            {
                ShowMessage = message =>
                {
                    said.Add(message);
                    return Task.CompletedTask;
                },
                AskPdfToSave = name =>
                {
                    names.Add(name);
                    return Task.FromResult<string?>(pdf);
                },
            };
            model.ShowCustomDeckCreator = creator =>
            {
                shown++;
                if (shown == 1)
                {
                    creator.Open(small);
                    return Task.FromResult<string?>(output);
                }

                Assert.Equal(5, creator.Items.Count);                   // the window comes back as it was
                return Task.FromResult<string?>(null);
            };
            IPlatformServices services = Platform.Services;
            Platform.Services = new QuietServices();
            try
            {
                HeadlessFixture.Wait(model.CreateCustomDeckAsync());
            }
            finally
            {
                Platform.Services = services;
            }

            Assert.Equal(2, shown);
            Assert.Empty(said);
            Assert.Equal(["mazo.pdf"], names);
            Assert.True(File.Exists(pdf));
            Assert.Equal(4, Directory.GetFiles(output, "*.png").Length);
            string projectFile = Path.Combine(output, CustomDeckProjects.ProjectFileName);
            (AppError error, Project project) = ProjectFileStore.LoadProject(projectFile);
            Assert.False(error.HasError, error.Message);
            Assert.Equal(4, project.Deck.Cards.Length);
            Assert.True(project.Settings.PrintLayout.DoubleSided);
            using var document = new iText.Kernel.Pdf.PdfDocument(new iText.Kernel.Pdf.PdfReader(pdf));
            Assert.Equal(2, document.GetNumberOfPages());              // fronts and the shared back
        });
    }

    private sealed class QuietServices : IPlatformServices
    {
        public void OpenUrl(string url)
        {
        }

        public void OpenFile(string path)
        {
        }

        public void OpenFolder(string path)
        {
        }

        public void RevealFile(string path)
        {
        }
    }

    // ---- Windows ----

    [Fact]
    public void Creator_window_shows_the_card_and_closes_with_the_folder()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            var owner = new Window();
            owner.Show();
            var creator = new CustomDeckViewModel { SelectedDeckName = "Baraja Poker" };
            creator.SelectedIndex = 12;
            creator.SetImage(Picture(folder, "fondo.png", "#E8D8A0"));
            var window = new CustomDeckWindow(creator);
            Task<string?> result = window.ShowDialog<string?>(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(window.FindControl<Image>("PictureImage")!.Source);
            Assert.NotNull(window.FindControl<Image>("BackgroundImage")!.Source);
            Assert.Equal(54, window.FindControl<ListBox>("CardsBox")!.ItemCount);
            Screenshots.Capture(window, "ventana-mazo-personalizado");

            creator.SelectedIndex = 53;
            creator.SetImage(Picture(folder, "back.png", "#802020"));
            window.AskFolder = () => Task.FromResult<string?>(folder);
            HeadlessFixture.Wait(window.CreateAsync());
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(folder, result.Result);
            owner.Close();
        });
    }

    [Fact]
    public void Sports_window_places_the_texts_and_adds_players()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            var owner = new Window();
            owner.Show();
            var creator = new SportsDeckViewModel { TeamName = "Las Leonas" };
            creator.TemplatePath = Template(folder);
            creator.BackPath = Picture(folder, "back.png");
            creator.SetFont(team: true, new FontSpec { FontSize = 22.0, FontWeight = 700, Color = "255_255_255_255" });
            creator.SetFont(team: false, new FontSpec { FontSize = 16.0, Color = "255_255_255_255" });
            creator.PlaceText(team: true, 0.2, 0.75);
            creator.PlaceText(team: false, 0.15, 0.03);
            var window = new SportsDeckWindow(creator);
            Task<string?> result = window.ShowDialog<string?>(owner);
            Dispatcher.UIThread.RunJobs();

            var names = new Queue<string?>(["Ana", null]);
            window.AskName = () => Task.FromResult(names.Dequeue());
            HeadlessFixture.Wait(window.AddPlayerAsync());
            HeadlessFixture.Wait(window.AddPlayerAsync());               // cancelled
            creator.SetPlayerPicture(Picture(folder, "ana.png", "#C0A020", 544, 600));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(["Ana"], creator.Players);
            TextBlock team = window.FindControl<TextBlock>("TeamText")!;
            TextBlock player = window.FindControl<TextBlock>("PlayerText")!;
            Assert.Equal("Ana", player.Text);
            Assert.Equal("Las Leonas", team.Text);
            Assert.Equal(0.2 * 240.0, Canvas.GetLeft(team), 3);
            Assert.Equal(0.75 * 336.0, Canvas.GetTop(team), 3);
            Assert.Equal(22.0, team.FontSize);
            Assert.NotNull(window.FindControl<Image>("TemplateImage")!.Source);
            Assert.NotNull(window.FindControl<Image>("PlayerImage")!.Source);
            Screenshots.Capture(window, "ventana-mazo-deportivo");

            creator.TeamVisible = false;
            Dispatcher.UIThread.RunJobs();
            Assert.False(team.IsVisible);

            window.AskFolder = () => Task.FromResult<string?>(folder);
            HeadlessFixture.Wait(window.CreateAsync());
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(folder, result.Result);

            var input = new TextInputWindow("Nombre");
            Task<string?> typed = input.ShowDialog<string?>(owner);
            input.Text = "Berta";
            input.Accept();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Berta", typed.Result);
            owner.Close();
        });
    }
}
