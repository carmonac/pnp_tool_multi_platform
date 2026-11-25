using System.Security.Cryptography;
using System.Text;
using ImageMagick;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Kernel.Pdf.Xobject;
using PnpTool.Core;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using Path = System.IO.Path;

namespace PnpTool.Pdf.Tools;

/// <summary>
/// Gets images out of PDF files: either the images the PDF contains, each once, or a picture
/// of every whole page. Original: <c>FormExtraerModelo</c>, <c>ImageRenderListener</c> and
/// <c>ListaImagenesExtraccion</c>.
/// </summary>
public sealed class PdfImageExtractor
{
    /// <summary>Folder the images are written to. Original: <c>RutaDestino</c>.</summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>Original: <c>NombresArchivos</c> and <c>CargaNombresArchivos</c>.</summary>
    public IReadOnlyList<string> Files { get; set; } = [];

    /// <summary>Empty the destination folder first. Original: <c>BorrarCarpetaDestino</c>.</summary>
    public bool ClearDestination { get; set; }

    /// <summary>Render each page as one image instead of taking the images it contains. Original: <c>ExtraerPaginaCompleta</c>.</summary>
    public bool WholePages { get; set; }

    /// <summary>Original: <c>InvertirColores</c>.</summary>
    public bool InvertColors { get; set; }

    /// <summary>The images written by the last file processed. Original: <c>ListadoImagenesLocal</c>.</summary>
    public IReadOnlyList<string> Extracted { get; private set; } = [];

    /// <summary>Receives the percentage done of the file being processed. Original: <c>ReportProgressDelegate</c>.</summary>
    public Action<int>? ReportProgress { get; set; }

    /// <summary>
    /// Processes every file. Whole pages are rendered at <paramref name="dpi"/>. Returns an
    /// error naming the files that do not allow their content to be changed or could not be
    /// rendered; such files are still attempted. Original: <c>Extraer</c>.
    /// </summary>
    public AppError Extract(int dpi)
    {
        Log.Enter(nameof(Extract));
        var error = new AppError();
        var problems = new StringBuilder();
        AppSettings.LastFolder = Destination;
        try
        {
            if (ClearDestination && Directory.Exists(Destination))
            {
                Directory.Delete(Destination, recursive: true);
            }

            Directory.CreateDirectory(Destination);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new AppError { Kind = ErrorKind.Error, Message = exception.Message };
        }

        foreach (string file in Files)
        {
            bool allowed;
            using (var reader = new PdfReader(file))
            using (var document = new PdfDocument(reader))
            {
                allowed = !reader.IsEncrypted() || PdfEncryptor.IsModifyContentsAllowed((int)reader.GetPermissions());
            }

            if (!allowed)
            {
                error.Kind = ErrorKind.Error;
                problems.AppendLine(file + " " + StringResources.Get("pdf_no_extraer"));
            }

            if (WholePages)
            {
                AppError pageError = RenderPages(file, dpi);
                if (pageError.HasError)
                {
                    error.Kind = ErrorKind.Error;
                    problems.AppendLine(pageError.Message);
                }
            }
            else
            {
                ExtractImages(file);
            }
        }

        if (error.HasError)
        {
            error.Message = problems.ToString();
        }

        Log.Exit(nameof(Extract));
        return error;
    }

    /// <summary>
    /// One PNG per page, named <c>&lt;file&gt;_&lt;page&gt;.png</c>. Original: <c>GeneraPaginasCompletas</c>,
    /// which needed Ghostscript; PDFium renders the pages here.
    /// </summary>
    private AppError RenderPages(string file, int dpi)
    {
        var error = new AppError();
        var written = new List<string>();
        ReportProgress?.Invoke(0);
        try
        {
            int pages = PdfRasterizer.PageCount(file);
            string name = Path.GetFileNameWithoutExtension(file);
            for (int page = 0; page < pages; page++)
            {
                using MagickImage image = PdfRasterizer.RenderPage(file, page, dpi);
                if (InvertColors)
                {
                    image.Negate();
                }

                string path = FileNames.Unique(Path.Combine(Destination, name + "_" + (page + 1) + ".png"));
                image.Write(path);
                written.Add(path);
                ReportProgress?.Invoke(Convert.ToInt32(Math.Round(page * 100.0 / pages)));
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            error.Kind = ErrorKind.Error;
            error.Message = file + ": " + exception.Message;
        }

        Extracted = written;
        return error;
    }

    /// <summary>
    /// The images inside the PDF, each written once as <c>Img_&lt;n&gt;.&lt;ext&gt;</c>, or
    /// <c>Img_&lt;n&gt;_X&lt;copies&gt;.&lt;ext&gt;</c> when the PDF shows it several times, which is
    /// the name the program reads a number of copies from. Original: <c>ExtraeImagenesSueltasItext</c>.
    /// </summary>
    /// <remarks>
    /// The images are written with the bytes the PDF holds. The original then made a copy with
    /// a higher stated resolution and, through an inverted check, threw it away; that step is
    /// left out.
    /// </remarks>
    private void ExtractImages(string file)
    {
        Log.Enter(nameof(ExtractImages));
        var listener = new ImageCollector();
        using (var document = new PdfDocument(new PdfReader(file)))
        {
            var processor = new PdfCanvasProcessor(listener);
            int pages = document.GetNumberOfPages();
            ReportProgress?.Invoke(0);
            for (int page = 1; page <= pages; page++)
            {
                processor.ProcessPageContent(document.GetPage(page));
                ReportProgress?.Invoke(Convert.ToInt32((float)page / pages * 100f));
            }
        }

        var written = new List<string>();
        for (int i = 0; i < listener.Images.Count; i++)
        {
            FoundImage image = listener.Images[i];
            string name = "Img_" + i + (image.Copies > 1 ? "_X" + image.Copies : string.Empty) + "." + image.Extension;
            string path = FileNames.Unique(Path.Combine(Destination, name));
            File.WriteAllBytes(path, image.Bytes);
            if (InvertColors)
            {
                using var inverted = new MagickImage(path);
                inverted.Negate();
                inverted.Write(path);
            }

            written.Add(path);
        }

        Extracted = written;
        Log.Exit(nameof(ExtractImages));
    }

    private sealed class FoundImage(string hash, string extension, byte[] bytes)
    {
        public string Hash { get; } = hash;

        public string Extension { get; } = extension;

        public byte[] Bytes { get; } = bytes;

        public int Copies { get; set; } = 1;
    }

    /// <summary>Collects the distinct images drawn on the pages and counts how often each is drawn.</summary>
    private sealed class ImageCollector : IEventListener
    {
        public List<FoundImage> Images { get; } = [];

        public void EventOccurred(IEventData data, EventType type)
        {
            if (data is not ImageRenderInfo info)
            {
                return;
            }

            try
            {
                PdfImageXObject? image = info.GetImage();
                if (image == null)
                {
                    return;
                }

                byte[] bytes = image.GetImageBytes();
                string hash = Convert.ToHexString(SHA256.HashData(bytes));
                FoundImage? known = Images.FirstOrDefault(found => found.Hash == hash);
                if (known != null)
                {
                    known.Copies++;
                }
                else
                {
                    Images.Add(new FoundImage(hash, image.IdentifyImageFileExtension(), bytes));
                }
            }
            catch (Exception exception) when (exception is iText.Kernel.Exceptions.PdfException or iText.IO.Exceptions.IOException or NotSupportedException)
            {
                // An image in a form the library cannot decode is skipped, as in the original.
            }
        }

        public ICollection<EventType> GetSupportedEvents() => [EventType.RENDER_IMAGE];
    }
}
