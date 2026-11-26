using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PnpTool.Core.Projects;

namespace PnpTool.App.Views;

/// <summary>
/// The poster over its sheets, shrunk to fit: the picture, the printed area of every sheet
/// and, darker, the strips that two sheets repeat.
/// </summary>
public sealed class PosterPreview : Control
{
    private Bitmap? _picture;
    private PosterPlan? _plan;

    /// <summary>The plan drawn.</summary>
    public PosterPlan? Plan => _plan;

    public void Show(Bitmap? picture, PosterPlan? plan)
    {
        _picture = picture;
        _plan = plan;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (_plan == null)
        {
            return;
        }

        PosterPlan plan = _plan;
        double scale = Math.Min(Bounds.Width / plan.CoveredWidth, Bounds.Height / plan.CoveredHeight);
        if (scale <= 0.0 || double.IsNaN(scale) || double.IsInfinity(scale))
        {
            return;
        }

        var origin = new Point((Bounds.Width - (plan.CoveredWidth * scale)) / 2.0, (Bounds.Height - (plan.CoveredHeight * scale)) / 2.0);
        Rect Scaled(double x, double y, double width, double height) => new(origin.X + (x * scale), origin.Y + (y * scale), width * scale, height * scale);

        Rect covered = Scaled(0, 0, plan.CoveredWidth, plan.CoveredHeight);
        context.DrawRectangle(Brushes.White, null, covered);
        if (_picture != null)
        {
            context.DrawImage(_picture, Scaled(plan.OffsetX, plan.OffsetY, plan.PosterWidth, plan.PosterHeight));
        }

        double stepX = plan.PrintableWidth - plan.Overlap;
        double stepY = plan.PrintableHeight - plan.Overlap;
        var strip = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0));
        for (int column = 1; column < plan.Columns && plan.Overlap > 0.0; column++)
        {
            context.DrawRectangle(strip, null, Scaled(column * stepX, 0, plan.Overlap, plan.CoveredHeight));
        }

        for (int row = 1; row < plan.Rows && plan.Overlap > 0.0; row++)
        {
            context.DrawRectangle(strip, null, Scaled(0, row * stepY, plan.CoveredWidth, plan.Overlap));
        }

        var pen = new Pen(Brushes.DeepPink, 1.0);
        foreach (PosterSheet sheet in plan.Sheets)
        {
            context.DrawRectangle(null, pen, Scaled(sheet.Column * stepX, sheet.Row * stepY, plan.PrintableWidth, plan.PrintableHeight));
        }
    }
}
