using Avalonia.Controls;
using Avalonia.Interactivity;
using PnpTool.Core.Infrastructure;

namespace PnpTool.Controls;

/// <summary>
/// The cancel and accept buttons at the foot of the dialogs. Enter presses accept and Escape
/// presses cancel. Original: <c>Control_Botones_OkCancelar</c>, whose windows attached their
/// handlers to the two buttons by name; here the control raises two events.
/// </summary>
public partial class OkCancelButtons : UserControl
{
    public OkCancelButtons()
    {
        InitializeComponent();
        ToolTip.SetTip(OkButtonControl, StringResources.Get("Form_SubirImagenes_BotonOk"));
        ToolTip.SetTip(CancelButtonControl, StringResources.Get("Form_Setup_Boton_Cancelar"));
    }

    public event EventHandler? Accepted;

    public event EventHandler? Cancelled;

    public Button OkButton => OkButtonControl;

    public Button CancelButton => CancelButtonControl;

    private void OnOk(object? sender, RoutedEventArgs e) => Accepted?.Invoke(this, EventArgs.Empty);

    private void OnCancel(object? sender, RoutedEventArgs e) => Cancelled?.Invoke(this, EventArgs.Empty);
}
