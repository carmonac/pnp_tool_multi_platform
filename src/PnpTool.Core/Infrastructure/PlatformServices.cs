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
