using Avalonia;
using Avalonia.Controls;

namespace PnpTool.Controls;

/// <summary>
/// The slider of the main window and of the image-cutting window. It moves in whole ticks,
/// shows its value as a tooltip and reports changes to <see cref="ValueChangedCallback"/>.
/// Original: <c>Control_Slider</c>.
/// </summary>
public class ZoomSlider : Slider
{
    public ZoomSlider()
    {
        IsSnapToTickEnabled = true;
    }

    /// <summary>
    /// Called with the new value. The change that moves the slider away from 0 reports 0, as
    /// in the original, so that setting the first value does not count as a change.
    /// Original: <c>NotificaCambioValor</c>.
    /// </summary>
    public Action<double>? ValueChangedCallback { get; set; }

    protected override Type StyleKeyOverride => typeof(Slider);

    /// <summary>Original: <c>Inicializa</c>.</summary>
    public void Initialize(double tick, double smallChange, double minimum, double maximum, double value)
    {
        TickFrequency = tick;
        SmallChange = smallChange;
        Minimum = minimum;
        Maximum = maximum;
        Value = value;
    }

    /// <summary>
    /// Moves one tick up, or down for a negative amount. Original: <c>CambiaValor</c>, which
    /// moved by 1; with ticks every 25 the slider snapped back and the wheel did nothing.
    /// </summary>
    public void Step(double amount) => Value += amount < 0.0 ? -TickFrequency : TickFrequency;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != ValueProperty)
        {
            return;
        }

        double oldValue = change.GetOldValue<double>();
        double newValue = change.GetNewValue<double>();
        ToolTip.SetTip(this, newValue);
        ValueChangedCallback?.Invoke(oldValue == 0.0 ? 0.0 : newValue);
    }
}
