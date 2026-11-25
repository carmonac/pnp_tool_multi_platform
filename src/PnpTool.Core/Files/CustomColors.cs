using PnpTool.Core.Infrastructure;

namespace PnpTool.Core.Files;

/// <summary>
/// The colours the user picked recently, newest first, kept in a text file with one
/// "A_R_G_B" colour per line. Original: the file handling inside the colour controls
/// <c>Selector_Color</c> (11 colours) and <c>ColorPickerDialog</c> (19 colours), which share
/// the file; each control passes its own capacity.
/// </summary>
public sealed class CustomColors
{
    private readonly string _path;
    private readonly List<string> _colors = [];

    /// <param name="capacity">How many colours the control shows.</param>
    /// <param name="path">The colour file; by default <see cref="AppPaths.CustomColorsFile"/>.</param>
    public CustomColors(int capacity, string? path = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
        _path = path ?? AppPaths.CustomColorsFile;
    }

    public int Capacity { get; }

    /// <summary>The colours as "A_R_G_B" text, newest first.</summary>
    public IReadOnlyList<string> Colors => _colors;

    /// <summary>Whether the text has the four parts of a stored colour, each a number from 0 to 255.</summary>
    public static bool IsColor(string text)
    {
        string[] parts = text.Split('_');
        return parts.Length == 4 && parts.All(part => byte.TryParse(part, out _));
    }

    /// <summary>
    /// Reads up to <see cref="Capacity"/> colours, skipping lines that are not colours.
    /// Original: <c>CargaColoresFichero</c>.
    /// </summary>
    public void Load()
    {
        _colors.Clear();
        if (File.Exists(_path))
        {
            _colors.AddRange(File.ReadLines(_path).Where(IsColor).Take(Capacity));
        }
    }

    /// <summary>
    /// Puts a colour first and drops the oldest one when the list is full.
    /// Original: <c>AsignaColorIndice</c>. Call <see cref="Save"/> to keep it.
    /// </summary>
    public void Push(string color)
    {
        if (!IsColor(color))
        {
            throw new ArgumentException("Not an A_R_G_B colour: " + color, nameof(color));
        }

        _colors.Insert(0, color);
        if (_colors.Count > Capacity)
        {
            _colors.RemoveRange(Capacity, _colors.Count - Capacity);
        }
    }

    /// <summary>Original: <c>GuardaColoresFichero</c>.</summary>
    public void Save() => File.WriteAllLines(_path, _colors);
}
