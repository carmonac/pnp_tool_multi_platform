namespace PnpTool.Core.Infrastructure;

/// <summary>
/// Entry points of the program log. Original: <c>FuncionesGenerales.LogEnter</c>,
/// <c>LogExit</c> and <c>InsertaLog</c>. The file writer is attached when logging
/// is ported (backlog task 7); until then calls are dropped.
/// </summary>
public static class Log
{
    /// <summary>Receives each log line.</summary>
    public static Action<string>? Sink { get; set; }

    public static void Enter(string name) => Sink?.Invoke("<LogEnter>" + name);

    public static void Exit(string name) => Sink?.Invoke(name + "</LogExit>");

    public static void Write(string text) => Sink?.Invoke(text);
}
