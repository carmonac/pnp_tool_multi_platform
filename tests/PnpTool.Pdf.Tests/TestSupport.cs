using ImageMagick;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using PnpTool.Core;
using PnpTool.Core.Cards;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Projects;
using PnpTool.Imaging;
using SkiaSharp;
using Path = System.IO.Path;

namespace PnpTool.Pdf.Tests;

/// <summary>Tests that use the program's static folders and settings do not run at the same time.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalState
{
    public const string Name = "Global state";
}

/// <summary>
/// A scratch folder with the program pointed at the bundled assets and at folders inside it.
/// Everything is put back and deleted on dispose.
/// </summary>
public sealed class Workspace : IDisposable
{
    private readonly string _assets = AppPaths.AssetsFolder;
    private readonly string _user = AppPaths.UserDataFolder;
    private readonly string _temp = AppSettings.TempFolder;
    private readonly bool _embed = AppSettings.EmbedColorProfile;

    public Workspace()
    {
        Root = Directory.CreateTempSubdirectory("pnptool-pdf-").FullName;
        AppPaths.Initialize(AppPaths.FindAssetsFolder(AppContext.BaseDirectory), File("user"));
        AppSettings.TempFolder = File("tmp") + Path.DirectorySeparatorChar;
        AppSettings.EmbedColorProfile = false;
        Directory.CreateDirectory(AppSettings.TempFolder);
        Directory.CreateDirectory(File("out"));
    }

    public string Root { get; }

    public string WorkFolder => File("work");

    public string File(params string[] parts) => Path.Combine([Root, .. parts]);

    /// <summary>Colours easy to tell apart, one per card.</summary>
    public static readonly string[] Colors =
    [
        "#FF0000", "#00FF00", "#0000FF", "#FFFF00", "#FF00FF", "#00FFFF", "#800000", "#008000", "#000080",
        "#808000", "#800080", "#008080", "#FF8000", "#8000FF", "#0080FF", "#80FF00", "#FF0080", "#404040",
    ];

    /// <summary>The colour of the back of card <paramref name="index"/>: its front colour darkened.</summary>
    public static string BackColor(int index)
    {
        var color = new MagickColor(Colors[index % Colors.Length]);
        return $"#{(color.R >> 8) / 2:X2}{(color.G >> 8) / 2:X2}{(color.B >> 8) / 2:X2}";
    }

    /// <summary>Writes a PNG of one colour, 180 × 252 pixels, and returns the face that points to it.</summary>
    public CardFace Face(string name, string color, FaceType type = FaceType.Front)
    {
        string path = File(name + ".png");
        using (var image = new MagickImage(new MagickColor(color), 180, 252))
        {
            image.Density = new Density(72, DensityUnit.PixelsPerInch);
            image.Write(path);
        }

        CardFace face = MagickImageInspector.GetImageProperties(path, null);
        face.FaceType = type;
        face.ImageKind = ImageKind.Normal;
        return face;
    }

    /// <summary>A project with the given bundled style and <paramref name="cards"/> cards of different colours.</summary>
    public Project Project(string style, int cards, bool withBacks = false)
    {
        var settings = new ProjectSettings();
        AppError error = ProjectFileStore.LoadStyle(Path.Combine(AppPaths.DefaultStylesFolder, style), ref settings).Error;
        Assert.False(error.HasError, error.Message);
        settings.InputParameters.OutputPdf = File("out", "deck.pdf");

        var deck = new Deck();
        var list = new List<Card>();
        for (int i = 0; i < cards; i++)
        {
            var card = new Card { Copies = 1, Front = Face("front" + i, Colors[i % Colors.Length]) };
            if (withBacks)
            {
                card.Back = Face("back" + i, BackColor(i), FaceType.Back);
            }

            list.Add(card);
        }

        deck.Cards = [.. list];
        deck.RecountCopies();
        return new Project { Settings = settings, Deck = deck };
    }

    public void Dispose()
    {
        AppPaths.Initialize(_assets, _user);
        AppSettings.TempFolder = _temp;
        AppSettings.EmbedColorProfile = _embed;
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

/// <summary>An image drawn on a PDF page: its box in points, from the bottom-left corner of the page.</summary>
public sealed record PlacedImage(double X, double Y, double Width, double Height);

internal static class PdfInspection
{
    /// <summary>The images drawn on a page, in drawing order, with coordinates rounded to 0.01 points.</summary>
    public static List<PlacedImage> ImagesOn(string file, int page)
    {
        using var document = new PdfDocument(new PdfReader(file));
        var listener = new ImageListener();
        new PdfCanvasProcessor(listener).ProcessPageContent(document.GetPage(page));
        return listener.Images;
    }

    public static int PageCount(string file)
    {
        using var document = new PdfDocument(new PdfReader(file));
        return document.GetNumberOfPages();
    }

    public static (double Width, double Height) PageSize(string file, int page)
    {
        using var document = new PdfDocument(new PdfReader(file));
        Rectangle size = document.GetPage(page).GetPageSize();
        return (size.GetWidth(), size.GetHeight());
    }

    /// <summary>
    /// The colour of the page at a point given in PDF coordinates (points from the bottom-left),
    /// as "#RRGGBB". The page is rendered at one pixel per point.
    /// </summary>
    public static string ColorAt(string file, int page, double x, double y)
    {
        using SKBitmap bitmap = PDFtoImage.Conversion.ToImage(System.IO.File.ReadAllBytes(file), page - 1, options: new PDFtoImage.RenderOptions(Dpi: 72));
        SKColor color = bitmap.GetPixel((int)x, bitmap.Height - 1 - (int)y);
        return $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
    }

    /// <summary>Whether two "#RRGGBB" colours differ by at most <paramref name="tolerance"/> per channel.</summary>
    public static bool Close(string expected, string actual, int tolerance = 12)
    {
        for (int i = 1; i < 7; i += 2)
        {
            if (Math.Abs(Convert.ToInt32(expected.Substring(i, 2), 16) - Convert.ToInt32(actual.Substring(i, 2), 16)) > tolerance)
            {
                return false;
            }
        }

        return true;
    }

    private sealed class ImageListener : IEventListener
    {
        public List<PlacedImage> Images { get; } = [];

        public void EventOccurred(IEventData data, EventType type)
        {
            if (data is ImageRenderInfo info)
            {
                Matrix matrix = info.GetImageCtm();
                Images.Add(new PlacedImage(
                    Math.Round(matrix.Get(Matrix.I31), 2),
                    Math.Round(matrix.Get(Matrix.I32), 2),
                    Math.Round(matrix.Get(Matrix.I11), 2),
                    Math.Round(matrix.Get(Matrix.I22), 2)));
            }
        }

        public ICollection<EventType> GetSupportedEvents() => [EventType.RENDER_IMAGE];
    }
}
