using ImageMagick;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Xobject;
using PnpTool.Core;
using PnpTool.Core.Primitives;
using PnpTool.Core.Projects;
using PnpTool.Imaging;
using PnpTool.Pdf.Layout;

namespace PnpTool.Pdf.Tools;

/// <summary>
/// Writes a poster as a PDF with one page per sheet. Replaces the program PosteRazor, which
/// the Windows version starts from its tools tab.
/// </summary>
public static class PosterWriter
{
    /// <summary>Length of the marks at the corners of the printed area, at most.</summary>
    private static readonly double MarkLength = Unit.FromMillimeter(5.0).Point;

    /// <summary>The size an image has at its own resolution, and the plan of the poster made from it.</summary>
    public static (AppError Error, PosterPlan? Plan) Plan(PosterSettings settings)
    {
        AppError error = PosterLayout.Check(settings);
        if (error.HasError)
        {
            return (error, null);
        }

        if (!File.Exists(settings.ImagePath))
        {
            error.Kind = ErrorKind.MissingData;
            error.Message = "No image is selected";
            return (error, null);
        }

        try
        {
            return PosterLayout.Plan(settings, ImageDensity.PrintedSize(new MagickImageInfo(settings.ImagePath)));
        }
        catch (MagickException exception)
        {
            error.Kind = ErrorKind.Error;
            error.Message = exception.Message;
            return (error, null);
        }
    }

    /// <summary>
    /// Writes the PDF. The picture is stored once and drawn on every page, moved so that the
    /// page shows its part and cut at the printed area of the sheet.
    /// </summary>
    /// <param name="progress">Told the percentage done.</param>
    public static AppError Write(string file, PosterSettings settings, Action<int>? progress = null)
    {
        progress?.Invoke(0);
        (AppError error, PosterPlan? plan) = Plan(settings);
        if (plan == null)
        {
            return error;
        }

        var image = new MagickImage(settings.ImagePath);
        image.ResetPage();
        (ImageData data, AppError imageError) = PdfLayoutWriter.ToImageData(image, pdfX: false);
        if (imageError.HasError)
        {
            return imageError;
        }

        using var writer = new PdfWriter(file);
        using var pdf = new PdfDocument(writer);
        pdf.GetDocumentInfo().SetAuthor(AppInfo.Name);
        var picture = new PdfImageXObject(data);
        var size = new PageSize((float)plan.PaperWidth, (float)plan.PaperHeight);
        float margin = (float)plan.Margin;
        var printable = new Rectangle(margin, margin, (float)plan.PrintableWidth, (float)plan.PrintableHeight);

        for (int index = 0; index < plan.Sheets.Count; index++)
        {
            PosterSheet sheet = plan.Sheets[index];
            var canvas = new PdfCanvas(pdf.AddNewPage(size));
            canvas.SaveState();
            canvas.Rectangle(printable);
            canvas.Clip();
            canvas.EndPath();

            // PDF measures from the bottom of the page; the plan from the top.
            float bottom = (float)(plan.PaperHeight - sheet.ImageTop - plan.PosterHeight);
            canvas.AddXObjectFittedIntoRectangle(picture, new Rectangle((float)sheet.ImageLeft, bottom, (float)plan.PosterWidth, (float)plan.PosterHeight));
            canvas.RestoreState();

            if (settings.CutMarks && plan.Margin > 0.0)
            {
                DrawMarks(canvas, plan);
            }

            canvas.Release();
            progress?.Invoke((index + 1) * 100 / plan.Sheets.Count);
        }

        return error;
    }

    /// <summary>Two short lines in the margin at each corner of the printed area, pointing away from it.</summary>
    private static void DrawMarks(PdfCanvas canvas, PosterPlan plan)
    {
        double length = Math.Min(MarkLength, plan.Margin * 0.8);
        double left = plan.Margin;
        double right = plan.PaperWidth - plan.Margin;
        double bottom = plan.Margin;
        double top = plan.PaperHeight - plan.Margin;

        canvas.SaveState();
        canvas.SetLineWidth(0.25f);
        canvas.SetStrokeColor(ColorConstants.BLACK);
        foreach (double x in new[] { left, right })
        {
            canvas.MoveTo(x, bottom).LineTo(x, bottom - length);
            canvas.MoveTo(x, top).LineTo(x, top + length);
        }

        foreach (double y in new[] { bottom, top })
        {
            canvas.MoveTo(left, y).LineTo(left - length, y);
            canvas.MoveTo(right, y).LineTo(right + length, y);
        }

        canvas.Stroke();
        canvas.RestoreState();
    }
}
