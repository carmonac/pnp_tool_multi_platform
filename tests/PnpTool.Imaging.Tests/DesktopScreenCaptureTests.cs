using System.Runtime.InteropServices;
using ImageMagick;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;

namespace PnpTool.Imaging.Tests;

/// <summary>The capture of the screen with the programs of each system replaced by one that writes a known picture.</summary>
public sealed class DesktopScreenCaptureTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pnptool-capture-").FullName;
    private readonly List<string> _calls = [];

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>
    /// A desktop of 300 by 200, red with a blue square of 100 at (150, 50). A program that
    /// takes the area itself writes that part; one that does not writes it all.
    /// </summary>
    private int Run(string program, IReadOnlyList<string> arguments, ScreenArea area, bool takesArea)
    {
        _calls.Add(program + " " + string.Join(" ", arguments.Take(arguments.Count - 1)));
        using var desktop = new MagickImage(new MagickColor("#FF0000"), 300, 200) { Format = MagickFormat.Png };
        using var square = new MagickImage(new MagickColor("#0000FF"), 100, 100);
        desktop.Composite(square, 150, 50, CompositeOperator.Over);
        if (takesArea)
        {
            desktop.Crop(new MagickGeometry(area.X, area.Y, (uint)area.Width, (uint)area.Height));
            desktop.ResetPage();
        }

        desktop.Write(arguments[^1]);
        return 0;
    }

    private DesktopScreenCapture Linux(ScreenArea area, bool wayland, params string[] installed) => new(
        OSPlatform.Linux,
        wayland ? "wayland" : "x11",
        (program, arguments) => Run(program, arguments, area, takesArea: program is "import" or "scrot" or "maim" or "grim"),
        installed.Contains,
        null);

    private static string ColorAt(string file, int x, int y)
    {
        using var image = new MagickImage(file);
        IMagickColor<ushort> color = image.GetPixels().GetPixel(x, y).ToColor()!;
        return color.ToHexString()[..7].ToUpperInvariant();
    }

    [Fact]
    public void Mac_measures_the_area_in_window_units_and_saves_in_the_format_asked()
    {
        var area = new ScreenArea(300, 100, 200, 100, Scaling: 2.0);
        Assert.Equal(["-x", "-t", "png", "-R", "150,50,100,50", "out.png"], DesktopScreenCapture.MacArguments(area, "out.png"));
        Assert.Equal("0,0,133.333,50", DesktopScreenCapture.MacArguments(new ScreenArea(0, 0, 200, 75, 1.5), "f")[4]);

        var capture = new DesktopScreenCapture(OSPlatform.OSX, null, (program, arguments) => Run(program, arguments, new ScreenArea(150, 50, 100, 100), true), _ => true, () => true);
        string file = Path.Combine(_folder, "sub", "Capture_0.jpg");

        AppError error = capture.Capture(new ScreenArea(150, 50, 100, 100), file);

        Assert.False(error.HasError, error.Message);
        Assert.Equal(["screencapture -x -t png -R 150,50,100,100"], _calls);
        using var saved = new MagickImage(file);
        Assert.Equal(MagickFormat.Jpeg, saved.Format);
        Assert.Equal((100u, 100u), (saved.Width, saved.Height));
    }

    [Fact]
    public void Messages_are_in_spanish_for_spanish_and_catalan_and_in_english_for_the_rest()
    {
        System.Globalization.CultureInfo before = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("es");
            Assert.StartsWith("PNP Tool no tiene permiso", DesktopScreenCapture.MacPermissionMessage);
            Assert.StartsWith("No se puede capturar", DesktopScreenCapture.WaylandMessage);
            Assert.StartsWith("No se ha encontrado", DesktopScreenCapture.X11Message);
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ca");
            Assert.StartsWith("PNP Tool no tiene permiso", DesktopScreenCapture.MacPermissionMessage);
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de");
            Assert.StartsWith("PNP Tool is not allowed", DesktopScreenCapture.MacPermissionMessage);
            Assert.StartsWith("The screen cannot", DesktopScreenCapture.WaylandMessage);
            Assert.StartsWith("No program", DesktopScreenCapture.X11Message);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = before;
        }
    }

    [Fact]
    public void Mac_without_the_permission_takes_no_picture_and_says_why()
    {
        var capture = new DesktopScreenCapture(OSPlatform.OSX, null, (program, arguments) => Run(program, arguments, default, true), _ => true, () => false);
        string file = Path.Combine(_folder, "no.jpg");

        AppError error = capture.Capture(new ScreenArea(0, 0, 10, 10), file);

        Assert.True(error.HasError);
        Assert.Equal(DesktopScreenCapture.MacPermissionMessage, error.Message);
        Assert.Empty(_calls);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void Linux_uses_the_first_program_installed()
    {
        var area = new ScreenArea(150, 50, 100, 100);
        string file = Path.Combine(_folder, "x11.png");

        Assert.False(Linux(area, wayland: false, "maim", "scrot").Capture(area, file).HasError);

        Assert.Equal(["scrot -o -a 150,50,100,100"], _calls);
        Assert.Equal("#0000FF", ColorAt(file, 50, 50));
        using var saved = new MagickImage(file);
        Assert.Equal((100u, 100u), (saved.Width, saved.Height));
    }

    [Fact]
    public void A_program_that_captures_the_whole_desktop_has_the_area_cut_out_of_its_picture()
    {
        var area = new ScreenArea(140, 40, 60, 60);
        string file = Path.Combine(_folder, "whole.png");

        Assert.False(Linux(area, wayland: true, "gnome-screenshot").Capture(area, file).HasError);

        Assert.Equal(["gnome-screenshot -f"], _calls);
        using var saved = new MagickImage(file);
        Assert.Equal((60u, 60u), (saved.Width, saved.Height));
        Assert.Equal("#FF0000", ColorAt(file, 5, 5));          // (145, 45) of the desktop
        Assert.Equal("#0000FF", ColorAt(file, 30, 30));        // (170, 70)
    }

    [Fact]
    public void Wayland_prefers_grim_and_says_what_to_install_without_any()
    {
        var area = new ScreenArea(150, 50, 100, 100);
        Assert.False(Linux(area, wayland: true, "grim", "spectacle", "import").Capture(area, Path.Combine(_folder, "w.png")).HasError);
        Assert.Equal(["grim -g 150,50 100x100"], _calls);

        AppError none = Linux(area, wayland: true, "import", "scrot").Capture(area, Path.Combine(_folder, "n.png"));
        Assert.Equal(DesktopScreenCapture.WaylandMessage, none.Message);
        AppError noneX11 = Linux(area, wayland: false).Capture(area, Path.Combine(_folder, "n.png"));
        Assert.Equal(DesktopScreenCapture.X11Message, noneX11.Message);
    }

    [Fact]
    public void A_program_that_fails_gives_way_to_the_next()
    {
        var area = new ScreenArea(150, 50, 100, 100);
        var capture = new DesktopScreenCapture(
            OSPlatform.Linux,
            "x11",
            (program, arguments) => program == "import" ? 1 : Run(program, arguments, area, true),
            _ => true,
            null);

        Assert.False(capture.Capture(area, Path.Combine(_folder, "next.png")).HasError);
        Assert.Equal(["scrot -o -a 150,50,100,100"], _calls);

        Assert.True(capture.Capture(new ScreenArea(0, 0, 0, 10), Path.Combine(_folder, "empty.png")).HasError);
    }
}
