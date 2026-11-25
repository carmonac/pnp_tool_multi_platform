using Avalonia.Controls;
using PnpTool.Core.CustomDecks;

namespace PnpTool.Controls;

/// <summary>
/// The window to choose a font. It closes with the chosen font, or with nothing when
/// cancelled; the font given is not altered. Original: <c>ControlFuente.Fuente.FontDialog</c>,
/// where cancelling left the changes made in the font it was given.
/// </summary>
public partial class FontDialog : Window
{
    public FontDialog()
        : this(null, null)
    {
    }

    /// <param name="previewText">The sample text; "lorem ipsum" when empty.</param>
    /// <param name="font">The font to start from; the default font when null.</param>
    public FontDialog(string? previewText, FontSpec? font)
    {
        InitializeComponent();
        Picker.PreviewText = string.IsNullOrEmpty(previewText) ? "lorem ipsum" : previewText;
        if (font != null)
        {
            Picker.SelectedFont = font;
        }
    }

    public FontPicker FontPicker => Picker;

    /// <summary>Accepts the font shown, as the accept button does.</summary>
    public void Accept() => Close(Picker.SelectedFont);

    private void OnAccepted(object? sender, EventArgs e) => Accept();

    private void OnCancelled(object? sender, EventArgs e) => Close((FontSpec?)null);
}
