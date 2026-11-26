using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PnpTool.App.Views;

/// <summary>A card of a template as the sketch draws it: its place and size in millimetres.</summary>
public sealed record TemplatePreviewCard(Rect Area, Color Color, bool Selected = false, bool Fits = true);

/// <summary>
/// A sketch, to scale, of the sheet of an advanced template with the places of its cards.
/// Original: <c>ControlCanvasPlantillaAvanzada</c>, a canvas of 210 by 297 whatever the paper.
/// </summary>
public sealed class TemplatePreview : Control
{
    private double _paperWidth = 210.0;
    private double _paperHeight = 297.0;

    /// <summary>The cards drawn, in order.</summary>
    public IReadOnlyList<TemplatePreviewCard> Cards { get; private set; } = [];

    /// <summary>Draws a sheet of the given size, in millimetres, with its cards.</summary>
    public void Show(double paperWidth, double paperHeight, IReadOnlyList<TemplatePreviewCard> cards)
    {
        _paperWidth = paperWidth > 0.0 ? paperWidth : 210.0;
        _paperHeight = paperHeight > 0.0 ? paperHeight : 297.0;
        Cards = cards;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        double scale = Math.Min(Bounds.Width / _paperWidth, Bounds.Height / _paperHeight);
        if (scale <= 0.0 || double.IsNaN(scale) || double.IsInfinity(scale))
        {
            return;
        }

        var sheet = new Rect(
            (Bounds.Width - (_paperWidth * scale)) / 2.0,
            (Bounds.Height - (_paperHeight * scale)) / 2.0,
            _paperWidth * scale,
            _paperHeight * scale);
        context.DrawRectangle(Brushes.Gray, new Pen(Brushes.Black, 1.0), sheet);
        using (context.PushClip(sheet))
        {
            foreach (TemplatePreviewCard card in Cards)
            {
                var area = new Rect(
                    sheet.X + (card.Area.X * scale),
                    sheet.Y + (card.Area.Y * scale),
                    card.Area.Width * scale,
                    card.Area.Height * scale);
                IPen? outline = card.Selected ? new Pen(Brushes.White, 2.0) : card.Fits ? null : new Pen(Brushes.Red, 2.0);
                context.DrawRectangle(new SolidColorBrush(card.Color), outline, area);
            }
        }
    }
}
