using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace PnpTool.Controls;

/// <summary>
/// A text box for a number with decimals. Only digits, one decimal separator and, when
/// allowed, a leading minus sign can be typed. The decimal separator is always shown as a
/// point: a comma, typed or pasted, becomes a point, whatever the language of the program.
/// Original: <c>TextBoxDecimalWpf</c> (the copy in <c>ControlFuente</c> is the same control).
/// </summary>
/// <remarks>
/// The original computed its value with <c>Convert.ToDouble</c>, which reads the text in the
/// current culture: in Spanish "1.5" gave 15, and an empty box threw. Here the text is read
/// with a point as the separator in every language, and text that is not a number gives 0.
/// </remarks>
public class DecimalTextBox : TextBox
{
    public DecimalTextBox()
    {
        MaxLength = 6;
        VerticalContentAlignment = VerticalAlignment.Center;
    }

    /// <summary>Original: <c>AceptaValoresNegativos</c>.</summary>
    public bool AllowNegative { get; set; }

    /// <summary>The number in the box, or 0 while the text is not a number. Original: <c>Valor</c>.</summary>
    public double Value { get; private set; }

    protected override Type StyleKeyOverride => typeof(TextBox);

    /// <summary>Reads a number written with a point as the decimal separator; 0 when it is not one.</summary>
    public static double Parse(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0.0;

    /// <summary>Writes a number as the box shows it.</summary>
    public static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Original: <c>DecimalNumberValidationTextBox</c>.</summary>
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

    /// <summary>Original: <c>TextoCambia</c>.</summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != TextProperty)
        {
            return;
        }

        string text = Text ?? string.Empty;
        if (text.Contains(','))
        {
            int caret = CaretIndex;
            Text = text.Replace(',', '.');
            CaretIndex = caret;
            return;
        }

        Value = Parse(text);
    }

    /// <summary>Original: <c>CompruebaModificacion</c>.</summary>
    private bool Accepts(string typed)
    {
        string text = Text ?? string.Empty;
        if (typed is "." or ",")
        {
            return !text.Contains('.');
        }

        return typed == "-" && AllowNegative && !text.Contains('-') && Math.Min(SelectionStart, SelectionEnd) == 0;
    }
}
