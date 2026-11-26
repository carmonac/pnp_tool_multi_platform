using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PnpTool.App.Views;

/// <summary>
/// A small drawing of the sheet with its rows and columns of cards, upright or on its side.
/// Original: <c>Control_FolioPreview</c>.
/// </summary>
public sealed class SheetPreview : Control
{
    public static readonly StyledProperty<int> ColumnsProperty = AvaloniaProperty.Register<SheetPreview, int>(nameof(Columns), 1);
    public static readonly StyledProperty<int> RowsProperty = AvaloniaProperty.Register<SheetPreview, int>(nameof(Rows), 1);
    public static readonly StyledProperty<bool> LandscapeProperty = AvaloniaProperty.Register<SheetPreview, bool>(nameof(Landscape));
    public static readonly StyledProperty<bool> FitsProperty = AvaloniaProperty.Register<SheetPreview, bool>(nameof(Fits), true);

    /// <summary>More cells than this would only be a blur.</summary>
    private const int MostCells = 60;

    static SheetPreview()
    {
        AffectsRender<SheetPreview>(ColumnsProperty, RowsProperty, LandscapeProperty, FitsProperty);
    }

    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public int Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public bool Landscape
    {
        get => GetValue(LandscapeProperty);
        set => SetValue(LandscapeProperty, value);
    }

    /// <summary>Whether the cards fit the sheet: they are drawn green, or red.</summary>
    public bool Fits
    {
        get => GetValue(FitsProperty);
        set => SetValue(FitsProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        // The sheet keeps the proportion of an A4 whatever the paper: it is a sketch, as in the original.
        double longSide = Math.Min(Bounds.Width, Bounds.Height);
        double shortSide = longSide / Math.Sqrt(2.0);
        double width = Landscape ? longSide : shortSide;
        double height = Landscape ? shortSide : longSide;
        var sheet = new Rect((Bounds.Width - width) / 2.0, (Bounds.Height - height) / 2.0, width, height);
        context.DrawRectangle(Brushes.White, new Pen(Brushes.Gray, 1.0), sheet);

        int columns = Math.Clamp(Columns, 0, MostCells);
        int rows = Math.Clamp(Rows, 0, MostCells);
        if (columns == 0 || rows == 0)
        {
            return;
        }

        const double margin = 6.0;
        double cellWidth = (sheet.Width - (2.0 * margin)) / columns;
        double cellHeight = (sheet.Height - (2.0 * margin)) / rows;
        double gap = Math.Min(2.0, Math.Min(cellWidth, cellHeight) / 4.0);
        IBrush fill = Fits ? new SolidColorBrush(Color.FromRgb(0x8B, 0xC3, 0x4A)) : new SolidColorBrush(Color.FromRgb(0xE5, 0x73, 0x73));
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                context.DrawRectangle(fill, null, new Rect(
                    sheet.X + margin + (column * cellWidth) + (gap / 2.0),
                    sheet.Y + margin + (row * cellHeight) + (gap / 2.0),
                    Math.Max(1.0, cellWidth - gap),
                    Math.Max(1.0, cellHeight - gap)));
            }
        }
    }
}
