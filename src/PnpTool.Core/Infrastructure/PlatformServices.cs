using System.Diagnostics;

namespace PnpTool.Core.Infrastructure;

/// <summary>
/// What the program asks of the operating system outside its own windows. Original: calls to
/// <c>explorer.exe</c> and to the Windows shell spread over <c>FuncionesGenerales</c>
/// (<c>EjecutaArchivo</c>, <c>AbreExplorer</c>).
/// </summary>
public interface IPlatformServices
{
    /// <summary>Opens a web address in the default browser.</summary>
    void OpenUrl(string url);

    /// <summary>Opens a file with the program the system associates with it.</summary>
    void OpenFile(string path);

    /// <summary>Shows a folder in the system's file manager.</summary>
    void OpenFolder(string path);

    /// <summary>
    /// Shows a file in the system's file manager: selected, where the system can do that,
    /// or else its folder. Original: <c>AbreExplorer</c> with <c>seleccionado</c>.
    /// </summary>
    void RevealFile(string path);

    /// <summary>
    /// Starts the calculator of the system. Gives whether one was found. Original:
    /// <c>EjecutaPrograma("calc.exe")</c>.
    /// </summary>
    bool OpenCalculator() => false;
}

/// <summary>
/// The implementation for desktop systems: <c>open</c> on macOS, <c>xdg-open</c> on Linux and
/// the shell on Windows. Failures are logged and otherwise ignored, as opening something is
/// never essential to what the program was doing.
/// </summary>
public sealed class DesktopPlatformServices : IPlatformServices
{
    public void OpenUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https")
        {
            Launch(uri.AbsoluteUri);
        }
        else
        {
            Log.Write("Not a web address: " + url, force: true);
        }
    }

    public void OpenFile(string path)
    {
        if (File.Exists(path))
        {
            Launch(path);
        }
    }

    public void OpenFolder(string path)
    {
        if (Directory.Exists(path))
        {
            Launch(path);
        }
    }

    public void RevealFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            Start("open", "-R", path);
        }
        else if (OperatingSystem.IsWindows())
        {
            Start("explorer.exe", "/select,\"" + path + "\"");
        }
        else
        {
            OpenFolder(Path.GetDirectoryName(path) ?? string.Empty);
        }
    }

    /// <summary>The calculators looked for on Linux, in order: those of the usual desktops, then the plain X one.</summary>
    public static IReadOnlyList<string> LinuxCalculators { get; } =
        ["gnome-calculator", "kcalc", "mate-calc", "galculator", "qalculate-gtk", "xcalc"];

    /// <summary>The program and arguments that start the calculator on a system, or null when none is found.</summary>
    public static (string Program, string[] Arguments)? CalculatorCommand(bool macOS, bool linux, Func<string, bool> hasProgram)
    {
        if (macOS)
        {
            return ("open", ["-a", "Calculator"]);
        }

        if (!linux)
        {
            return ("calc.exe", []);
        }

        string? found = LinuxCalculators.FirstOrDefault(hasProgram);
        return found == null ? null : (found, []);
    }

    public bool OpenCalculator()
    {
        (string Program, string[] Arguments)? command = CalculatorCommand(OperatingSystem.IsMacOS(), OperatingSystem.IsLinux(), OnPath);
        if (command == null)
        {
            Log.Write("No calculator found", force: true);
            return false;
        }

        return Start(command.Value.Program, command.Value.Arguments);
    }

    private static bool OnPath(string program) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(folder => File.Exists(Path.Combine(folder, program)));

    private static bool Start(string program, params string[] arguments)
    {
        try
        {
            var info = new ProcessStartInfo(program);
            foreach (string argument in arguments)
            {
                info.ArgumentList.Add(argument);
            }

            using Process? process = Process.Start(info);
            return process != null;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            Log.Write("Could not start " + program + ": " + exception.Message, force: true);
            return false;
        }
    }

    private static void Launch(string target)
    {
        try
        {
            ProcessStartInfo info;
            if (OperatingSystem.IsMacOS())
            {
                info = new ProcessStartInfo("open");
                info.ArgumentList.Add(target);
            }
            else if (OperatingSystem.IsLinux())
            {
                info = new ProcessStartInfo("xdg-open");
                info.ArgumentList.Add(target);
            }
            else
            {
                info = new ProcessStartInfo(target) { UseShellExecute = true };
            }

            using Process? process = Process.Start(info);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            Log.Write("Could not open " + target + ": " + exception.Message, force: true);
        }
    }
}

/// <summary>The platform services in use.</summary>
public static class Platform
{
    public static IPlatformServices Services { get; set; } = new DesktopPlatformServices();
}
