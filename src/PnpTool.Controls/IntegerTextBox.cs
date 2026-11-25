using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace PnpTool.Controls;

/// <summary>
/// A text box for a whole number. Only digits and, when allowed, a leading minus sign can be
/// typed. Original: <c>TextBoxEnteroWpf</c>.
/// </summary>
/// <remarks>
/// The original threw on text that is not a number, such as a lone minus sign or pasted
/// letters; here such text gives 0, as an empty box does.
/// </remarks>
public class IntegerTextBox : TextBox
{
    public IntegerTextBox()
    {
        MaxLength = 3;
        VerticalContentAlignment = VerticalAlignment.Center;
    }

    /// <summary>Original: <c>AceptaValoresNegativos</c>.</summary>
    public bool AllowNegative { get; set; }

    /// <summary>The number in the box, or 0 while the text is not a number. Original: <c>Valor</c>.</summary>
    public int Value { get; private set; }

    protected override Type StyleKeyOverride => typeof(TextBox);

    /// <summary>Reads a whole number, or gives the fallback. Original: <c>Convertir_StringAInt</c>.</summary>
    public static int Parse(string? text, int fallback = 0) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

    /// <summary>Original: <c>NumberValidationTextBox</c>.</summary>
    protected override void OnTextInput(TextInputEventArgs e)
    {
        string typed = e.Text ?? string.Empty;
        if (typed.Length == 0 || !(char.IsDigit(typed, typed.Length - 1) || Accepts(typed)))
        {
            e.Handled = true;
            return;
        }

        base.OnTextInput(e);
    }

    /// <summary>Original: <c>Esto_TextChanged</c>.</summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Value = Parse(Text);
        }
    }

    /// <summary>Original: <c>AceptaModificacion</c>.</summary>
    private bool Accepts(string typed) =>
        typed == "-" && AllowNegative && !(Text ?? string.Empty).Contains('-') && Math.Min(SelectionStart, SelectionEnd) == 0;
}
