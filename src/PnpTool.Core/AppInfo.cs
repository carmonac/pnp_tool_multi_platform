using System.Reflection;

namespace PnpTool.Core;

/// <summary>Basic facts about the running application.</summary>
public static class AppInfo
{
    public const string Name = "PNP Tool";

    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0";
}
