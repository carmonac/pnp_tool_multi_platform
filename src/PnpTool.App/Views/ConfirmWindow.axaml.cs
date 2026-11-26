using Avalonia.Controls;
using Avalonia.Interactivity;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.Views;

/// <summary>
/// A question answered with yes or no and, when offered, cancel. It closes with true, false
/// or nothing (cancel, or closed some other way). Original: <c>Dialogos.DialogoSino</c> and
/// <c>DialogoSinoCancelar</c>, which used the message boxes of Windows.
/// </summary>
public partial class ConfirmWindow : Window
{
    public ConfirmWindow()
        : this(string.Empty, true)
    {
    }

    /// <param name="message">The question.</param>
    /// <param name="withCancel">Whether cancelling is offered as a third answer.</param>
    public ConfirmWindow(string message, bool withCancel)
    {
        InitializeComponent();
        Title = AppInfo.Name;
        MessageText.Text = message;
        YesButton.Content = StringResources.Get("Form_ValidarImagen_Boton_Si");
        CancelButton.Content = StringResources.Get("Form_Setup_Boton_Cancelar");
        CancelButton.IsVisible = withCancel;
    }

    private void OnYes(object? sender, RoutedEventArgs e) => Close((bool?)true);

    private void OnNo(object? sender, RoutedEventArgs e) => Close((bool?)false);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close((bool?)null);
}
