using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ImageMagick;
using iText.Kernel.Pdf;
using PnpTool.App.ViewModels;
using PnpTool.App.Views;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Pdf.Layout;

namespace PnpTool.App.Tests;

[Collection(UserInterface.Name)]
public sealed class ProjectFilesTests(HeadlessFixture fixture)
{
    private static string Picture(string folder, string name)
    {
        string path = Path.Combine(folder, name);
        using var image = new MagickImage(new MagickColor("#3060C0"), 150, 210);
        image.Density = new Density(300);
        image.Write(path);
        return path;
    }

    /// <summary>What the model asks the user and tells them, answered from the test.</summary>
    private sealed class Answers
    {
        public List<string> Said { get; } = [];

        public List<string> Asked { get; } = [];

        public bool? Answer { get; set; } = false;

        public string? FileToOpen { get; set; }

        public string? ProjectToSave { get; set; }

        public string? PdfToSave { get; set; }

        public List<string> PdfNames { get; } = [];
    }

    /// <summary>A model in a clean state, as after starting the program, with some cards.</summary>
    private (MainWindowViewModel Model, Answers Answers, string Folder) Start(int cards = 0)
    {
        File.Delete(MainWindowViewModel.TemporaryProjectFile);
        File.Delete(AppPaths.RecentFilesFile);
        AppSettings.OpenProjectFile = string.Empty;
        var answers = new Answers();
        MainWindowViewModel model = NewModel(answers);
        HeadlessFixture.Wait(model.StartAsync());
        string folder = fixture.NewFolder();
        if (cards != 0)
        {
            HeadlessFixture.Wait(model.ImagePanel.AddImagesAsync([.. Enumerable.Range(1, cards).Select(i => Picture(folder, "c" + i + ".png"))]));
        }

        return (model, answers, folder);
    }

    private static MainWindowViewModel NewModel(Answers answers) => new()
    {
        ShowMessage = message =>
        {
            answers.Said.Add(message);
            return Task.CompletedTask;
        },
        Ask = (message, _) =>
        {
            answers.Asked.Add(message);
            return Task.FromResult(answers.Answer);
        },
        AskFileToOpen = () => Task.FromResult(answers.FileToOpen),
        AskProjectToSave = () => Task.FromResult(answers.ProjectToSave),
        AskPdfToSave = name =>
        {
            answers.PdfNames.Add(name);
            return Task.FromResult(answers.PdfToSave);
        },
    };

    private static string[] Fronts(MainWindowViewModel model) => [.. model.ImagePanel.Cards.Select(card => Path.GetFileNameWithoutExtension(card.Card.Front.Path))];

    private sealed class RecordingServices(List<string> opened) : IPlatformServices
    {
        public void OpenUrl(string url) => opened.Add("url " + url);

        public void OpenFile(string path) => opened.Add("open " + path);

        public void OpenFolder(string path) => opened.Add("folder " + path);

        public void RevealFile(string path) => opened.Add("reveal " + path);
    }

    private static int Pages(string file)
    {
        using var document = new PdfDocument(new PdfReader(file));
        return document.GetNumberOfPages();
    }

    // ---- Starting ----

    [Fact]
    public void Ticket_projects_are_refused_as_the_original_refuses_them()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            (MainWindowViewModel model, Answers answers, string folder) = Start(cards: 2);
            string ticket = Path.Combine(folder, "rifa.pnpt");
            File.Copy(Path.Combine(AppPaths.TicketsFolder, "TicketDefecto.pnpt"), ticket);

            Task<bool> opening = model.OpenAsync(ticket);
            HeadlessFixture.Wait(opening);
            Assert.False(opening.Result);

            Assert.Equal(["Funcionalidad Obsoleta"], answers.Said);
            Assert.Equal(2, model.ImagePanel.Cards.Count);
        });
    }

    [Fact]
    public void The_program_starts_on_a_custom_project_with_the_standard_card()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            (MainWindowViewModel model, Answers answers, _) = Start();

            Assert.Equal(SettingsGroup.Custom, model.ProjectSettings.SelectedGroup!.Group);
            Assert.Equal(63.5, model.ProjectSettings.Settings.Grid.CardSize.X.Millimeter, 2);
            Assert.False(model.HasChanged);
            Assert.Equal(string.Empty, model.OpenFile);
            Assert.Equal(AppInfo.Name + " " + AppInfo.Version + " - Unsaved", model.WindowTitle);
            Assert.Empty(answers.Asked);
            Assert.Empty(model.RecentFiles);
        });
    }

    // ---- Saving and opening ----

    [Fact]
    public void A_project_is_saved_and_opened_again_as_it_was()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            (MainWindowViewModel model, Answers answers, string folder) = Start(cards: 3);
            model.ProjectSettings.SelectedGroup = model.ProjectSettings.Groups.First(group => group.Group == SettingsGroup.Cards);
            model.ProjectSettings.SelectedStyle = model.ProjectSettings.Styles.First(style => style.FileName == "Cartas_Euro.pnps");
            model.ImagePanel.Cards[1].Copies = 4;
            model.ImagePanel.RotateFace(2, FaceType.Front, new RotationProperties(90, false, false));
            Assert.True(model.HasChanged);
            Assert.EndsWith(" - Unsaved *", model.WindowTitle);
            string file = Path.Combine(folder, "game.pnp");

            // Cancelling the dialog saves nothing.
            Assert.False(HeadlessFixture.WaitFor(model.SaveAsync(askPath: false)));
            Assert.False(File.Exists(file));

            answers.ProjectToSave = file;
            Assert.True(HeadlessFixture.WaitFor(model.SaveAsync(askPath: false)));
            Assert.True(File.Exists(file));
            Assert.False(model.HasChanged);
            Assert.Equal(file, model.OpenFile);
            Assert.EndsWith(" - " + file, model.WindowTitle);
            Assert.Equal([file], model.RecentFiles.Select(recent => recent.Path));
            Assert.False(File.Exists(MainWindowViewModel.TemporaryProjectFile));

            // Saving again does not ask.
            answers.ProjectToSave = null;
            model.ImagePanel.AddBlankCard();
            Assert.True(HeadlessFixture.WaitFor(model.SaveAsync(askPath: false)));

            Assert.True(HeadlessFixture.WaitFor(model.NewAsync()));
            Assert.Equal(0, model.ImagePanel.PanelCount);
            Assert.Equal(string.Empty, model.OpenFile);

            Assert.True(HeadlessFixture.WaitFor(model.OpenAsync(file)));
            Assert.Equal(["c1", "c2", "c3", "255_255_255_255"], Fronts(model));
            Assert.Equal([1, 4, 1, 1], model.ImagePanel.Cards.Select(card => card.Copies));
            Assert.Equal(90, model.ImagePanel.Cards[2].Card.Front.Rotation.Angle);
            Assert.Equal("Cartas_Euro.pnps", model.ProjectSettings.SelectedStyle!.FileName);
            Assert.Equal(59.0, model.ProjectSettings.Settings.Grid.CardSize.X.Millimeter, 2);
            Assert.False(model.HasChanged);
            Assert.Equal(file, model.OpenFile);
            Assert.False(model.ImagePanel.History.CanUndo);
            Assert.Empty(answers.Said);
        });
    }

    [Fact]
    public void Open_asks_for_the_file_and_takes_styles_too()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            (MainWindowViewModel model, Answers answers, string folder) = Start(cards: 2);
            answers.Answer = false;    // do not save the changes

            Assert.False(HeadlessFixture.WaitFor(model.OpenAsync()));    // dialog cancelled
            Assert.Equal(2, model.ImagePanel.PanelCount);

            answers.FileToOpen = StyleCatalog.PathOf(SettingsGroup.Cards, "Cartas_Gutterfold.pnps");
            Assert.True(HeadlessFixture.WaitFor(model.OpenAsync()));
            Assert.Equal(0, model.ImagePanel.PanelCount);    // a style starts a new project
            Assert.True(model.ProjectSettings.Settings.DoublePanel);
            Assert.True(model.ImagePanel.IsDouble);
            Assert.Single(answers.Asked);

            string text = Path.Combine(folder, "notes.txt");
            File.WriteAllText(text, "x");
            Assert.False(HeadlessFixture.WaitFor(model.OpenAsync(text)));
            Assert.Single(answers.Said);

            string broken = Path.Combine(folder, "broken.pnp");
            File.WriteAllText(broken, "not a project");
            Assert.False(HeadlessFixture.WaitFor(model.OpenAsync(broken)));
            Assert.Equal(2, answers.Said.Count);
        });
    }

    [Fact]
    public void A_project_with_missing_images_opens_and_says_which()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            (MainWindowViewModel model, Answers answers, string folder) = Start(cards: 3);
            string file = Path.Combine(folder, "game.pnp");
            answers.ProjectToSave = file;
            HeadlessFixture.Wait(model.SaveAsync(askPath: true));
            File.Delete(Path.Combine(folder, "c2.png"));

            Assert.True(HeadlessFixture.WaitFor(model.OpenAsync(file)));

            Assert.Equal(3, model.ImagePanel.PanelCount);
            string said = Assert.Single(answers.Said);
            Assert.Contains("#2 ", said);
            Assert.Contains("c2.png", said);
            Assert.DoesNotContain("c1.png", said);
        });
    }

    [Fact]
    public void Recent_files_open_when_picked()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            (MainWindowViewModel model, Answers answers, string folder) = Start(cards: 2);
            string first = Path.Combine(folder, "first.pnp");
            string second = Path.Combine(folder, "second.pnp");
            answers.ProjectToSave = first;
            HeadlessFixture.Wait(model.SaveAsync(askPath: true));
            model.ImagePanel.AddBlankCard();
            answers.ProjectToSave = second;
            HeadlessFixture.Wait(model.SaveAsync(askPath: true));
            Assert.Equal([second, first], model.RecentFiles.Select(recent => recent.Path));

            model.SelectedRecentFile = model.RecentFiles[1];
            Dispatcher.UIThread.RunJobs();
            HeadlessFixture.Wait(Task.Delay(200));

            Assert.Equal(first, model.OpenFile);
            Assert.Equal(2, model.ImagePanel.PanelCount);
            Assert.Null(model.SelectedRecentFile);

            // Another run of the program finds the list.
            var later = NewModel(new Answers());
            HeadlessFixture.Wait(later.StartAsync());
            Assert.Equal(2, later.RecentFiles.Count);
            Assert.Equal(41, new RecentFileItem(new string('a', 50)).ToString().Length);    // long paths are shortened in the list
        });
    }

    // ---- Unsaved changes ----

    [Fact]
    public void Unsaved_changes_are_asked_about_before_they_are_lost()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            (MainWindowViewModel model, Answers answers, string folder) = Start(cards: 2);

            answers.Answer = null;    // cancel
            Assert.False(HeadlessFixture.WaitFor(model.NewAsync()));
            Assert.Equal(2, model.ImagePanel.PanelCount);
            Assert.Equal(StringResources.Get("proyecto_ha_cambiado"), Assert.Single(answers.Asked));
            Assert.False(HeadlessFixture.WaitFor(model.CloseAsync()));

            answers.Answer = true;    // save, but the save dialog is cancelled: nothing is lost
            Assert.False(HeadlessFixture.WaitFor(model.NewAsync()));
            Assert.Equal(2, model.ImagePanel.PanelCount);

            string file = Path.Combine(folder, "kept.pnp");
            answers.ProjectToSave = file;
            Assert.True(HeadlessFixture.WaitFor(model.NewAsync()));
            Assert.True(File.Exists(file));
            Assert.Equal(0, model.ImagePanel.PanelCount);
            Assert.False(model.HasChanged);

            model.ImagePanel.AddBlankCard();
            answers.Answer = false;    // discard
            int asked = answers.Asked.Count;
            Assert.True(HeadlessFixture.WaitFor(model.CloseAsync()));
            Assert.Equal(asked + 1, answers.Asked.Count);
            Assert.False(File.Exists(MainWindowViewModel.TemporaryProjectFile));
        });
    }

    [Fact]
    public void A_copy_of_unsaved_work_is_offered_after_an_unexpected_end()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            (MainWindowViewModel model, _, _) = Start(cards: 3);
            model.ImagePanel.Cards[0].Copies = 6;
            Assert.True(File.Exists(MainWindowViewModel.TemporaryProjectFile));

            // The program ends without closing; the next run offers the copy.
            var answers = new Answers { Answer = true };
            MainWindowViewModel next = NewModel(answers);
            AppSettings.OpenProjectFile = string.Empty;
            HeadlessFixture.Wait(next.StartAsync());

            Assert.Equal(StringResources.Get("Proyecto_Temporal"), Assert.Single(answers.Asked));
            Assert.Equal(["c1", "c2", "c3"], Fronts(next));
            Assert.Equal(6, next.ImagePanel.Cards[0].Copies);
            Assert.True(next.HasChanged);
            Assert.Equal(string.Empty, next.OpenFile);
            Assert.Empty(next.RecentFiles);

            // Declined, the copy is deleted and the program starts clean.
            answers = new Answers { Answer = false };
            MainWindowViewModel declined = NewModel(answers);
            HeadlessFixture.Wait(declined.StartAsync());
            Assert.Single(answers.Asked);
            Assert.Equal(0, declined.ImagePanel.PanelCount);
            Assert.False(File.Exists(MainWindowViewModel.TemporaryProjectFile));
        });
    }

    // ---- Making the PDF ----

    /// <summary>How many images are drawn on a page.</summary>
    private static int ImagesDrawn(string file, int page)
    {
        using var document = new PdfDocument(new PdfReader(file));
        var counter = new ImageCounter();
        new iText.Kernel.Pdf.Canvas.Parser.PdfCanvasProcessor(counter).ProcessPageContent(document.GetPage(page));
        return counter.Count;
    }

    private sealed class ImageCounter : iText.Kernel.Pdf.Canvas.Parser.Listener.IEventListener
    {
        public int Count { get; private set; }

        public void EventOccurred(iText.Kernel.Pdf.Canvas.Parser.Data.IEventData data, iText.Kernel.Pdf.Canvas.Parser.EventType type) => Count++;

        public ICollection<iText.Kernel.Pdf.Canvas.Parser.EventType> GetSupportedEvents() => [iText.Kernel.Pdf.Canvas.Parser.EventType.RENDER_IMAGE];
    }

    [Fact]
    public void Stacked_layout_spreads_the_cards_over_the_sheets_of_the_block()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            (MainWindowViewModel model, Answers answers, string folder) = Start();
            PrintLayout layout = model.ProjectSettings.Settings.PrintLayout;
            int perSheet = layout.Rows * layout.Columns;
            Assert.True(perSheet >= 4, "The test needs several cards to a sheet.");
            HeadlessFixture.Wait(model.ImagePanel.AddImagesAsync([.. Enumerable.Range(1, perSheet + 2).Select(i => Picture(folder, "c" + i + ".png"))]));

            // As they come: a full sheet and two cards on the next.
            answers.PdfToSave = Path.Combine(folder, "standard.pdf");
            Assert.NotNull(HeadlessFixture.WaitFor(model.CreatePdfAsync(preview: false)));
            Assert.Equal(2, Pages(answers.PdfToSave));
            Assert.Equal(perSheet, ImagesDrawn(answers.PdfToSave, 1));
            Assert.Equal(2, ImagesDrawn(answers.PdfToSave, 2));

            // In piles of two sheets: a card on one sheet, the next on the other.
            model.ProjectSettings.Settings.Other.StackedLayout.CopyFrom(new StackedLayout(2, LayoutType.Stacked));
            answers.PdfToSave = Path.Combine(folder, "stacked.pdf");
            Assert.NotNull(HeadlessFixture.WaitFor(model.CreatePdfAsync(preview: false)));
            Assert.Equal(2, Pages(answers.PdfToSave));
            Assert.Equal((perSheet + 3) / 2, ImagesDrawn(answers.PdfToSave, 1));
            Assert.Equal((perSheet + 2) / 2, ImagesDrawn(answers.PdfToSave, 2));

            // The cards of the panel are as they were.
            Assert.Equal(perSheet + 2, model.ImagePanel.Cards.Count);
            Assert.Empty(answers.Said);
        });
    }

    [Fact]
    public void Preview_makes_the_first_sheet_and_create_the_whole_pdf_and_both_open_it()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            (MainWindowViewModel model, Answers answers, string folder) = Start(cards: 12);
            var opened = new List<string>();
            IPlatformServices services = Platform.Services;
            Platform.Services = new RecordingServices(opened);
            try
            {
                PdfOutput? preview = HeadlessFixture.WaitFor(model.CreatePdfAsync(preview: true));
                Assert.NotNull(preview);
                Assert.True(preview.SingleFile);
                Assert.Equal(1, Pages(preview.Files[0]));
                Assert.Equal(["open " + preview.Files[0]], opened);
                Assert.Empty(answers.PdfNames);    // a preview does not ask where to save

                // Cancelling the save dialog makes nothing.
                Assert.Null(HeadlessFixture.WaitFor(model.CreatePdfAsync(preview: false)));
                Assert.Single(answers.PdfNames);

                string pdf = Path.Combine(folder, "out.pdf");
                answers.PdfToSave = pdf;
                PdfOutput? created = HeadlessFixture.WaitFor(model.CreatePdfAsync(preview: false));
                Assert.NotNull(created);
                Assert.Equal(pdf, created.Files[0]);
                Assert.Equal(2, Pages(pdf));    // twelve cards, nine to a sheet
                Assert.Equal("open " + pdf, opened[^1]);
                Assert.Equal(0, model.Progress);
                Assert.Empty(answers.Said);

                // A saved project gives its name to the PDF.
                answers.ProjectToSave = Path.Combine(folder, "my game.pnp");
                HeadlessFixture.Wait(model.SaveAsync(askPath: true));
                HeadlessFixture.Wait(model.CreatePdfAsync(preview: false));
                Assert.Equal("my game.pdf", answers.PdfNames[^1]);
            }
            finally
            {
                Platform.Services = services;
            }
        });
    }

    [Fact]
    public void A_template_made_in_the_editor_lays_out_the_pdf()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            (MainWindowViewModel model, Answers answers, string folder) = Start(cards: 5);
            string? fileName = null;
            try
            {
                var editor = new TemplateEditorViewModel { Name = "Dos en A5" };
                editor.Paper.Selected = editor.Paper.Options.First(option => option.Value == PnpTool.Core.Paper.PaperType.A5);
                editor.Paper.Landscape = true;
                foreach (string x in new[] { "10", "110" })
                {
                    editor.LocationXText = x;
                    editor.LocationYText = "10";
                    editor.SizeXText = "60";
                    editor.SizeYText = "90";
                    editor.Add();
                }

                editor.Save();
                fileName = editor.SelectedTemplate.FileName;
                ProjectSettingsViewModel settings = model.ProjectSettings;
                settings.SelectedGroup = settings.Groups.First(option => option.Group == SettingsGroup.AdvancedStyles);
                settings.SelectedStyle = settings.Styles.First(option => option.FileName == fileName);
                Assert.Equal(2, settings.Settings.PrintLayout.Columns);
                Assert.Equal(ProjectType.AdvancedStyles, settings.Settings.Group.ProjectType);  // Windows left it a "card" project
                answers.PdfToSave = Path.Combine(folder, "plantilla.pdf");

                PdfOutput? created = HeadlessFixture.WaitFor(model.CreatePdfAsync(preview: false));

                Assert.NotNull(created);
                Assert.Empty(answers.Said);
                using var document = new PdfDocument(new PdfReader(answers.PdfToSave));
                Assert.Equal(3, document.GetNumberOfPages());      // five cards, two to a sheet
                iText.Kernel.Geom.Rectangle page = document.GetPage(1).GetPageSize();
                Assert.Equal(210.0, page.GetWidth() * 25.4 / 72.0, 1);  // an A5 on its side
                Assert.Equal(148.0, page.GetHeight() * 25.4 / 72.0, 0);

                // The first card is drawn in its place, 10 mm from the left and top edges, and not beyond it.
                using SkiaSharp.SKBitmap sheet = PDFtoImage.Conversion.ToImage(File.ReadAllBytes(answers.PdfToSave), 0, options: new PDFtoImage.RenderOptions(Dpi: 72));
                static int Dots(double millimetres) => (int)(millimetres * 72.0 / 25.4);
                Assert.NotEqual(SkiaSharp.SKColors.White, sheet.GetPixel(Dots(40.0), Dots(55.0)));
                Assert.Equal(SkiaSharp.SKColors.White, sheet.GetPixel(Dots(90.0), Dots(55.0)));
                Assert.NotEqual(SkiaSharp.SKColors.White, sheet.GetPixel(Dots(140.0), Dots(55.0)));
            }
            finally
            {
                if (fileName != null)
                {
                    File.Delete(AppSettings.AdvancedStylesFolder + fileName);
                }
            }
        });
    }

    [Fact]
    public void Settings_that_cannot_work_stop_the_pdf_with_a_message()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            (MainWindowViewModel model, Answers answers, string folder) = Start(cards: 2);
            answers.PdfToSave = Path.Combine(folder, "out.pdf");
            model.ProjectSettings.Settings.PrintLayout.Columns = 5;

            Assert.Null(HeadlessFixture.WaitFor(model.CreatePdfAsync(preview: false)));

            Assert.Contains(StringResources.Get("Cartas_no_entran_ancho"), Assert.Single(answers.Said));
            Assert.False(File.Exists(answers.PdfToSave));
            Assert.Empty(answers.PdfNames);
        });
    }

    [Fact]
    public void A_missing_image_is_asked_about_before_the_pdf_is_made()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            (MainWindowViewModel model, Answers answers, string folder) = Start(cards: 3);
            answers.PdfToSave = Path.Combine(folder, "out.pdf");
            File.Delete(Path.Combine(folder, "c3.png"));
            IPlatformServices services = Platform.Services;
            Platform.Services = new RecordingServices([]);
            try
            {
                answers.Answer = false;
                Assert.Null(HeadlessFixture.WaitFor(model.CreatePdfAsync(preview: false)));
                Assert.Contains("c3.png", Assert.Single(answers.Asked));
                Assert.Contains(StringResources.Get("Continuar"), answers.Asked[0]);
                Assert.False(File.Exists(answers.PdfToSave));

                answers.Answer = true;
                Assert.NotNull(HeadlessFixture.WaitFor(model.CreatePdfAsync(preview: false)));
                Assert.True(File.Exists(answers.PdfToSave));
            }
            finally
            {
                Platform.Services = services;
            }
        });
    }

    [Fact]
    public void A_project_saved_by_windows_opens_and_gives_the_pdf_windows_gave()
    {
        string? reference = WindowsReference();
        Assert.SkipWhen(reference == null, "The Windows reference files are not on this machine.");
        fixture.Run(LanguageId.Spanish, () =>
        {
            (MainWindowViewModel model, Answers answers, string folder) = Start();
            foreach (string name in new[] { "cartas", "cartas-traseras" })
            {
                Directory.CreateDirectory(Path.Combine(folder, name));
                foreach (string image in Directory.GetFiles(Path.Combine(reference!, name), "*.png"))
                {
                    File.Copy(image, Path.Combine(folder, name, Path.GetFileName(image)));
                }
            }

            string project = Path.Combine(folder, "cartas", "2-doble-cara.pnp");
            File.Copy(Path.Combine(reference!, "salida", "2-doble-cara.pnp"), project);
            IPlatformServices services = Platform.Services;
            Platform.Services = new RecordingServices([]);
            try
            {
                Assert.True(HeadlessFixture.WaitFor(model.OpenAsync(project)));
                Assert.Empty(answers.Said);
                Assert.Equal(12, model.ImagePanel.PanelCount);
                Assert.True(model.ImagePanel.IsDouble);
                Assert.Equal("frontal01", Fronts(model)[0]);
                Assert.Equal("trasera12", Path.GetFileNameWithoutExtension(model.ImagePanel.Cards[0].Card.Back.Path));

                answers.PdfToSave = Path.Combine(folder, "out.pdf");
                PdfOutput? output = HeadlessFixture.WaitFor(model.CreatePdfAsync(preview: false));

                Assert.NotNull(output);
                Assert.Empty(answers.Said);
                Assert.Equal(Pages(Path.Combine(reference!, "salida", "2-doble-cara.pdf")), Pages(answers.PdfToSave));
            }
            finally
            {
                Platform.Services = services;
            }
        });
    }

    private static string? WindowsReference()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
        {
            string candidate = Path.Combine(folder.FullName, "samples", "referencia-windows");
            if (Directory.Exists(Path.Combine(candidate, "salida")))
            {
                return candidate;
            }
        }

        return null;
    }

    // ---- Windows ----

    [Fact]
    public void Question_window_answers_yes_no_or_nothing()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var owner = new Window();
            owner.Show();

            bool? Answer(string button, bool withCancel)
            {
                var window = new ConfirmWindow("¿Seguro?", withCancel);
                Task<bool?> result = window.ShowDialog<bool?>(owner);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("¿Seguro?", window.FindControl<TextBlock>("MessageText")!.Text);
                Assert.Equal(withCancel, window.FindControl<Button>("CancelButton")!.IsVisible);
                if (button == "close")
                {
                    window.Close();
                }
                else
                {
                    window.FindControl<Button>(button)!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                }

                Dispatcher.UIThread.RunJobs();
                return result.Result;
            }

            Assert.True(Answer("YesButton", true));
            Assert.False(Answer("NoButton", false));
            Assert.Null(Answer("CancelButton", true));
            Assert.Null(Answer("close", true));
            owner.Close();
        });
    }

    [Fact]
    public void Progress_window_runs_its_task_and_closes_and_can_stop_it()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var owner = new Window();
            owner.Show();
            var reported = new List<int>();
            var window = new ProgressWindow("Crear", (report, _) =>
            {
                report(40);
                report(100);
                return Task.CompletedTask;
            });
            Task shown = window.ShowDialog(owner);
            HeadlessFixture.Wait(shown);
            Assert.Null(window.Failure);
            Assert.False(window.WasCancelled);
            Assert.Equal(100.0, window.FindControl<ProgressBar>("Bar")!.Value);
            Assert.Equal("100%", window.FindControl<TextBlock>("PercentLabel")!.Text);

            // A task that fails leaves what went wrong.
            window = new ProgressWindow("Crear", (_, _) => throw new InvalidOperationException("boom"));
            HeadlessFixture.Wait(window.ShowDialog(owner));
            Assert.Equal("boom", window.Failure!.Message);

            // Cancelling tells the task, and the window waits for it.
            var started = new ManualResetEventSlim();
            window = new ProgressWindow("Crear", (_, cancellation) =>
            {
                started.Set();
                cancellation.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                return Task.CompletedTask;
            });
            shown = window.ShowDialog(owner);
            while (!started.IsSet)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(5);
            }

            Screenshots.Capture(window, "ventana-progreso");
            window.Cancel();
            HeadlessFixture.Wait(shown);
            Assert.True(window.WasCancelled);
            owner.Close();
        });
    }

    [Fact]
    public void Main_window_shows_the_title_and_the_recent_files()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            AppSettings.StartMaximized = false;
            AppSettings.WindowSize = new PointI(1280, 800);
            (MainWindowViewModel model, Answers answers, string folder) = Start(cards: 2);
            answers.ProjectToSave = Path.Combine(folder, "shown.pnp");
            HeadlessFixture.Wait(model.SaveAsync(askPath: true));
            var window = new MainWindow { DataContext = model };
            model.Ask = (_, _) => Task.FromResult<bool?>(false);    // the window sets its own dialogs; keep the test's
            window.Show();
            model.SelectedTab = model.Tabs[0];
            Dispatcher.UIThread.RunJobs();
            HeadlessFixture.Wait(Task.Delay(100));

            Assert.Equal(model.WindowTitle, window.Title);
            Assert.EndsWith("shown.pnp", window.Title);
            ComboBox recent = window.GetVisualDescendants().OfType<ComboBox>().First(list => list.Name == "RecentList");
            Assert.Equal(1, recent.ItemCount);
            Assert.True(recent.IsEnabled);
            Screenshots.Capture(window, "ventana-archivo");

            model.ImagePanel.AddBlankCard();
            Assert.EndsWith("shown.pnp *", window.Title);
            window.Close();
            Dispatcher.UIThread.RunJobs();
            HeadlessFixture.Wait(Task.Delay(100));
        });
    }
}
