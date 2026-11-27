using Avalonia;

namespace PnpTool.App.Services;

/// <summary>The part of a screen that windows may use, in pixels of the desktop, and its scale.</summary>
public readonly record struct WorkArea(PixelRect WorkingArea, double Scaling);

/// <summary>
/// Decides where the main window opens from where it was when it was closed. The screens may
/// have changed in between: a monitor unplugged, another resolution, a laptop used on its own.
/// </summary>
/// <remarks>
/// Not in the original, which remembers the size of its window and whether it was maximised
/// but lets Windows place it.
/// </remarks>
public static class WindowPlacement
{
    /// <summary>Height of the strip at the top of a window that must be in sight, in device-independent units.</summary>
    public const double TitleBarHeight = 30.0;

    /// <summary>Width of that strip that must be in sight for the window to be reached and dragged.</summary>
    public const double ReachableWidth = 120.0;

    /// <summary>
    /// The screen that would show the title bar of a window of <paramref name="size"/> put at
    /// <paramref name="position"/>: all of its height and enough of its width to take hold of.
    /// Null when no screen does, and the window has to open somewhere else.
    /// </summary>
    public static WorkArea? ScreenFor(PixelPoint position, Size size, IReadOnlyList<WorkArea> screens)
    {
        WorkArea? best = null;
        int bestWidth = 0;
        foreach (WorkArea screen in screens)
        {
            var titleBar = new PixelRect(
                position.X,
                position.Y,
                (int)Math.Round(size.Width * screen.Scaling),
                (int)Math.Round(TitleBarHeight * screen.Scaling));
            PixelRect seen = titleBar.Intersect(screen.WorkingArea);
            if (seen.Height >= titleBar.Height
                && seen.Width >= Math.Min(ReachableWidth * screen.Scaling, titleBar.Width)
                && seen.Width > bestWidth)
            {
                best = screen;
                bestWidth = seen.Width;
            }
        }

        return best;
    }

    /// <summary>True when a window of <paramref name="size"/> is no larger than what the screen offers.</summary>
    public static bool Fits(Size size, WorkArea screen) =>
        size.Width <= screen.WorkingArea.Width / screen.Scaling
        && size.Height <= screen.WorkingArea.Height / screen.Scaling;
}
