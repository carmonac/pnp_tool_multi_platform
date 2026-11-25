using System.Runtime.CompilerServices;

namespace PnpTool.Core.Infrastructure;

/// <summary>
/// The program log: a text file of XML-like lines, one opening tag when a method starts and
/// one closing tag when it ends. Original: the <c>LibreriaLogs</c> assembly and its wrappers
/// <c>FuncionesGenerales.CreaLog</c>, <c>LogEnter</c>, <c>LogExit</c> and <c>InsertaLog</c>.
/// </summary>
/// <remarks>
/// Two things differ from the original, both of which looked unintended there:
/// <list type="bullet">
/// <item>The original named the tag after the caller found on the stack, which was always its
/// own wrapper, so every line read <c>&lt;ClasesGlobalesPNPTool-FuncionesGenerales-LogEnter&gt;</c>.
/// Here the tag names the calling file and member.</item>
/// <item>The original wrote enter and exit lines only when forced, even with logging switched
/// on in the settings. Here they are also written when <see cref="AppSettings.WriteLogs"/> is on,
/// like <see cref="Write"/>.</item>
/// </list>
/// A line that cannot be written is dropped; logging never stops the program.
/// </remarks>
public static class Log
{
    private const string Header = "<!--Fichero de logs programa PNP Tool-->";
    private static readonly Lock Gate = new();
    private static string? _path;

    /// <summary>Also receives each line written, for example in tests.</summary>
    public static Action<string>? Sink { get; set; }

    /// <summary>
    /// Starts a new log file, replacing the one of the previous run. Original: <c>CreaLog</c>.
    /// </summary>
    public static void Start(string path)
    {
        lock (Gate)
        {
            _path = path;
            try
            {
                string? folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                File.WriteAllText(path, Header + Environment.NewLine + "<Log>" + Environment.NewLine);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _path = null;
            }
        }
    }

    /// <summary>Stops writing to the file.</summary>
    public static void Stop()
    {
        lock (Gate)
        {
            _path = null;
        }
    }

    /// <summary>
    /// Marks the start of a method: <c>&lt;File-Member&gt;value</c>. Original: <c>LogEnter</c>.
    /// </summary>
    /// <param name="value">Text after the tag, usually the method name or an argument.</param>
    /// <param name="force">Write even when logging is off. Original: <c>SaltaCreaLog</c>.</param>
    public static void Enter(string? value = null, bool force = false, [CallerMemberName] string member = "", [CallerFilePath] string file = "") =>
        Write("<" + Tag(file, member) + ">" + value, force);

    /// <summary>
    /// Marks the end of a method: <c>value&lt;/File-Member&gt;</c>. Original: <c>LogExit</c>.
    /// </summary>
    public static void Exit(string? value = null, bool force = false, [CallerMemberName] string member = "", [CallerFilePath] string file = "") =>
        Write(value + "</" + Tag(file, member) + ">", force);

    /// <summary>Writes a line when logging is on or <paramref name="force"/> is set. Original: <c>InsertaLog</c>.</summary>
    public static void Write(string text, bool force = false)
    {
        if (!force && !AppSettings.WriteLogs)
        {
            return;
        }

        Sink?.Invoke(text);
        lock (Gate)
        {
            if (_path == null)
            {
                return;
            }

            try
            {
                File.AppendAllText(_path, text + Environment.NewLine);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>"File-Member", without the characters that would break the tag. Original: <c>ModificaLog</c>.</summary>
    private static string Tag(string file, string member)
    {
        // The path was written by the compiler, possibly on another system: split on both separators.
        string name = file[(file.LastIndexOfAny(['/', '\\']) + 1)..];
        int dot = name.LastIndexOf('.');
        if (dot > 0)
        {
            name = name[..dot];
        }

        return Clean(name) + "-" + Clean(member);
    }

    private static string Clean(string text) =>
        text.Replace("<", string.Empty).Replace(">", string.Empty).Replace(" ", string.Empty);
}
