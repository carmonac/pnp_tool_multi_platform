using Avalonia.Controls;
using Avalonia.Input;

namespace PnpTool.App.Views;

/// <summary>
/// Asks for a short text, such as the name of a player. It closes with the text, or with
/// nothing when cancelled or left empty. Original: <c>Window_EditarTextBox</c>.
/// </summary>
public partial class TextInputWindow : Window
{
    public TextInputWindow()
        : this(string.Empty)
    {
    }

    public TextInputWindow(string title)
    {
        InitializeComponent();
        Title = title;
        Buttons.OkButton.IsDefault = true;
        Opened += (_, _) => TextBox.Focus();
    }

    /// <summary>The text typed so far.</summary>
    public string Text
    {
        get => TextBox.Text ?? string.Empty;
        set => TextBox.Text = value;
    }

    /// <summary>Accepts the text, as the accept button does.</summary>
    public void Accept() => Close(string.IsNullOrEmpty(Text) ? null : Text);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Close(null);
        }
    }

    private void OnAccepted(object? sender, EventArgs e) => Accept();

    private void OnCancelled(object? sender, EventArgs e) => Close(null);
}
