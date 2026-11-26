using Avalonia.Controls;
using Avalonia.Threading;
using ImageMagick;
using iText.IO.Image;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using PnpTool.App.ViewModels;
using PnpTool.App.Views;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Imaging;
using PnpTool.Pdf.Tools;
using Path = System.IO.Path;

namespace PnpTool.App.Tests;

[Collection(UserInterface.Name)]
public sealed class PdfToolsTests(HeadlessFixture fixture)
{
    private static string Picture(string folder, string name, uint width = 300, uint height = 420, string color = "#3060C0", bool noisy = false)
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, name);
        using var image = new MagickImage(new MagickColor(color), width, height);
        if (noisy)
        {
            image.AddNoise(NoiseType.Gaussian);    // so that the file is not tiny
        }

        image.Density = new Density(300);
        image.Write(path);
        return path;
    }

    /// <summary>A PDF with one image on each of its pages.</summary>
    private static string Pdf(string folder, string name, int pages, float width = 595f, float height = 842f)
    {
        string path = Path.Combine(folder, name);
        using var writer = new PdfWriter(path);
        using var pdf = new PdfDocument(writer);
        using var document = new Document(pdf, new PageSize(width, height));
        for (int page = 1; page <= pages; page++)
        {
            string picture = Picture(folder, Path.GetFileNameWithoutExtension(name) + "-" + page + ".png", 200, 280, page % 2 == 0 ? "#C03030" : "#30A050", noisy: true);
            if (page > 1)
            {
                document.Add(new iText.Layout.Element.AreaBreak());
            }

            document.Add(new iText.Layout.Element.Image(ImageDataFactory.Create(picture)).SetFixedPosition(page, 50f, 400f, 150f));
        }

        return path;
    }

    private static int Pages(string pdf)
    {
        using var document = new PdfDocument(new PdfReader(pdf));
        return document.GetNumberOfPages();
    }

    private static (double Width, double Height) PageMillimetres(string pdf)
    {
        using var document = new PdfDocument(new PdfReader(pdf));
        Rectangle size = document.GetPage(1).GetPageSize();
        return (Math.Round(size.GetWidth() * 25.4 / 72.0), Math.Round(size.GetHeight() * 25.4 / 72.0));
    }

    private sealed class RecordingServices(List<string> opened) : IPlatformServices
    {
        public void OpenUrl(string url) => opened.Add("url " + url);

        public void OpenFile(string path) => opened.Add("file " + path);

        public void OpenFolder(string path) => opened.Add("folder " + path);

        public void RevealFile(string path) => opened.Add("reveal " + path);
    }

    private static void WithServices(List<string> opened, Action action)
    {
        IPlatformServices services = Platform.Services;
        Platform.Services = new RecordingServices(opened);
        try
        {
            action();
        }
        finally
        {
            Platform.Services = services;
        }
    }

    // ---- Extracting ----

    [Fact]
    public void Images_are_taken_out_of_pdfs_or_their_pages_become_images()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            string first = Pdf(folder, "uno.pdf", pages: 2);
            string second = Pdf(folder, "dos.pdf", pages: 1);
            var tool = new ExtractPdfViewModel();
            var notices = new List<string>();
            tool.Notice += notices.Add;

            Assert.Null(HeadlessFixture.WaitFor(tool.ExtractAsync(Path.Combine(folder, "nada"))));
            Assert.Equal([StringResources.Get("faltan_datos")], notices);

            tool.Add([first, Path.Combine(folder, "uno-1.png"), second]);
            Assert.Equal([first, second], tool.Files);

            notices.Clear();
            string images = Path.Combine(folder, "imagenes");
            List<string>? written = HeadlessFixture.WaitFor(tool.ExtractAsync(images));
            Assert.NotNull(written);
            Assert.Equal(3, written.Count);                      // the images of both PDFs
            Assert.All(written, file => Assert.True(ImageFormats.IsImage(file), file));
            Assert.Equal([StringResources.Get("proceso_completado")], notices);
            Assert.False(tool.IsBusy);
            Assert.True(ExtractPdfViewModel.HasFiles(images));

            tool.WholePages = true;
            tool.SelectedFile = second;
            tool.RemoveSelected();
            string pages = Path.Combine(folder, "paginas");
            written = HeadlessFixture.WaitFor(tool.ExtractAsync(pages));
            Assert.Equal(2, written!.Count);                     // a PNG a page
            Assert.All(written, file => Assert.EndsWith(".png", file));

            // Images under the size of the settings can be thrown away.
            tool.WholePages = false;
            tool.DeleteSmall = true;
            AppSettings.SmallFileSizeKb = 100_000;
            try
            {
                Assert.Empty(HeadlessFixture.WaitFor(tool.ExtractAsync(Path.Combine(folder, "pequenas")))!);
            }
            finally
            {
                AppSettings.SmallFileSizeKb = 5;
            }
        });
    }

    [Fact]
    public void Extracted_images_reach_the_panel_when_asked_to()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            string pdf = Pdf(folder, "cartas.pdf", pages: 2);
            var model = new MainWindowViewModel();
            model.ShowExtractPdf = tool =>
            {
                tool.Add([pdf]);
                List<string>? written = HeadlessFixture.WaitFor(tool.ExtractAsync(Path.Combine(folder, "salida")));
                return Task.FromResult(tool.Import ? written : []);
            };

            HeadlessFixture.Wait(model.ExtractPdfAsync());
            Assert.Equal(2, model.ImagePanel.PanelCount);

            model.ShowExtractPdf = _ => Task.FromResult<List<string>?>(null);
            HeadlessFixture.Wait(model.ExtractPdfAsync());
            Assert.Equal(2, model.ImagePanel.PanelCount);
        });
    }

    // ---- Joining ----

    [Fact]
    public void Pdfs_are_joined_in_the_order_of_the_list_or_page_by_page()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            string a = Pdf(folder, "a.pdf", pages: 2);
            string b = Pdf(folder, "b.pdf", pages: 1);
            string c = Pdf(folder, "c.pdf", pages: 2);
            var tool = new MergePdfViewModel();
            var notices = new List<string>();
            tool.Notice += notices.Add;
            var opened = new List<string>();
            tool.Add([a, Path.Combine(folder, "a-1.png"), b]);
            Assert.Equal([a, b], tool.Files);

            Assert.False(HeadlessFixture.WaitFor(tool.MergeAsync(null)));
            Assert.Equal([StringResources.Get("faltan_datos")], notices);

            notices.Clear();
            string joined = Path.Combine(folder, "unido");
            Assert.True(HeadlessFixture.WaitFor(tool.MergeAsync(joined)));
            Assert.Equal(3, Pages(joined + ".pdf"));
            Assert.Equal([StringResources.Get("proceso_completado")], notices);

            // Page by page needs two PDFs with the same number of pages.
            tool.Interleave = true;
            notices.Clear();
            Assert.False(HeadlessFixture.WaitFor(tool.MergeAsync(Path.Combine(folder, "mal.pdf"))));
            Assert.Single(notices);
            Assert.False(File.Exists(Path.Combine(folder, "mal.pdf")));

            tool.SelectedIndex = 1;
            tool.RemoveSelected();
            tool.Add([c]);
            tool.SelectedIndex = 1;
            tool.MoveUp();
            Assert.Equal([c, a], tool.Files);
            Assert.Equal(0, tool.SelectedIndex);
            tool.MoveDown();
            Assert.Equal([a, c], tool.Files);
            tool.OpenWhenDone = true;
            notices.Clear();
            string turns = Path.Combine(folder, "turnos.pdf");
            WithServices(opened, () => Assert.True(HeadlessFixture.WaitFor(tool.MergeAsync(turns))));
            Assert.Equal(4, Pages(turns));
            Assert.Equal(["file " + turns], opened);
            Assert.Empty(notices);
        });
    }

    // ---- Fronts and backs ----

    [Fact]
    public void Images_are_sorted_into_fronts_and_backs_by_their_names()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            Picture(folder, "carta_f_1.png");
            Picture(folder, "carta_f_2.png");
            Picture(folder, "carta_b_1.png");
            var tool = new SplitFrontsBacksViewModel();
            var notices = new List<string>();
            tool.Notice += notices.Add;

            Assert.Null(tool.Split());
            tool.Folder = folder;
            Assert.Null(tool.Split());                           // no text to tell them apart
            Assert.Equal(2, notices.Count);

            tool.Marker = "_f_";
            Assert.Equal((2, 1), tool.Split());
            Assert.Equal(2, Directory.GetFiles(Path.Combine(folder, "front")).Length);
            Assert.Single(Directory.GetFiles(Path.Combine(folder, "back")));
            Assert.Equal(StringResources.Get("proceso_completado"), notices[^1]);
        });
    }

    // ---- Changing a PDF ----

    [Fact]
    public void A_pdf_gets_sheets_of_another_size_or_its_content_moved()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            string pdf = Pdf(folder, "hojas.pdf", pages: 2);
            var tool = new ModifyPdfViewModel();
            var notices = new List<string>();
            tool.Notice += notices.Add;

            Assert.Null(tool.ChangeSize());
            Assert.Equal([StringResources.Get("faltan_datos")], notices);

            tool.File = pdf;
            Assert.Contains("A4", tool.CurrentSizeText);
            Assert.Equal(Path.Combine(folder, "hojas_NewSize.pdf"), tool.ResizedFile);
            tool.Paper.Selected = tool.Paper.Options.First(option => option.Value == Core.Paper.PaperType.A3);
            notices.Clear();
            string? resized = tool.ChangeSize();
            Assert.Equal(tool.ResizedFile, resized);
            Assert.Equal((297.0, 420.0), PageMillimetres(resized!));
            Assert.Equal(2, Pages(resized!));
            Assert.Equal([StringResources.Get("proceso_completado")], notices);

            // Moving and turning the even pages.
            Assert.False(tool.PagesTyped);
            tool.SelectedPages = (int)PageSelection.Listed;
            Assert.True(tool.PagesTyped);
            tool.PagesText = "2";
            tool.OffsetXText = "1.5";
            tool.OffsetYText = "-2";
            tool.AngleText = "0.5";
            notices.Clear();
            Assert.False(tool.Align(pdf));                       // not over itself
            Assert.Single(notices);
            string aligned = Path.Combine(folder, "alineado.pdf");
            Assert.True(tool.Align(aligned));
            Assert.Equal(2, Pages(aligned));
            Assert.Equal((210.0, 297.0), PageMillimetres(aligned));
        });
    }

    // ---- The cover of a book ----

    [Fact]
    public void Mirror_of_a_cover_grows_the_image_into_its_bleed()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            var settings = new BookCoverSettings
            {
                CoverSize = new UnitPoint { X = Unit.FromMillimeter(100.0), Y = Unit.FromMillimeter(200.0) },
                Bleed = Unit.FromMillimeter(10.0),
            };
            // Red on the left half and blue on the right, so that mirrored strips can be told.
            using var image = new MagickImage(new MagickColor("#0000FF"), 100, 200);
            using var red = new MagickImage(new MagickColor("#FF0000"), 50, 200);
            image.Composite(red, 0, 0, CompositeOperator.Copy);

            using MagickImage left = BookCoverMirror.Apply(image, settings, BookCoverPart.Left);
            Assert.Equal((110u, 220u), (left.Width, left.Height));
            Assert.Equal(65535, left.GetPixels().GetPixel(2, 110).ToColor()!.R);      // the mirrored strip is red
            Assert.Equal(65535, left.GetPixels().GetPixel(105, 5).ToColor()!.B);     // above the blue half, blue

            using MagickImage right = BookCoverMirror.Apply(image, settings, BookCoverPart.Right);
            Assert.Equal((110u, 220u), (right.Width, right.Height));
            Assert.Equal(65535, right.GetPixels().GetPixel(108, 110).ToColor()!.B);  // the strip beyond the right edge
            Assert.Equal(65535, right.GetPixels().GetPixel(5, 215).ToColor()!.R);

            using MagickImage spine = BookCoverMirror.Apply(image, settings, BookCoverPart.Spine);
            Assert.Equal((100u, 220u), (spine.Width, spine.Height));
            Assert.Equal((100u, 200u), (image.Width, image.Height));               // the image given is not changed
        });
    }

    [Fact]
    public void Cover_tool_checks_its_settings_and_writes_one_sheet()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            var tool = new BookCoverViewModel();
            var notices = new List<string>();
            tool.Notice += notices.Add;
            var opened = new List<string>();

            Assert.StartsWith("Cover Size", tool.Problem());
            tool.CoverWidthText = "150";
            tool.CoverHeightText = "210";
            tool.PageCountText = "200";
            // The spine follows pages and paper: 200 × 0.01417 + 0.015.
            Assert.Equal("2.849", tool.SpineWidthText);
            tool.SelectedStock = tool.Stocks[0];
            Assert.Equal("0.010695", tool.PageThicknessText);
            tool.SpineWidthText = "12";
            Assert.Equal(StringResources.Get("ShellMenuBarItem_Imagenes_AnadirImagen"), tool.Problem());

            tool.SetImage(BookCoverPart.Left, Picture(folder, "contra.png", 600, 840, "#205080"));
            tool.SetImage(BookCoverPart.Spine, Picture(folder, "lomo.png", 48, 840, "#101010"));
            tool.SetImage(BookCoverPart.Right, Picture(folder, "portada.png", 600, 840, "#C04020"));
            Assert.Null(tool.Problem());
            Assert.Equal(StringResources.Get("Form_PortadaLibro_ContraPortada"), tool.LeftLabel);
            tool.SelectedSpineSide = 1;
            Assert.Equal(StringResources.Get("Form_PortadaLibro_Portada"), tool.LeftLabel);

            tool.BleedText = "0";                                // the mirror needs bleed
            Assert.EndsWith("<=0", tool.Problem());
            tool.BleedText = "3";
            tool.CoverWidthText = "300";                         // two covers of 300 do not fit 500
            Assert.StartsWith(StringResources.Get("Form_PortadaLibro_No_Caben"), tool.Problem());
            Assert.False(HeadlessFixture.WaitFor(tool.CreateAsync(Path.Combine(folder, "no.pdf"))));
            Assert.Single(notices);
            tool.CoverWidthText = "150";

            string pdf = Path.Combine(folder, "portada.pdf");
            notices.Clear();
            WithServices(opened, () => Assert.True(HeadlessFixture.WaitFor(tool.CreateAsync(pdf)), string.Join(" | ", notices)));
            Assert.Equal(["file " + pdf], opened);
            Assert.Equal(1, Pages(pdf));
            Assert.Equal((500.0, 330.0), PageMillimetres(pdf));  // the sheet for covers, on its side

            // Back cover, spine and front cover, side by side in the middle of the sheet.
            using SkiaSharp.SKBitmap sheet = PDFtoImage.Conversion.ToImage(File.ReadAllBytes(pdf), 0, options: new PDFtoImage.RenderOptions(Dpi: 72));
            static int Dots(double millimetres) => (int)(millimetres * 72.0 / 25.4);
            SkiaSharp.SKColor back = sheet.GetPixel(Dots(250.0 - 6.0 - 75.0), Dots(165.0));
            SkiaSharp.SKColor spine = sheet.GetPixel(Dots(250.0), Dots(165.0));
            SkiaSharp.SKColor front = sheet.GetPixel(Dots(250.0 + 6.0 + 75.0), Dots(165.0));
            Assert.True(back.Blue > back.Red, back.ToString());
            Assert.True(spine.Red < 60 && spine.Blue < 60, spine.ToString());
            Assert.True(front.Red > front.Blue, front.ToString());
            Assert.Equal(SkiaSharp.SKColors.White, sheet.GetPixel(Dots(40.0), Dots(30.0)));
        });
    }

    // ---- Windows ----

    [Fact]
    public void Pdf_tool_windows_show_what_their_models_hold()
    {
        fixture.Run(LanguageId.Spanish, () =>
        {
            string folder = fixture.NewFolder();
            string pdf = Pdf(folder, "hojas.pdf", pages: 2);
            var owner = new Window();
            owner.Show();

            var extract = new ExtractPdfViewModel();
            extract.Add([pdf]);
            var extractWindow = new ExtractPdfWindow(extract);
            Task<List<string>?> extracted = extractWindow.ShowDialog<List<string>?>(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, extractWindow.FindControl<ListBox>("FilesBox")!.ItemCount);
            Screenshots.Capture(extractWindow, "ventana-extraer-pdf");
            Picture(Path.Combine(folder, "destino"), "ya.png");
            extractWindow.AskFolder = () => Task.FromResult<string?>(Path.Combine(folder, "destino"));
            var asked = new List<string>();
            extractWindow.Ask = question =>
            {
                asked.Add(question);
                return Task.FromResult<bool?>(true);
            };
            HeadlessFixture.Wait(extractWindow.ExtractAsync());
            Dispatcher.UIThread.RunJobs();
            Assert.Equal([StringResources.Get("Directorio_Tiene_Archivos")], asked);
            Assert.Equal(2, extracted.Result!.Count);
            Assert.All(extracted.Result, file => Assert.StartsWith(Path.Combine(folder, "destino", ExtractPdfViewModel.Subfolder), file));

            var merge = new MergePdfViewModel();
            merge.Add([pdf, pdf]);
            var mergeWindow = new MergePdfWindow(merge);
            Task<bool?> merged = mergeWindow.ShowDialog<bool?>(owner);
            Dispatcher.UIThread.RunJobs();
            Screenshots.Capture(mergeWindow, "ventana-unir-pdf");
            merge.OpenWhenDone = true;
            mergeWindow.AskFile = () => Task.FromResult<string?>(Path.Combine(folder, "doble.pdf"));
            WithServices([], () => HeadlessFixture.Wait(mergeWindow.MergeAsync()));
            Dispatcher.UIThread.RunJobs();
            Assert.True(merged.Result);
            Assert.Equal(4, Pages(Path.Combine(folder, "doble.pdf")));

            string images = Path.Combine(folder, "imagenes");
            Picture(images, "a_front.png");
            Picture(images, "a_back.png");
            var split = new SplitFrontsBacksViewModel { Folder = images, Marker = "front" };
            var splitWindow = new SplitFrontsBacksWindow(split);
            Task<bool?> sorted = splitWindow.ShowDialog<bool?>(owner);
            Dispatcher.UIThread.RunJobs();
            Screenshots.Capture(splitWindow, "ventana-separar-frontales");
            HeadlessFixture.Wait(splitWindow.AcceptAsync(tell: false));
            Dispatcher.UIThread.RunJobs();
            Assert.True(sorted.Result);
            Assert.True(File.Exists(Path.Combine(images, "front", "a_front.png")));

            var modify = new ModifyPdfViewModel { File = pdf };
            var modifyWindow = new ModifyPdfWindow(modify);
            modifyWindow.Show(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("A4", modifyWindow.FindControl<TextBlock>("CurrentSizeBlock")!.Text);
            Screenshots.Capture(modifyWindow, "ventana-modificar-pdf");
            modifyWindow.Close();

            var cover = new BookCoverViewModel { CoverWidthText = "150", CoverHeightText = "210", PageCountText = "200" };
            cover.SetImage(BookCoverPart.Left, Picture(folder, "contra.png", 600, 840, "#205080"));
            cover.SetImage(BookCoverPart.Spine, Picture(folder, "lomo.png", 48, 840, "#101010"));
            cover.SetImage(BookCoverPart.Right, Picture(folder, "portada.png", 600, 840, "#C04020"));
            var coverWindow = new BookCoverWindow(cover);
            coverWindow.Show(owner);
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(coverWindow.FindControl<Image>("RightPicture")!.Source);
            Screenshots.Capture(coverWindow, "ventana-portada-libro");
            coverWindow.AskFile = () => Task.FromResult<string?>(Path.Combine(folder, "cubierta.pdf"));
            WithServices([], () => Assert.True(HeadlessFixture.WaitFor(coverWindow.CreateAsync())));
            Assert.True(File.Exists(Path.Combine(folder, "cubierta.pdf")));
            coverWindow.Close();
            owner.Close();
        });
    }
}
