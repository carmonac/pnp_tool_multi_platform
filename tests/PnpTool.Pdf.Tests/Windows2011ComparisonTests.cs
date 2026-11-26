using System.Globalization;
using SkiaSharp;
using PnpTool.Core;
using PnpTool.Core.Files;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Projects;
using PnpTool.Pdf.Layout;

namespace PnpTool.Pdf.Tests;

/// <summary>
/// Opens the projects saved by PNP Tool 20.11.1 for Windows, makes their PDFs with this
/// program and compares them, sheet by sheet, with the PDFs Windows made from the same
/// projects. The projects and PDFs are in <c>samples/referencia-windows/salida</c>, which is
/// not part of the repository; without that folder the tests are skipped.
/// </summary>
[Collection(GlobalState.Name)]
public sealed class Windows2011ComparisonTests : IDisposable
{
    private readonly Workspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    /// <summary>The folder with the test images and, under "salida", the Windows projects and PDFs; null when absent.</summary>
    internal static string? ReferenceFolder()
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable("PNPTOOL_WINDOWS_REFERENCE");
        if (!string.IsNullOrEmpty(fromEnvironment))
        {
            return Directory.Exists(fromEnvironment) ? fromEnvironment : null;
        }

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

    /// <summary>
    /// Opens a Windows project and makes its PDF. The projects name their images relative to
    /// the folder they were saved in, so a copy of the images is made and the project is put
    /// back in that folder.
    /// </summary>
    internal string Generate(string reference, string name, string savedIn)
    {
        string images = _workspace.File("ref");
        foreach (string folder in Directory.GetDirectories(reference).Where(folder => Path.GetFileName(folder) != "salida"))
        {
            string target = Directory.CreateDirectory(Path.Combine(images, Path.GetFileName(folder))).FullName;
            foreach (string file in Directory.GetFiles(folder, "*.png"))
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
            }
        }

        string projectFile = Path.Combine(images, savedIn, name + ".pnp");
        File.Copy(Path.Combine(reference, "salida", name + ".pnp"), projectFile, overwrite: true);
        (AppError error, Project project) = ProjectFileStore.LoadProject(projectFile);
        Assert.False(error.HasError, error.Message);
        project.Settings.InputParameters.OutputPdf = _workspace.File("out", name + ".pdf");
        PdfOutput output = PdfGenerator.Generate(PdfGenerator.ForOutput(project), _workspace.WorkFolder);
        Assert.True(output.SingleFile, "More than one file: " + string.Join(", ", output.Files));
        return output.Files[0];
    }

    private static string F(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>What is on every sheet of a PDF, as text that can be compared line by line.</summary>
    internal static List<string> Describe(string file)
    {
        var lines = new List<string>();
        int pages = PdfInspection.PageCount(file);
        lines.Add("pages " + pages);
        for (int page = 1; page <= pages; page++)
        {
            (double width, double height) = PdfInspection.PageSize(file, page);
            lines.Add($"p{page} size {F(width)} x {F(height)}");
            foreach (PlacedImage image in PdfInspection.ImagesOn(file, page).OrderBy(i => i.Y).ThenBy(i => i.X))
            {
                lines.Add($"p{page} image {F(image.X)} {F(image.Y)} {F(image.Width)} {F(image.Height)}");
            }

            foreach (DrawnPath path in PdfInspection.PathsOn(file, page).OrderBy(p => p.Y1).ThenBy(p => p.X1).ThenBy(p => p.Y2).ThenBy(p => p.X2))
            {
                lines.Add($"p{page} {(path.IsCurve ? "curve" : "line")} {F(path.X1)} {F(path.Y1)} {F(path.X2)} {F(path.Y2)} w{F(path.LineWidth)}");
            }

            lines.Add($"p{page} text " + PdfInspection.TextOn(file, page).Replace("\n", " | "));
        }

        return lines;
    }

    /// <summary>
    /// The share of pixels, from 0 to 1, that differ visibly between the same page of two
    /// PDFs drawn at 72 dots per inch; 1 when the pages are not the same size.
    /// </summary>
    internal static double PixelDifference(string first, string second, int page)
    {
        using SKBitmap a = PDFtoImage.Conversion.ToImage(File.ReadAllBytes(first), page - 1, options: new PDFtoImage.RenderOptions(Dpi: 72));
        using SKBitmap b = PDFtoImage.Conversion.ToImage(File.ReadAllBytes(second), page - 1, options: new PDFtoImage.RenderOptions(Dpi: 72));
        if (a.Width != b.Width || a.Height != b.Height)
        {
            return 1.0;
        }

        int different = 0;
        for (int y = 0; y < a.Height; y++)
        {
            for (int x = 0; x < a.Width; x++)
            {
                SKColor one = a.GetPixel(x, y);
                SKColor other = b.GetPixel(x, y);
                if (Math.Abs(one.Red - other.Red) > 24 || Math.Abs(one.Green - other.Green) > 24 || Math.Abs(one.Blue - other.Blue) > 24)
                {
                    different++;
                }
            }
        }

        return (double)different / (a.Width * a.Height);
    }

    /// <summary>
    /// Sizes of the sheets, places and sizes of the images, every line drawn and the text are
    /// the same as in the Windows PDF to a hundredth of a point, and the sheets look the same
    /// when drawn: the same image, the same way up, in every place.
    /// </summary>
    [Theory]
    [InlineData("1-estandar", "cartas")]     // style "Estandar 63,5x88,9": twelve cards, one side
    [InlineData("2-doble-cara", "cartas")]   // double-sided with a back per card, 3 x 3
    [InlineData("3-gutterfold", "cartas")]   // style "Gutterfold 63.5x88.9": fronts over backs, to fold
    [InlineData("4-tokens", "tokens")]       // style "A4 25x25 8x11"
    [InlineData("5-libreto", "libro")]      // style "Libreto en A4": eight pages as a booklet
    [InlineData("6-plantilla", "cartas")]   // advanced template chosen in Windows: saved as a "card" project without sizes, blank sheets
    [InlineData("7-plantilla-tipo", "cartas")]  // the same with the kind of project of a template: six places a sheet, some turned
    public void Pdf_is_the_same_as_the_one_windows_made(string name, string savedIn)
    {
        string? reference = ReferenceFolder();
        Assert.SkipWhen(reference == null, "The Windows reference files are not on this machine.");
        string windows = Path.Combine(reference!, "salida", name + ".pdf");

        string ours = Generate(reference!, name, savedIn);

        string? report = Environment.GetEnvironmentVariable("PNPTOOL_COMPARISON_REPORT");
        if (!string.IsNullOrEmpty(report))
        {
            Directory.CreateDirectory(report);
            File.WriteAllLines(Path.Combine(report, name + ".windows.txt"), Describe(windows));
            File.WriteAllLines(Path.Combine(report, name + ".port.txt"), Describe(ours));
            File.Copy(ours, Path.Combine(report, name + ".port.pdf"), overwrite: true);
        }

        Assert.Equal(Describe(windows), Describe(ours));
        int pages = PdfInspection.PageCount(windows);
        var differences = new List<string>();
        for (int page = 1; page <= pages; page++)
        {
            double difference = PixelDifference(windows, ours, page);
            differences.Add(FormattableString.Invariant($"p{page} {difference:P2}"));
            Assert.True(difference < 0.005, $"Sheet {page} of {name} differs in {difference:P2} of its pixels");
        }

        if (!string.IsNullOrEmpty(report))
        {
            File.WriteAllLines(Path.Combine(report, name + ".pixels.txt"), differences);
        }
    }

    /// <summary>
    /// The bundled gutterfold style stores its paper already wider than tall and also marks
    /// it as landscape, so Windows turns it upright and the four columns do not fit: the
    /// first and last cards are cut by the edges of the sheet. This program does the same.
    /// </summary>
    [Fact]
    public void Gutterfold_style_of_windows_puts_cards_off_the_sheet()
    {
        string? reference = ReferenceFolder();
        Assert.SkipWhen(reference == null, "The Windows reference files are not on this machine.");
        string windows = Path.Combine(reference!, "salida", "3-gutterfold.pdf");

        (double width, double height) = PdfInspection.PageSize(windows, 1);
        List<PlacedImage> images = PdfInspection.ImagesOn(windows, 1);

        Assert.True(width < height);
        Assert.Contains(images, image => image.X < 0);
        Assert.Contains(images, image => image.X + image.Width > width);
    }
}
