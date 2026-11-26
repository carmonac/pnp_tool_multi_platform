using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace PnpTool.App.Views;

/// <summary>
/// An image, shrunk to fit, with the lines it will be cut by and the borders that are thrown
/// away in red. Original: <c>ControlCanvas</c>, which drew at the size of the image under a
/// zoom slider.
/// </summary>
public sealed class CutPreview : Control
{
    private Bitmap? _picture;
    private int _imageWidth;
    private int _imageHeight;
    private IReadOnlyList<Rect> _pieces = [];
    private Rect _kept;
    private Color _lineColor = Colors.HotPink;
    private double _thickness = 1.0;

    /// <summary>The pieces drawn, in pixels of the image.</summary>
    public IReadOnlyList<Rect> Pieces => _pieces;

    /// <summary>Shows an image of the given size in pixels with its pieces and what is kept of it.</summary>
    public void Show(Bitmap? picture, int imageWidth, int imageHeight, IReadOnlyList<Rect> pieces, Rect kept, Color lineColor, double thickness)
    {
        _picture = picture;
        _imageWidth = imageWidth;
        _imageHeight = imageHeight;
        _pieces = pieces;
        _kept = kept;
        _lineColor = lineColor;
        _thickness = thickness;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (_picture == null || _imageWidth <= 0 || _imageHeight <= 0)
        {
            return;
        }

        double scale = Math.Min(Bounds.Width / _imageWidth, Bounds.Height / _imageHeight);
        if (scale <= 0.0 || double.IsNaN(scale) || double.IsInfinity(scale))
        {
            return;
        }

        var origin = new Point((Bounds.Width - (_imageWidth * scale)) / 2.0, (Bounds.Height - (_imageHeight * scale)) / 2.0);
        Rect Scaled(Rect area) => new(origin.X + (area.X * scale), origin.Y + (area.Y * scale), area.Width * scale, area.Height * scale);
        Rect whole = Scaled(new Rect(0, 0, _imageWidth, _imageHeight));
        context.DrawImage(_picture, whole);

        var red = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));
        Rect kept = Scaled(_kept);
        context.DrawRectangle(red, null, new Rect(whole.X, whole.Y, Math.Max(0.0, kept.X - whole.X), whole.Height));
        context.DrawRectangle(red, null, new Rect(kept.Right, whole.Y, Math.Max(0.0, whole.Right - kept.Right), whole.Height));
        context.DrawRectangle(red, null, new Rect(kept.X, whole.Y, kept.Width, Math.Max(0.0, kept.Y - whole.Y)));
        context.DrawRectangle(red, null, new Rect(kept.X, kept.Bottom, kept.Width, Math.Max(0.0, whole.Bottom - kept.Bottom)));

        var pen = new Pen(new SolidColorBrush(_lineColor), _thickness);
        using (context.PushClip(whole))
        {
            foreach (Rect piece in _pieces)
            {
                context.DrawRectangle(null, pen, Scaled(piece));
            }
        }
    }
}
