using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.Views;

/// <summary>
/// Asks for a number: the position to move a card to, or a number of copies. It closes with
/// the number, or with nothing when closed without accepting. Original: <c>Window_moverA</c>.
/// </summary>
public partial class MoveToWindow : Window
{
    private readonly int _maximum;

    public MoveToWindow()
        : this(1, copies: false)
    {
    }

    /// <param name="value">The number of cards when asking for a position; the current number of copies otherwise.</param>
    /// <param name="copies">Whether the number asked for is a number of copies.</param>
    public MoveToWindow(int value, bool copies)
    {
        InitializeComponent();
        if (copies)
        {
            _maximum = 1000;
            Title = StringResources.Get("MenuContextualPanelImagen_CambiarCopias");
            PositionLabel.Text = "#";
            ValueBox.Text = value.ToString();
        }
        else
        {
            _maximum = value;
            Title = StringResources.Get("Form_MoverA_Boton_Mover");
            PositionLabel.Text = StringResources.Get("Form_MoverA_LabelPosicion");
        }

        ValueBox.MaxLength = _maximum.ToString().Length;
        ValueBox.CaretIndex = ValueBox.Text?.Length ?? 0;
        ToolTip.SetTip(AcceptButton, Title);
        Opened += (_, _) => ValueBox.Focus();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close((int?)null);
            }
        };
    }

    /// <summary>
    /// Accepts the number typed: at most the maximum, and 1 when nothing or 0 was typed.
    /// Original: <c>Boton_MoverA</c>.
    /// </summary>
    public void Accept()
    {
        int value = Math.Min(ValueBox.Value, _maximum);
        Close((int?)(value == 0 ? 1 : value));
    }

    private void OnAccept(object? sender, RoutedEventArgs e) => Accept();
}
