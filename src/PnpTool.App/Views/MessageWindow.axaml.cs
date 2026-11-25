using Avalonia.Controls;

namespace PnpTool.App.Views;

/// <summary>
/// A window with a message and a button to close it. Original: <c>Dialogos.DialogoError</c>
/// for the one case the program cannot start.
/// </summary>
public partial class MessageWindow : Window
{
    public MessageWindow()
        : this(string.Empty)
    {
    }

    public MessageWindow(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
        CloseButton.Click += (_, _) => Close();
    }
}
