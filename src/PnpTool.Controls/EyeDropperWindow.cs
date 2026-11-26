using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PnpTool.Core.Primitives;

namespace PnpTool.Controls;

/// <summary>
/// A picture of the screen shown over the whole screen, to pick the colour of one of its
/// points: a click chooses it and Escape cancels. A small panel follows the pointer with the
/// points around it enlarged and the colour under it. Closes with the colour, or with nothing.
/// Original: <c>Dsafa.WpfColorPicker.EyeDropperWindow</c> and <c>EyeDropperWindowAdorner</c>.
/// </summary>
public sealed class EyeDropperWindow : Window
{
    /// <summary>Points of the picture shown enlarged at each side of the pointer.</summary>
    private const int Around = 5;

    private const double LensSize = 110.0;

    private readonly WriteableBitmap _picture;
    private readonly Image _lens;
    private readonly Border _swatch;
    private readonly Border _panel;
    private readonly Canvas _overlay;

    /// <param name="pictureFile">The picture of the screen.</param>
    /// <param name="position">Where the screen it shows is, to cover it; null leaves the window where the system puts it.</param>
    public EyeDropperWindow(string pictureFile, PixelPoint? position = null)
    {
        using (FileStream stream = File.OpenRead(pictureFile))
        {
            _picture = WriteableBitmap.Decode(stream);
        }

        WindowDecorations = WindowDecorations.None;
        ShowInTaskbar = false;
        Topmost = true;
        CanResize = false;
        Cursor = new Cursor(StandardCursorType.Cross);
        if (position != null)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = position.Value;
        }

        WindowState = WindowState.FullScreen;

        _lens = new Image { Width = LensSize, Height = LensSize, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapInterpolationMode(_lens, BitmapInterpolationMode.None);
        _swatch = new Border { Height = 22.0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 1, 0, 0) };
        _panel = new Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1.0),
            Background = Brushes.White,
            IsVisible = false,
            IsHitTestVisible = false,
            Child = new StackPanel { Children = { _lens, _swatch } },
        };
        _overlay = new Canvas { Children = { _panel } };
        Content = new Panel
        {
            Children =
            {
                new Image { Source = _picture, Stretch = Stretch.Fill, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch },
                _overlay,
            },
        };
        Closed += (_, _) => _picture.Dispose();
    }

    /// <summary>The colour under the pointer when it was last moved.</summary>
    public ArgbColor Current { get; private set; } = ArgbColor.White;

    /// <summary>The point of the picture under a point of the window.</summary>
    public PixelPoint PictureAt(Point point)
    {
        PixelSize size = _picture.PixelSize;
        double width = Bounds.Width <= 0.0 ? size.Width : Bounds.Width;
        double height = Bounds.Height <= 0.0 ? size.Height : Bounds.Height;
        return new PixelPoint(
            Math.Clamp((int)(point.X * size.Width / width), 0, size.Width - 1),
            Math.Clamp((int)(point.Y * size.Height / height), 0, size.Height - 1));
    }

    /// <summary>The colour of the picture under a point of the window. Original: <c>GetColorAtPoint</c>.</summary>
    public ArgbColor ColorAt(Point point)
    {
        PixelPoint pixel = PictureAt(point);
        using ILockedFramebuffer frame = _picture.Lock();
        int offset = (pixel.Y * frame.RowBytes) + (pixel.X * 4);
        byte first = Marshal.ReadByte(frame.Address, offset);
        byte green = Marshal.ReadByte(frame.Address, offset + 1);
        byte third = Marshal.ReadByte(frame.Address, offset + 2);
        return frame.Format == PixelFormat.Rgba8888 ? new ArgbColor(first, green, third) : new ArgbColor(third, green, first);
    }

    /// <summary>Follows the pointer: shows what is under it. Original: <c>OnMouseMove</c>.</summary>
    public void MoveTo(Point point)
    {
        Current = ColorAt(point);
        PixelPoint pixel = PictureAt(point);
        PixelSize size = _picture.PixelSize;
        int left = Math.Clamp(pixel.X - Around, 0, Math.Max(0, size.Width - ((2 * Around) + 1)));
        int top = Math.Clamp(pixel.Y - Around, 0, Math.Max(0, size.Height - ((2 * Around) + 1)));
        _lens.Source = new CroppedBitmap(_picture, new PixelRect(left, top, Math.Min((2 * Around) + 1, size.Width), Math.Min((2 * Around) + 1, size.Height)));
        _swatch.Background = Current.ToBrush();

        // Beside the pointer, on the side that has room.
        double x = point.X + 20.0 + LensSize + 2.0 > Bounds.Width ? point.X - 22.0 - LensSize : point.X + 20.0;
        double y = point.Y + 20.0 + LensSize + 24.0 > Bounds.Height ? point.Y - 44.0 - LensSize : point.Y + 20.0;
        Canvas.SetLeft(_panel, Math.Max(0.0, x));
        Canvas.SetTop(_panel, Math.Max(0.0, y));
        _panel.IsVisible = true;
    }

    /// <summary>Chooses the colour under a point and closes. Original: <c>WindowOnMouseDown</c>.</summary>
    public void Pick(Point point) => Close((ArgbColor?)ColorAt(point));

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        MoveTo(e.GetPosition(this));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            Pick(e.GetPosition(this));
        }
        else
        {
            Close((ArgbColor?)null);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Close((ArgbColor?)null);
        }
    }
}
