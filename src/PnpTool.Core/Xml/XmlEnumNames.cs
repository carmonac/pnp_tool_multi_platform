using System.Collections.Concurrent;
using System.Reflection;
using System.Xml.Serialization;

namespace PnpTool.Core.Xml;

/// <summary>
/// Gives the name an enum member has in project files, which is its original Spanish
/// name when the member was renamed. Use this wherever the Windows code called
/// <c>ToString()</c> or <c>Enum.Parse</c> on an enum and the text is stored or compared.
/// </summary>
public static class XmlEnumNames
{
    private static readonly ConcurrentDictionary<Type, (Dictionary<object, string> ToName, Dictionary<string, object> ToValue)> Cache = new();

    /// <summary>The file name of <paramref name="value"/>: its <see cref="XmlEnumAttribute"/> name, or the member name.</summary>
    public static string ToFileName<TEnum>(this TEnum value)
        where TEnum : struct, Enum =>
        Map(typeof(TEnum)).ToName.TryGetValue(value, out string? name) ? name : value.ToString();

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
