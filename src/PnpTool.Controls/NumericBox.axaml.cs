using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PnpTool.Controls;

/// <summary>
/// A whole number with a button to lower it and a button to raise it, kept between a minimum
/// and a maximum. Original: <c>Control_Numerico_v2</c>.
/// </summary>
public partial class NumericBox : UserControl
{
    private int _value;
    private bool _updating;

    public NumericBox()
        : this(int.MaxValue, 0, 1)
    {
    }

    public NumericBox(int maximum, int minimum, int step)
    {
        InitializeComponent();
        Maximum = maximum;
        Minimum = minimum;
        Step = step;

        // The property is watched rather than the TextChanged event, which arrives later.
        ValueBox.PropertyChanged += (_, change) =>
        {
            if (change.Property == TextBox.TextProperty)
            {
                OnTextChanged();
            }
        };
        ValueBox.Text = "0";
    }

    /// <summary>Raised when the number changes, by typing, by the buttons or from code.</summary>
    public event EventHandler? ValueChanged;

    /// <summary>
    /// The number. A number set from code is limited to the range like a typed one.
    /// Original: <c>Valor</c>.
    /// </summary>
    public int Value
    {
        get => _value;
        set => ValueBox.Text = value.ToString();
    }

    public int Maximum { get; set; }

    public int Minimum { get; set; }

    /// <summary>How much each press of a button adds or takes away. Original: <c>Paso</c>.</summary>
    public int Step { get; set; }

    /// <summary>Original: <c>CambiaParametros</c>.</summary>
    public void SetRange(int maximum, int minimum, int step)
    {
        Maximum = maximum;
        Minimum = minimum;
        Step = step;
    }

    /// <summary>
    /// Brings what was typed into the range as it is typed; text that is not a number counts
    /// as 0. Original: <c>CambioTexto</c>.
    /// </summary>
    private void OnTextChanged()
    {
        if (_updating)
        {
            return;
        }

        int value = Math.Max(Math.Min(IntegerTextBox.Parse(ValueBox.Text), Maximum), Minimum);
        string text = value.ToString();
        if (ValueBox.Text != text)
        {
            _updating = true;
            ValueBox.Text = text;
            ValueBox.CaretIndex = text.Length;
            _updating = false;
        }

        if (value != _value)
        {
            _value = value;
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Original: <c>Aumentar</c>.</summary>
    private void Increase(object? sender, RoutedEventArgs e)
    {
        long next = (long)IntegerTextBox.Parse(ValueBox.Text) + Step;
        Value = (int)Math.Min(next, Maximum);
    }

    /// <summary>Original: <c>Disminuir</c>.</summary>
    private void Decrease(object? sender, RoutedEventArgs e)
    {
        long next = (long)IntegerTextBox.Parse(ValueBox.Text) - Step;
        Value = (int)Math.Max(next, Minimum);
    }
}
