using System.Collections.Concurrent;
using System.Reflection;
using System.Xml.Serialization;

namespace PnpTool.Core.Xml;

/// <summary>
/// The name an enum member had in the Windows program's code, for the few members whose
/// name there differed from the one written to files (for example <c>SinTrasera</c>, stored
/// as <c>Sin_trasera</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class OriginalNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

/// <summary>
/// Gives the names an enum member had in the Windows program, which are Spanish when the
/// member was renamed: the name written to project files (<see cref="ToFileName"/>) and the
/// name in the code (<see cref="ToOriginalName"/>). Use the first where the Windows code
/// serialised the value, the second where it called <c>ToString()</c> on it, as it did to
/// build the keys of translated texts.
/// </summary>
public static class XmlEnumNames
{
    private static readonly ConcurrentDictionary<Type, (Dictionary<object, string> ToName, Dictionary<string, object> ToValue)> Cache = new();

    /// <summary>The file name of <paramref name="value"/>: its <see cref="XmlEnumAttribute"/> name, or the member name.</summary>
    public static string ToFileName<TEnum>(this TEnum value)
        where TEnum : struct, Enum =>
        Map(typeof(TEnum)).ToName.TryGetValue(value, out string? name) ? name : value.ToString();

    /// <summary>
    /// The name of <paramref name="value"/> in the original code: its
    /// <see cref="OriginalNameAttribute"/> name, or else its file name.
    /// </summary>
    public static string ToOriginalName<TEnum>(this TEnum value)
        where TEnum : struct, Enum =>
        typeof(TEnum).GetField(value.ToString())?.GetCustomAttribute<OriginalNameAttribute>()?.Name ?? value.ToFileName();

    /// <summary>Finds the member stored in files as <paramref name="name"/> (case-sensitive).</summary>
    public static bool TryParseFileName<TEnum>(string? name, out TEnum value)
        where TEnum : struct, Enum
    {
        if (name is not null && Map(typeof(TEnum)).ToValue.TryGetValue(name, out object? boxed))
        {
            value = (TEnum)boxed;
            return true;
        }

        value = default;
        return false;
    }

    private static (Dictionary<object, string> ToName, Dictionary<string, object> ToValue) Map(Type type) =>
        Cache.GetOrAdd(type, static t =>
        {
            var toName = new Dictionary<object, string>();
            var toValue = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (FieldInfo field in t.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                object value = field.GetValue(null)!;
                string name = field.GetCustomAttribute<XmlEnumAttribute>()?.Name ?? field.Name;
                toName[value] = name;
                toValue[name] = value;
            }

            return (toName, toValue);
        });
}
