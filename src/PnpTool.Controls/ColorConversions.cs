using Avalonia.Media;
using PnpTool.Core.Primitives;

namespace PnpTool.Controls;

/// <summary>Between the colours of the program (<see cref="ArgbColor"/>, stored as "A_R_G_B") and those of the user interface.</summary>
public static class ColorConversions
{
    public static Color ToAvalonia(this ArgbColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    public static ArgbColor ToArgb(this Color color) => new(color.A, color.R, color.G, color.B);

    public static IBrush ToBrush(this ArgbColor color) => new SolidColorBrush(color.ToAvalonia());
}
