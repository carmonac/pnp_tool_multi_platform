using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using ImageMagick;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;

namespace PnpTool.Imaging;

/// <summary>
/// Screen capture through the tools each system has: <c>screencapture</c> on macOS and, on
/// Linux, the first found of the usual capture programs. Original:
/// <c>Graphics.CopyFromScreen</c>, which exists on Windows only.
/// </summary>
/// <remarks>
/// On macOS the system asks the user to allow screen recording for the program the first
/// time; until it is allowed, pictures show the desktop without the windows of other
/// programs, so the capture is refused with a message instead. On Linux under Wayland a
/// program cannot read the screen itself: <c>grim</c> (wlroots), <c>gnome-screenshot</c> or
/// <c>spectacle</c> must be installed, and the desktop may ask for confirmation.
/// </remarks>
public sealed class DesktopScreenCapture : IScreenCapture
{
    /// <summary>Runs a program and gives its exit code; -1 when it cannot be started.</summary>
    public delegate int ProgramRunner(string program, IReadOnlyList<string> arguments);

    private readonly ProgramRunner _run;
    private readonly Func<string, bool> _hasProgram;
    private readonly Func<bool> _macAllowed;

    public DesktopScreenCapture()
        : this(null, null, null, null, null)
    {
    }

    /// <summary>For tests: the system, the kind of Linux session and the programs are given.</summary>
    public DesktopScreenCapture(OSPlatform? system, string? sessionType, ProgramRunner? run, Func<string, bool>? hasProgram, Func<bool>? macAllowed)
    {
        Platform = system ?? (OperatingSystem.IsMacOS() ? OSPlatform.OSX : OperatingSystem.IsLinux() ? OSPlatform.Linux : OSPlatform.Windows);
        Wayland = string.Equals(sessionType ?? Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase);
        _run = run ?? Run;
        _hasProgram = hasProgram ?? OnPath;
        _macAllowed = macAllowed ?? MacScreenRecordingAllowed;
    }

    public OSPlatform Platform { get; }

    public bool Wayland { get; }

    /// <summary>Makes this the screen capture of the program.</summary>
    public static void Register() => ScreenCapture.Current = new DesktopScreenCapture();

    public const string MacPermissionMessage =
        "PNP Tool is not allowed to record the screen. Allow it in System Settings > Privacy & Security > Screen Recording and start the program again.";

    public const string WaylandMessage =
        "The screen cannot be captured in this Wayland session: install grim, gnome-screenshot or spectacle.";

    public const string X11Message =
        "No program to capture the screen was found: install ImageMagick (import), scrot, maim, gnome-screenshot or spectacle.";

    public AppError Capture(ScreenArea area, string file)
    {
        if (area.Width <= 0 || area.Height <= 0)
        {
            return Fail("The area to capture has no size");
        }

        string raw = Path.Combine(Path.GetTempPath(), "pnptool-capture-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            AppError error = Platform == OSPlatform.OSX ? CaptureMac(area, raw)
                : Platform == OSPlatform.Linux ? CaptureLinux(area, raw)
                : Fail("Screen capture is not available on this system");
            if (error.HasError)
            {
                return error;
            }

            using var image = new MagickImage(raw);
            image.ResetPage();
            if (_cropAfter)
            {
                image.Crop(new MagickGeometry(area.X, area.Y, (uint)area.Width, (uint)area.Height));
                image.ResetPage();
            }

            image.Alpha(AlphaAction.Remove);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
            image.Write(file);
            return new AppError();
        }
        catch (Exception exception) when (exception is MagickException or IOException or UnauthorizedAccessException)
        {
            return Fail(exception.Message);
        }
        finally
        {
            _cropAfter = false;
            try
            {
                File.Delete(raw);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>Set by a tool that can only capture the whole desktop: the area is cut out afterwards.</summary>
    private bool _cropAfter;

    /// <summary>The arguments for <c>screencapture</c>, which measures in the units windows are placed in.</summary>
    public static IReadOnlyList<string> MacArguments(ScreenArea area, string file)
    {
        double scale = area.Scaling <= 0.0 ? 1.0 : area.Scaling;
        string region = string.Join(",", new[] { area.X, area.Y, area.Width, area.Height }
            .Select(value => (value / scale).ToString("0.###", CultureInfo.InvariantCulture)));
        return ["-x", "-t", "png", "-R", region, file];
    }

    private AppError CaptureMac(ScreenArea area, string file)
    {
        if (!_macAllowed())
        {
            return Fail(MacPermissionMessage);
        }

        int code = _run("screencapture", MacArguments(area, file));
        return code == 0 && File.Exists(file) ? new AppError() : Fail("screencapture failed (" + code + ")");
    }

    /// <summary>The capture programs to try on Linux, in order, with whether each takes the area itself.</summary>
    public static IReadOnlyList<(string Program, IReadOnlyList<string> Arguments, bool TakesArea)> LinuxTools(ScreenArea area, string file, bool wayland)
    {
        string geometry = area.Width + "x" + area.Height + "+" + area.X + "+" + area.Y;
        var whole = new (string, IReadOnlyList<string>, bool)[]
        {
            ("gnome-screenshot", ["-f", file], false),
            ("spectacle", ["-b", "-n", "-f", "-o", file], false),
        };
        return wayland
            ? [("grim", ["-g", area.X + "," + area.Y + " " + area.Width + "x" + area.Height, file], true), .. whole]
            :
            [
                ("import", ["-silent", "-window", "root", "-crop", geometry, "+repage", file], true),
                ("scrot", ["-o", "-a", area.X + "," + area.Y + "," + area.Width + "," + area.Height, file], true),
                ("maim", ["-g", geometry, file], true),
                .. whole,
            ];
    }

    private AppError CaptureLinux(ScreenArea area, string file)
    {
        bool found = false;
        foreach ((string program, IReadOnlyList<string> arguments, bool takesArea) in LinuxTools(area, file, Wayland))
        {
            if (!_hasProgram(program))
            {
                continue;
            }

            found = true;
            if (_run(program, arguments) == 0 && File.Exists(file))
            {
                _cropAfter = !takesArea;
                return new AppError();
            }
        }

        return Fail(found ? "The screen could not be captured" : Wayland ? WaylandMessage : X11Message);
    }

    private static AppError Fail(string message) => new() { Kind = ErrorKind.Error, Message = message };

    private static int Run(string program, IReadOnlyList<string> arguments)
    {
        try
        {
            var info = new ProcessStartInfo(program) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (string argument in arguments)
            {
                info.ArgumentList.Add(argument);
            }

            using Process? process = Process.Start(info);
            if (process == null)
            {
                return -1;
            }

            if (!process.WaitForExit(15000))
            {
                process.Kill();
                return -1;
            }

            return process.ExitCode;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            Log.Write("Could not run " + program + ": " + exception.Message, force: true);
            return -1;
        }
    }

    private static bool OnPath(string program) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(folder => File.Exists(Path.Combine(folder, program)));

    /// <summary>
    /// Whether macOS lets this program record the screen; asks the system to offer the
    /// permission to the user when it does not.
    /// </summary>
    private static bool MacScreenRecordingAllowed()
    {
        try
        {
            if (CGPreflightScreenCaptureAccess())
            {
                return true;
            }

            CGRequestScreenCaptureAccess();
            return false;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            // Before macOS 10.15 there is no such permission.
            return true;
        }
    }

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CGPreflightScreenCaptureAccess();

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CGRequestScreenCaptureAccess();
}
