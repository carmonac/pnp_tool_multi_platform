using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Layout.Properties;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Projects;
using PnpTool.Pdf.Layout;
using Path = System.IO.Path;

namespace PnpTool.Pdf.Tests;

/// <summary>
/// Text drawn straight onto the page, in place of iText's layout. The places expected here
/// were measured on paragraphs laid out by iText (<c>SetFixedPosition(100, 200, width)</c>).
/// </summary>
[Collection(GlobalState.Name)]
public sealed class PdfTextLineTests : IDisposable
{
    private readonly Workspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    private sealed record Baseline(string Text, double X1, double Y1, double X2, double Y2);

    private sealed class Listener : IEventListener
    {
        public List<Baseline> Lines { get; } = [];

        public void EventOccurred(IEventData data, EventType type)
        {
            if (data is TextRenderInfo text)
            {
                Vector start = text.GetBaseline().GetStartPoint();
                Vector end = text.GetBaseline().GetEndPoint();
                Lines.Add(new Baseline(text.GetText(), start.Get(0), start.Get(1), end.Get(0), end.Get(1)));
            }
        }

        public ICollection<EventType> GetSupportedEvents() => [EventType.RENDER_TEXT];
    }

    private Baseline Draw(bool embedded, float size, string text, float width, TextAlignment alignment = TextAlignment.LEFT, bool upsideDown = false)
    {
        string file = _workspace.File("out", "text.pdf");
        using (var pdf = new PdfDocument(new PdfWriter(file)))
        {
            var canvas = new PdfCanvas(pdf.AddNewPage());
            PdfFont font = embedded
                ? PdfFontFactory.CreateFont(Path.Combine(AppPaths.FontsFolder, "LiberationSerif-Regular.ttf"), "Windows-1252", PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED)
                : PdfTextLine.DefaultFont();
            PdfTextLine.Draw(canvas, font, size, text, 100f, 200f, width, alignment, upsideDown);
        }

        using var read = new PdfDocument(new PdfReader(file));
        var listener = new Listener();
        new PdfCanvasProcessor(listener).ProcessPageContent(read.GetPage(1));
        return Assert.Single(listener.Lines);
    }

    // iText writes two decimals, so its own numbers are good to a hundredth of a point.
    private static void Near(double expected, double actual) => Assert.InRange(actual, expected - 0.011, expected + 0.011);

    [Theory]
    [InlineData(6f, TextAlignment.LEFT, 100.00, 202.46, 117.49)]
    [InlineData(8f, TextAlignment.LEFT, 100.00, 203.28, 123.32)]
    [InlineData(8f, TextAlignment.CENTER, 238.34, 203.28, 261.66)]
    [InlineData(8f, TextAlignment.RIGHT, 376.68, 203.28, 400.00)]
    [InlineData(12f, TextAlignment.LEFT, 100.00, 204.92, 134.98)]
    [InlineData(20f, TextAlignment.RIGHT, 341.70, 208.19, 400.00)]
    public void Footer_font_sits_where_the_layout_of_itext_puts_it(float size, TextAlignment alignment, double left, double baseline, double right)
    {
        Baseline line = Draw(embedded: true, size, "Abc 12", 300f, alignment);

        Assert.Equal("Abc 12", line.Text);
        Near(left, line.X1);
        Near(baseline, line.Y1);
        Near(right, line.X2);
        Near(baseline, line.Y2);
    }

    [Theory]
    [InlineData("7", 107.784)]
    [InlineData("10", 115.568)]
    public void Standard_font_sits_where_the_layout_of_itext_puts_it(string text, double right)
    {
        Baseline line = Draw(embedded: false, 14f, text, 28f);

        Near(100.0, line.X1);
        Near(206.20, line.Y1);
        Near(right, line.X2);
    }

    [Theory]
    [InlineData(14f, "7", 185.80, 92.216)]
    [InlineData(14f, "10", 185.80, 84.432)]
    [InlineData(14f, "K", 185.80, 90.662)]
    [InlineData(10f, "Q", 187.58, 92.220)]
    public void Text_turned_upside_down_runs_to_the_left_from_below_the_paragraph(float size, string text, double baseline, double end)
    {
        Baseline line = Draw(embedded: false, size, text, size * 2f, upsideDown: true);

        Near(100.0, line.X1);
        Near(baseline, line.Y1);
        Near(end, line.X2);
        Near(baseline, line.Y2);
    }

    [Fact]
    public void Empty_text_draws_nothing()
    {
        string file = _workspace.File("out", "empty.pdf");
        using (var pdf = new PdfDocument(new PdfWriter(file)))
        {
            PdfTextLine.Draw(new PdfCanvas(pdf.AddNewPage()), PdfTextLine.DefaultFont(), 8f, string.Empty, 10f, 10f, 100f);
        }

        Assert.Equal(string.Empty, PdfInspection.TextOn(file, 1).Trim());
    }

    /// <summary>
    /// With iText's layout, text failed after some thirty paragraphs once .NET had optimised
    /// the code: a long project lost its numbers, and later ones could not be made at all.
    /// </summary>
    [Fact]
    public void Every_sheet_of_a_long_project_gets_its_number()
    {
        Project project = _workspace.Project("Cartas_Standar.pnps", cards: 9 * 120);
        project.Settings.HeaderFooter.ShowPageNumber = true;
        project.Settings.HeaderFooter.FirstPageNumber = 1;
        project.Settings.HeaderFooter.SignatureText = "PNP";

        string file = PdfGenerator.Generate(project, _workspace.WorkFolder).Files[0];

        Assert.Equal(120, PdfInspection.PageCount(file));
        Assert.Contains("1", PdfInspection.TextOn(file, 1));
        Assert.Contains("60", PdfInspection.TextOn(file, 60));
        Assert.Contains("120", PdfInspection.TextOn(file, 120));
        Assert.Contains("PNP", PdfInspection.TextOn(file, 120));
    }
}
