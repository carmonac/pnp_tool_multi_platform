namespace PnpTool.Core.Infrastructure;

/// <summary>
/// Access to translated texts. Original: <c>FuncionesGenerales.DevuelveCadenaRecurso</c>.
/// Until the localisation resources are ported (backlog task 8) the key itself is returned.
/// </summary>
public static class StringResources
{
    /// <summary>Looks a key up in the current language.</summary>
    public static Func<string, string> Provider { get; set; } = static key => key;

    public static string Get(string key) => Provider(key);
}
