using Avalonia.Controls;
using Avalonia.Media;
using PnpTool.Core.CustomDecks;
using PnpTool.Core.Infrastructure;

namespace PnpTool.Controls;

/// <summary>
/// Chooses a font: family, variant, size and colour, with a sample of text in it.
/// Original: <c>ControlFuente.Fuente.FontPickerControl</c>.
/// </summary>
/// <remarks>
/// Differences from the original: the font given is not altered, the control works on a copy
/// of it; the families are those the system has installed, as Avalonia lists them; and the
/// variants are named from their weight, slant and width. Like the original, it does not let
/// the user choose a line over, under or through the text: that value of the font is kept as
/// it came.
/// </remarks>
public partial class FontPicker : UserControl
{
    private FontSpec _font = new();
    private bool _loading;

    public FontPicker()
    {
        InitializeComponent();
        SizeLabel.Text = StringResources.Get("Tamano");
        ColorButtonControl.ColorChanged += OnColorChanged;

        // The property is watched rather than the TextChanged event, which arrives later.
        SizeBox.PropertyChanged += (_, change) =>
        {
            if (change.Property == TextBox.TextProperty)
            {
                OnSizeChanged();
            }
        };
        Families = [.. FontManager.Current.SystemFonts.Select(family => family.Name).Distinct().Order(StringComparer.CurrentCultureIgnoreCase)];
        SelectedFont = new FontSpec();
    }

    /// <summary>Raised when any value of the font changes. Original: <c>ValueChanged</c>.</summary>
    public event EventHandler? FontChanged;

    /// <summary>The installed font families, in alphabetical order.</summary>
    public IReadOnlyList<string> Families { get; }

    /// <summary>
    /// The font being chosen. Setting it shows a copy of the font given; reading it gives a
    /// copy of the font shown. Original: <c>SelectedFont</c> and <c>FuenteSeleccionada</c>.
    /// </summary>
    public FontSpec SelectedFont
    {
        get => _font.Copy();
        set
        {
            _loading = true;
            _font = value.Copy();

            // A family that is not installed is still listed, so that the font is not changed by merely looking at it.
            FamilyList.ItemsSource = Families.Contains(_font.FontFamily) ? Families : [_font.FontFamily, .. Families];
            FamilyList.SelectedItem = _font.FontFamily;
            ShowFaces(FontFace.Of(_font), keepWanted: true);
            SizeBox.Text = DecimalTextBox.Format(_font.FontSize);
            ColorButtonControl.Load(_font.Color);
            _loading = false;
            UpdatePreview();
        }
    }

    /// <summary>The sample text. Original: <c>TextoEntrada</c>.</summary>
    public string PreviewText
    {
        get => PreviewBlock.Text ?? string.Empty;
        set => PreviewBlock.Text = value;
    }

    /// <summary>Hides the sample. Original: <c>WithoutPreviewRow</c>.</summary>
    public bool WithoutPreviewRow
    {
        get => !PreviewRow.IsVisible;
        set => PreviewRow.IsVisible = !value;
    }

    /// <summary>The variants listed for the family shown.</summary>
    public IReadOnlyList<FontFace> Faces => (IReadOnlyList<FontFace>?)FaceList.ItemsSource ?? [];

    /// <summary>Chooses a family, as picking it from the list does.</summary>
    public void SelectFamily(string family) => FamilyList.SelectedItem = family;

    /// <summary>Chooses a variant of the family, as picking it from the list does.</summary>
    public void SelectFace(FontFace face) => FaceList.SelectedItem = face;

    /// <summary>
    /// Lists the variants of the family and selects the wanted one, or the regular one, or
    /// the first. A wanted variant the family does not have is listed too.
    /// </summary>
    private void ShowFaces(FontFace wanted, bool keepWanted)
    {
        IReadOnlyList<FontFace> faces = FontFace.Of(_font.FontFamily);
        if (keepWanted && !faces.Contains(wanted))
        {
            faces = [wanted, .. faces];
        }

        FaceList.ItemsSource = faces;
        FaceList.SelectedItem = faces.Contains(wanted) ? wanted : faces.Contains(FontFace.Regular) ? FontFace.Regular : faces[0];
    }

    /// <summary>Original: <c>FontSelector_SelectionChanged</c>.</summary>
    private void OnFamilyChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading || FamilyList.SelectedItem is not string family)
        {
            return;
        }

        _font.FontFamily = family;
        _loading = true;
        ShowFaces(FontFace.Of(_font), keepWanted: false);
        _loading = false;
        Changed();
    }

    /// <summary>Original: <c>TypefaceSelector_SelectionChanged</c>.</summary>
    private void OnFaceChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (FaceList.SelectedItem is not FontFace face)
        {
            return;
        }

        _font.FontStyle = face.Style;
        _font.FontWeight = face.Weight;
        _font.FontStretch = face.Stretch;
        if (!_loading)
        {
            Changed();
        }
    }

    /// <summary>
    /// The size is a whole number; an empty box leaves the size as it was and text that is
    /// not a number gives 10. Original: <c>SizeTextbox_TextChanged</c>.
    /// </summary>
    private void OnSizeChanged()
    {
        if (_loading || string.IsNullOrEmpty(SizeBox.Text))
        {
            return;
        }

        _font.FontSize = IntegerTextBox.Parse(SizeBox.Text, 10);
        Changed();
    }

    /// <summary>Original: <c>Boton_Color_Click</c>.</summary>
    private void OnColorChanged(string color)
    {
        _font.Color = color;
        Changed();
    }

    private void Changed()
    {
        UpdatePreview();
        FontChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdatePreview()
    {
        _font.ApplyTo(PreviewBlock);

        // A size of 0 cannot be drawn.
        PreviewBlock.FontSize = Math.Max(_font.FontSize, 1.0);
    }
}
