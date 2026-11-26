namespace PnpTool.Core.Infrastructure;

/// <summary>
/// A rectangle of the desktop in pixels of the screen, measured from the top-left corner of
/// the main screen, with the scale between those pixels and the units the system places
/// windows in (2 on a high-density Mac screen).
/// </summary>
public readonly record struct ScreenArea(int X, int Y, int Width, int Height, double Scaling = 1.0);

/// <summary>
/// Takes pictures of the screen. Original: <c>Graphics.CopyFromScreen</c>, in AutoCaptura
/// and in the eyedropper of the colour window.
/// </summary>
public interface IScreenCapture
{
    /// <summary>
    /// Writes a picture of an area of the screen to a file, in the format its extension
    /// names. Gives an error with the reason when the picture cannot be taken.
    /// </summary>
    AppError Capture(ScreenArea area, string file);
}

/// <summary>Holds the screen capture in use; the imaging library registers the real one at start-up.</summary>
public static class ScreenCapture
{
    public static IScreenCapture Current { get; set; } = new NoScreenCapture();

    private sealed class NoScreenCapture : IScreenCapture
    {
        public AppError Capture(ScreenArea area, string file) =>
            new() { Kind = ErrorKind.Error, Message = "Screen capture is not available" };
    }
}
