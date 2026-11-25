using System.Globalization;
using System.Xml.Linq;

namespace PnpTool.Core.Tests.Files;

/// <summary>Compares two PNP Tool XML files by content rather than by text.</summary>
internal static class XmlComparison
{
    /// <summary>
    /// Every value-holding element of a file, keyed by its position in the tree, for example
    /// <c>/PNP_Tool_Mazo/MazoPersonalizado/Cartas/CartaPersonalizada[3]/Copias</c>.
    /// Repeated siblings are numbered from 1; an element with no children counts as a value.
    /// </summary>
    public static Dictionary<string, string> Leaves(string file)
    {
        var leaves = new Dictionary<string, string>();
        Walk(XDocument.Load(file).Root!, string.Empty, leaves);
        return leaves;
    }

    private static void Walk(XElement element, string prefix, Dictionary<string, string> leaves)
    {
        string path = prefix + "/" + element.Name.LocalName;
        var siblings = element.Parent?.Elements(element.Name).ToList();
        if (siblings is { Count: > 1 })
        {
            path += "[" + (siblings.IndexOf(element) + 1) + "]";
        }

        if (!element.HasElements)
        {
            leaves[path] = element.Value;
            return;
        }

        foreach (XElement child in element.Elements())
        {
            Walk(child, path, leaves);
        }
    }

    /// <summary>
    /// Lists what differs between the file a program wrote (<paramref name="original"/>) and the
    /// same data saved again (<paramref name="saved"/>): values lost, values changed and values
    /// added. Numbers are compared as numbers, so <c>6.300000000000001</c> equals <c>6.3</c>.
    /// Paths accepted by <paramref name="ignore"/> are skipped.
    /// </summary>
    public static List<string> Differences(string original, string saved, Func<string, bool> ignore)
    {
        Dictionary<string, string> before = Leaves(original);
        Dictionary<string, string> after = Leaves(saved);
        var differences = new List<string>();

        foreach ((string path, string value) in before)
        {
            if (ignore(path))
            {
                continue;
            }

            if (!after.TryGetValue(path, out string? savedValue))
            {
                differences.Add($"lost    {path} = '{value}'");
            }
            else if (!SameValue(value, savedValue))
            {
                differences.Add($"changed {path}: '{value}' -> '{savedValue}'");
            }
        }

        foreach ((string path, string value) in after)
        {
            if (!before.ContainsKey(path) && !ignore(path))
            {
                differences.Add($"added   {path} = '{value}'");
            }
        }

        return differences;
    }

    /// <summary>The element path inside a line returned by <see cref="Differences"/>.</summary>
    public static string PathOf(string line)
    {
        string rest = line[8..];
        int end = rest.IndexOfAny([' ', ':']);
        return end < 0 ? rest : rest[..end];
    }

    private static bool SameValue(string a, string b)
    {
        if (a == b)
        {
            return true;
        }

        const NumberStyles style = NumberStyles.Float;
        if (double.TryParse(a, style, CultureInfo.InvariantCulture, out double x)
            && double.TryParse(b, style, CultureInfo.InvariantCulture, out double y))
        {
            return Math.Abs(x - y) <= 1e-9 * Math.Max(1, Math.Max(Math.Abs(x), Math.Abs(y)));
        }

        return false;
    }
}
