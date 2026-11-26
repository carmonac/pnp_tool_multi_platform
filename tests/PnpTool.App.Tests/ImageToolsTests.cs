using System.IO.Compression;
using Avalonia.Controls;
using Avalonia.Threading;
using ImageMagick;
using PnpTool.App.ViewModels;
using PnpTool.App.Views;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Projects;
using PnpTool.Imaging;

namespace PnpTool.App.Tests;

[Collection(UserInterface.Name)]
public sealed class ImageToolsTests(HeadlessFixture fixture)
{
    private static string Picture(string folder, string name, uint width = 300, uint height = 400, string color = "#3060C0")
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, name);
        using var image = new MagickImage(new MagickColor(color), width, height);
        image.Density = new Density(300);
        image.Write(path);
        return path;
    }

    /// <summary>An image with a different colour in every cell of a table, so that pieces can be told apart.</summary>
    private static string Sheet(string folder, string name, int columns, int rows, uint cell = 100)
    {
        string path = Path.Combine(folder, name);
        using var image = new MagickImage(MagickColors.White, (uint)columns * cell, (uint)rows * cell);
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                using var tile = new MagickImage(new MagickColor($"#{60 * column:X2}{100 * row:X2}80"), cell, cell);
                image.Composite(tile, column * (int)cell, row * (int)cell, CompositeOperator.Copy);
            }
        }

        image.Write(path);
        return path;
    }

    private static (uint Width, uint Height) SizeOf(string path)
    {
        var info = new MagickImageInfo(path);
        return (info.Width, info.Height);
    }

    // ---- Cutting ----

    [Fact]
    public void Images_are_cut_into_a_table_of_pieces()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            string sheet = Sheet(folder, "hoja.png", columns: 3, rows: 2);
            var cutter = new ImageCutter { Columns = 3, Rows = 2, Extension = ".png" };

            Assert.Equal(
                [(0, 0, 0, 0, 100, 100), (0, 1, 100, 0, 100, 100), (0, 2, 200, 0, 100, 100), (1, 0, 0, 100, 100, 100), (1, 1, 100, 100, 100, 100), (1, 2, 200, 100, 100, 100)],
                cutter.Pieces(300, 200));

            var shares = new List<double>();
            List<string> written = cutter.Cut([sheet, Path.Combine(folder, "no-existe.png")], Path.Combine(folder, "piezas"), shares.Add);

            Assert.Equal(6, written.Count);
            Assert.EndsWith("hoja_Y_0_X_0.png", written[0]);
            Assert.EndsWith("hoja_Y_1_X_2.png", written[5]);
            Assert.All(written, piece => Assert.Equal((100u, 100u), SizeOf(piece)));
            using var last = new MagickImage(written[5]);
            IMagickColor<ushort> colour = last.GetPixels().GetPixel(50, 50).ToColor()!;
            Assert.Equal(120, colour.R >> 8);     // the cell of the third column
            Assert.Equal(100, colour.G >> 8);     // and the second row
            Assert.Equal(100.0, shares[^1], 3);
        });
    }

    [Fact]
    public void Borders_are_left_out_and_nearly_empty_pieces_can_be_deleted()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            string sheet = Sheet(folder, "hoja.png", columns: 2, rows: 2);
            var cutter = new ImageCutter { Columns = 2, Rows = 1, BorderLeft = 20, BorderRight = 20, BorderTop = 10, BorderBottom = 30, Extension = ".png" };

            Assert.Equal([(0, 0, 20, 10, 80, 160), (0, 1, 100, 10, 80, 160)], cutter.Pieces(200, 200));
            List<string> written = cutter.Cut([sheet], Path.Combine(folder, "a"));
            Assert.Equal([(80u, 160u), (80u, 160u)], written.Select(SizeOf));

            cutter.DeleteSmallPieces = true;      // flat colours: files of a few hundred bytes
            Assert.Empty(cutter.Cut([sheet], Path.Combine(folder, "b")));
            Assert.Empty(Directory.GetFiles(Path.Combine(folder, "b")));
        });
    }

    [Fact]
    public void Cut_tool_takes_images_warns_about_sizes_and_keeps_borders_inside_the_image()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            string first = Picture(folder, "a.png", 300, 400);
            string other = Picture(folder, "b.png", 200, 400);
            File.WriteAllText(Path.Combine(folder, "notas.txt"), "not an image");
            var tool = new CutImagesViewModel();
            var notices = new List<string>();
            tool.Notice += notices.Add;
            int previews = 0;
            tool.PreviewChanged += (_, _) => previews++;

            tool.BorderLeft = 10;                 // no image yet
            Assert.Equal(0, tool.BorderLeft);

            tool.Add([folder]);

            Assert.Equal([first, other], tool.Images);
            Assert.Equal(first, tool.SelectedImage);
            Assert.Equal((300, 400), tool.ImageSize);
            Assert.Contains("300", tool.MaximumText);
            Assert.Contains(StringResources.Get("Imagen_no_mismo_tamano"), Assert.Single(notices));
            Assert.True(previews > 0);

            tool.BorderLeft = 100;
            tool.BorderRight = 250;               // together more than the width
            Assert.Equal(200, tool.BorderRight);
            tool.ResetBorders();
            Assert.Equal((0, 0), (tool.BorderLeft, tool.BorderRight));

            tool.Columns = 3;
            tool.Rows = 1;
            tool.Extension = ".png";
            notices.Clear();
            List<string> written = HeadlessFixture.WaitFor(tool.CutAsync(Path.Combine(folder, "piezas")));
            Assert.Equal(6, written.Count);
            Assert.Equal([StringResources.Get("proceso_completado")], notices);

            tool.SelectedImage = other;
            tool.RemoveSelected();
            Assert.Equal([first], tool.Images);
            Assert.Equal(first, tool.SelectedImage);
            tool.Clear();
            Assert.Null(tool.SelectedImage);
            notices.Clear();
            Assert.Empty(HeadlessFixture.WaitFor(tool.CutAsync(folder)));
            Assert.Equal([StringResources.Get("no_hay_imagenes")], notices);
        });
    }

    // ---- Changing images ----

    [Fact]
    public void Images_are_resized_given_an_effect_and_another_format()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            string picture = Picture(folder, "carta.jpg", 300, 400);
            string output = Path.Combine(folder, "salida");
            var editor = new ImageBatchEditor { ScaleX = 50.0, ScaleY = 25.0, Extension = ".png" };
            var done = new List<int>();

            (List<string> written, List<string> problems) = editor.Edit([picture, Path.Combine(folder, "no.jpg")], output, new ProjectSettings(), done.Add);

            Assert.Equal([Path.Combine(output, "carta_new.png")], written);
            Assert.Equal((150u, 100u), SizeOf(written[0]));
            Assert.Equal(MagickFormat.Png, new MagickImageInfo(written[0]).Format);
            Assert.StartsWith("no.jpg", Assert.Single(problems));
            Assert.Equal([1, 2], done);
            Assert.Equal((300u, 400u), SizeOf(picture));          // the original is left alone

            // Replacing writes over the image when the format is its own...
            editor = new ImageBatchEditor { ScaleX = 50.0, ScaleY = 50.0, Extension = ".jpg", Replace = true };
            (written, problems) = editor.Edit([picture], output, new ProjectSettings());
            Assert.Equal([picture], written);
            Assert.Empty(problems);
            Assert.Equal((150u, 200u), SizeOf(picture));

            // ...and not when it changes.
            editor.Extension = ".bmp";
            (written, _) = editor.Edit([picture], output, new ProjectSettings());
            Assert.Equal([Path.Combine(output, "carta_new.bmp")], written);

            // An effect: rounded corners over a red background change the corner.
            var settings = new ProjectSettings();
            editor = new ImageBatchEditor { Effect = ImageEffectType.RoundedCorners, EffectSize = 40, Background = new Core.Primitives.ArgbColor(255, 255, 0, 0), Extension = ".png" };
            (written, problems) = editor.Edit([Picture(folder, "azul.png", 300, 400)], output, settings);
            Assert.Empty(problems);
            using var rounded = new MagickImage(written[0]);
            IMagickColor<ushort> corner = rounded.GetPixels().GetPixel(1, 1).ToColor()!;
            Assert.True(corner.R > corner.B, "the corner is not the background colour");
            Assert.Equal(0.0, settings.Grid.CardSize.X.Millimeter, 3);   // the project is not touched
        });
    }

    [Fact]
    public void Edit_tool_offers_the_effects_without_mirror_and_processes_its_list()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            Picture(folder, "a.png");
            Picture(folder, "b.png");
            var tool = new EditImagesViewModel(new ProjectSettings());
            var notices = new List<string>();
            tool.Notice += notices.Add;

            Assert.DoesNotContain(tool.Effects, option => option.Value is ImageEffectType.Mirror or ImageEffectType.CreateOutline);
            Assert.Equal(ImageEffectType.None, tool.SelectedEffect.Value);
            Assert.False(tool.EffectHasSize);
            tool.SelectedEffect = tool.Effects.First(option => option.Value == ImageEffectType.CircularToken);
            Assert.True(tool.EffectHasSize);
            tool.SelectedEffect = tool.Effects[0];

            Assert.Empty(HeadlessFixture.WaitFor(tool.ProcessAsync(folder)));
            Assert.Equal([StringResources.Get("faltan_datos")], notices);

            tool.Add([folder]);
            Assert.Equal(2, tool.Images.Count);
            tool.ScaleXText = "50";
            tool.ScaleYText = string.Empty;       // an empty box is 100
            tool.Extension = ".png";
            Assert.Equal((50.0, 100.0), (tool.Editor.ScaleX, tool.Editor.ScaleY));

            List<string> written = HeadlessFixture.WaitFor(tool.ProcessAsync(Path.Combine(folder, "salida")));
            Assert.Equal(2, written.Count);
            Assert.All(written, file => Assert.Equal((150u, 400u), SizeOf(file)));
            Assert.Equal(0.0, tool.Progress);

            tool.SelectedImage = tool.Images[0];
            tool.RemoveSelected();
            Assert.Single(tool.Images);
        });
    }

    // ---- Packing a project ----

    private static Project TwoFolders(string folder)
    {
        var project = new Project();
        project.Deck.Add(new Card { Front = new CardFace(Picture(Path.Combine(folder, "uno"), "carta.png"), FaceType.Front, ImageKind.Normal) });
        project.Deck.Add(new Card { Front = new CardFace(Picture(Path.Combine(folder, "dos"), "carta.png", color: "#C03030"), FaceType.Front, ImageKind.Normal) });
        project.Deck.Add(new Card { Front = new CardFace(Path.Combine(folder, "uno", "carta.png"), FaceType.Front, ImageKind.Normal) });
        project.Settings.PrintLayout.BackImage = BackImageMode.SameBack;
        project.Settings.SharedBack = new CardFace(Picture(folder, "trasera.png"), FaceType.Back, ImageKind.Normal);
        return project;
    }

    [Fact]
    public void A_packed_project_carries_its_images_and_opens_from_its_folder()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            string target = Path.Combine(folder, "destino");
            var shares = new List<int>();

            ProjectPacker.Packed packed = ProjectPacker.Pack(TwoFolders(folder), target, "juego", createZip: false, deleteFiles: true, shares.Add);

            Assert.Equal(Path.Combine(target, "juego"), packed.Folder);
            Assert.Null(packed.ZipFile);
            // Two images called the same do not overwrite each other, and one used twice is copied once.
            Assert.Equal(["carta.png", "carta_2.png", "trasera.png"], Directory.GetFiles(packed.ImagesFolder).Select(Path.GetFileName).Order(StringComparer.Ordinal));
            Assert.True(File.Exists(packed.ProjectFile));         // nothing is deleted without a zip
            Assert.Equal(100, shares[^1]);

            (AppError error, Project opened) = ProjectFileStore.LoadProject(packed.ProjectFile);
            Assert.False(error.HasError, error.Message);
            Assert.Equal(3, opened.Deck.Cards.Length);
            Assert.All(opened.Deck.Cards, card => Assert.True(File.Exists(card.Front.OriginalPath), card.Front.OriginalPath));
            Assert.StartsWith(packed.ImagesFolder, opened.Deck.Cards[1].Front.OriginalPath);
            Assert.Equal(opened.Deck.Cards[0].Front.OriginalPath, opened.Deck.Cards[2].Front.OriginalPath);
            Assert.NotEqual(opened.Deck.Cards[0].Front.OriginalPath, opened.Deck.Cards[1].Front.OriginalPath);
            Assert.True(File.Exists(opened.Settings.SharedBack.OriginalPath));
        });
    }

    [Fact]
    public void A_zip_holds_the_pack_and_can_be_all_that_is_left()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();

            ProjectPacker.Packed kept = ProjectPacker.Pack(TwoFolders(folder), Path.Combine(folder, "a"), "juego", createZip: true, deleteFiles: false);
            Assert.True(File.Exists(kept.ZipFile));
            Assert.True(File.Exists(kept.ProjectFile));
            using (ZipArchive zip = ZipFile.OpenRead(kept.ZipFile!))
            {
                Assert.Equal(["images/carta.png", "images/carta_2.png", "images/trasera.png", "juego.pnp"], zip.Entries.Select(entry => entry.FullName.Replace('\\', '/')).Order(StringComparer.Ordinal));
            }

            ProjectPacker.Packed only = ProjectPacker.Pack(TwoFolders(folder), Path.Combine(folder, "b"), "juego", createZip: true, deleteFiles: true);
            Assert.Equal([only.ZipFile], Directory.GetFileSystemEntries(only.Folder));
        });
    }

    [Fact]
    public void Pack_window_needs_a_folder_and_a_name()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            var pack = new PackProjectViewModel(TwoFolders(folder), Path.Combine(folder, "mi juego.pnp"));
            var notices = new List<string>();
            pack.Notice += notices.Add;
            Assert.Equal("mi juego", pack.Name);
            Assert.Equal("Project", new PackProjectViewModel(new Project(), string.Empty).Name);

            Assert.Null(HeadlessFixture.WaitFor(pack.PackAsync()));
            Assert.Equal([StringResources.Get("faltan_datos")], notices);

            pack.Folder = Path.Combine(folder, "destino");
            pack.DeleteFiles = true;              // ignored without a zip
            ProjectPacker.Packed? packed = HeadlessFixture.WaitFor(pack.PackAsync());
            Assert.NotNull(packed);
            Assert.True(File.Exists(Path.Combine(folder, "destino", "mi juego", "mi juego.pnp")));
            Assert.False(pack.IsBusy);
        });
    }

    // ---- Folders into PDFs ----

    [Fact]
    public void Folder_list_takes_folders_with_their_subfolders_and_reorders_them()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            string a = Directory.CreateDirectory(Path.Combine(folder, "a")).FullName;
            string a1 = Directory.CreateDirectory(Path.Combine(a, "uno")).FullName;
            string a2 = Directory.CreateDirectory(Path.Combine(a, "dos")).FullName;
            string b = Directory.CreateDirectory(Path.Combine(folder, "b")).FullName;
            string file = Picture(folder, "suelta.png");
            var list = new ProcessFoldersViewModel();
            var notices = new List<string>();
            list.Notice += notices.Add;
            Assert.False(list.Ready());
            Assert.Single(notices);

            list.Add([b, file]);
            list.IncludeSubfolders = true;
            list.Add([a]);
            Assert.Equal([b, a, a2, a1], list.Folders);
            Assert.True(list.Ready());

            list.SelectedIndex = 3;
            list.MoveUp();
            list.MoveUp();
            Assert.Equal([b, a1, a, a2], list.Folders);
            Assert.Equal(1, list.SelectedIndex);
            list.MoveDown();
            Assert.Equal(2, list.SelectedIndex);
            list.RemoveSelected();
            Assert.Equal([b, a, a2], list.Folders);
            Assert.Equal(1, list.SelectedIndex);
        });
    }

    private static MainWindowViewModel Model(List<string> said, Func<string, Task<string?>> pdf, Func<Task<string?>> folder) => new()
    {
        ShowMessage = message =>
        {
            said.Add(message);
            return Task.CompletedTask;
        },
        Ask = (_, _) => Task.FromResult<bool?>(true),
        AskPdfToSave = pdf,
        AskFolder = folder,
    };

    private sealed class QuietServices(List<string> opened) : IPlatformServices
    {
        public void OpenUrl(string url) => opened.Add("url " + url);

        public void OpenFile(string path) => opened.Add("file " + path);

        public void OpenFolder(string path) => opened.Add("folder " + path);

        public void RevealFile(string path) => opened.Add("reveal " + path);
    }

    private static int Pages(string pdf)
    {
        using var document = new iText.Kernel.Pdf.PdfDocument(new iText.Kernel.Pdf.PdfReader(pdf));
        return document.GetNumberOfPages();
    }

    [Fact]
    public void Folders_become_one_pdf_or_a_pdf_each_without_touching_the_panel()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            string first = Path.Combine(folder, "primera");
            string second = Path.Combine(folder, "segunda");
            string empty = Directory.CreateDirectory(Path.Combine(folder, "vacia")).FullName;
            for (int index = 1; index <= 10; index++)
            {
                Picture(first, "c" + index + ".png", 150, 210);
            }

            Picture(second, "d1.png", 150, 210);
            var said = new List<string>();
            var opened = new List<string>();
            string single = Path.Combine(folder, "todo");
            string each = Directory.CreateDirectory(Path.Combine(folder, "cada")).FullName;
            MainWindowViewModel model = Model(said, _ => Task.FromResult<string?>(single), () => Task.FromResult<string?>(each));
            model.ProjectSettings.SelectedGroup = model.ProjectSettings.Groups.First(option => option.Group == SettingsGroup.Cards);
            bool onePerFolder = false;
            model.ShowProcessFolders = list =>
            {
                list.Add([first, second, empty]);
                list.OnePdfPerFolder = onePerFolder;
                return Task.FromResult<bool?>(true);
            };
            IPlatformServices services = Platform.Services;
            Platform.Services = new QuietServices(opened);
            try
            {
                List<string> made = HeadlessFixture.WaitFor(model.ProcessFoldersAsync());
                Assert.True(made.Count == 1, string.Join(" | ", said));
                Assert.Equal([single + ".pdf"], made);
                Assert.Equal(2, Pages(made[0]));                  // eleven cards, nine a sheet
                Assert.Equal(["file " + single + ".pdf"], opened);
                Assert.Empty(said);
                Assert.Equal(0, model.ImagePanel.PanelCount);

                onePerFolder = true;
                opened.Clear();
                made = HeadlessFixture.WaitFor(model.ProcessFoldersAsync());
                Assert.Equal([Path.Combine(each, "primera.pdf"), Path.Combine(each, "segunda.pdf")], made);
                Assert.Equal(2, Pages(made[0]));
                Assert.Equal(1, Pages(made[1]));
                Assert.Equal(["folder " + each], opened);
                Assert.Contains(StringResources.Get("no_hay_imagenes"), Assert.Single(said));   // the empty folder

                // Closing the list, or not choosing where to save, makes nothing.
                model.ShowProcessFolders = _ => Task.FromResult<bool?>(null);
                Assert.Empty(HeadlessFixture.WaitFor(model.ProcessFoldersAsync()));
            }
            finally
            {
                Platform.Services = services;
            }
        });
    }

    [Fact]
    public void A_book_with_double_pages_offers_to_cut_them_instead()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            string pages = Path.Combine(folder, "libro");
            Picture(pages, "p1.png", 300, 400);
            Picture(pages, "p2.png", 300, 400);
            Picture(pages, "p3.png", 300, 400);
            string wide = Picture(pages, "p4.png", 900, 400);
            var said = new List<string>();
            MainWindowViewModel model = Model(said, _ => Task.FromResult<string?>(Path.Combine(folder, "libro.pdf")), () => Task.FromResult<string?>(null));
            model.ProjectSettings.SelectedGroup = model.ProjectSettings.Groups.First(option => option.Group == SettingsGroup.Books);
            Assert.Equal(ProjectType.Book, model.ProjectSettings.Settings.Group.ProjectType);
            model.ShowProcessFolders = list =>
            {
                list.Add([pages]);
                return Task.FromResult<bool?>(true);
            };
            CutImagesViewModel? cut = null;
            model.ShowCutImages = tool =>
            {
                cut = tool;
                return Task.CompletedTask;
            };

            Assert.Empty(HeadlessFixture.WaitFor(model.ProcessFoldersAsync()));

            Assert.NotNull(cut);
            Assert.Equal([wide], cut.Images);
            Assert.Equal((2, 1), (cut.Columns, cut.Rows));
            Assert.False(File.Exists(Path.Combine(folder, "libro.pdf")));
        });
    }

    // ---- Windows ----

    [Fact]
    public void Tool_windows_show_what_their_models_hold()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            var owner = new Window();
            owner.Show();

            var cut = new CutImagesViewModel([Sheet(folder, "hoja.png", 3, 3)]) { Columns = 3, Rows = 3, BorderLeft = 20, BorderTop = 10 };
            var cutWindow = new CutImagesWindow(cut);
            cutWindow.Show(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(9, cutWindow.FindControl<CutPreview>("Preview")!.Pieces.Count);
            Assert.Equal("3", cutWindow.FindControl<TextBox>("ColumnsBox")!.Text);
            cut.Columns = 2;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(6, cutWindow.FindControl<CutPreview>("Preview")!.Pieces.Count);
            Screenshots.Capture(cutWindow, "ventana-cortar-imagenes");
            cutWindow.AskFolder = () => Task.FromResult<string?>(Path.Combine(folder, "piezas"));
            cut.Extension = ".png";
            var cutNotices = new List<string>();
            cut.Notice += cutNotices.Add;
            HeadlessFixture.Wait(cutWindow.CutAsync());
            Assert.Equal(6, Directory.GetFiles(Path.Combine(folder, "piezas")).Length);
            cutWindow.Close();

            var edit = new EditImagesViewModel(new ProjectSettings());
            edit.Add([Picture(folder, "a.png"), Picture(folder, "b.png")]);
            edit.SelectedEffect = edit.Effects.First(option => option.Value == ImageEffectType.RoundedCorners);
            var editWindow = new EditImagesWindow(edit);
            editWindow.Show(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, editWindow.FindControl<ListBox>("ImagesBox")!.ItemCount);
            Assert.True(editWindow.FindControl<TextBox>("EffectSizeBox")!.IsVisible);
            Screenshots.Capture(editWindow, "ventana-modificar-imagenes");
            editWindow.Close();

            var pack = new PackProjectViewModel(new Project(), "juego.pnp") { CreateZip = true };
            var packWindow = new PackProjectWindow(pack);
            packWindow.Show(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.True(packWindow.FindControl<CheckBox>("DeleteBox")!.IsVisible);
            pack.CreateZip = false;
            Dispatcher.UIThread.RunJobs();
            Assert.False(packWindow.FindControl<CheckBox>("DeleteBox")!.IsVisible);
            Screenshots.Capture(packWindow, "ventana-empaquetar");
            packWindow.Close();

            var folders = new ProcessFoldersViewModel();
            var foldersWindow = new ProcessFoldersWindow(folders);
            Task<bool?> result = foldersWindow.ShowDialog<bool?>(owner);
            folders.Add([folder]);
            Dispatcher.UIThread.RunJobs();
            Screenshots.Capture(foldersWindow, "ventana-procesar-carpetas");
            foldersWindow.Accept();
            Dispatcher.UIThread.RunJobs();
            Assert.True(result.Result);
            owner.Close();
        });
    }
}
