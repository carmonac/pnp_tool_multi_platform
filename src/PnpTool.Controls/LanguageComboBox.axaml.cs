using Avalonia.Controls;
using Avalonia.Media.Imaging;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;

namespace PnpTool.Controls;

/// <summary>A language with its flag, as the list shows it. Original: <c>ComboboxItemImagen</c>.</summary>
public sealed record LanguageItem(SupportedLanguage Language, Bitmap? Flag);

/// <summary>
/// A list of languages with their flags. Original: <c>ComboBox_Idiomas</c> and
/// <c>ComboboxItemIdioma</c>.
/// </summary>
public partial class LanguageComboBox : UserControl
{
    public LanguageComboBox()
    {
        InitializeComponent();
    }

    /// <summary>Which languages to offer. Original: <c>ListadoIdiomasEsteComoBox</c>.</summary>
    public LanguageList List { get; set; } = LanguageList.Program;

    /// <summary>Raised when a language is picked, also by <see cref="Load"/>.</summary>
    public event EventHandler? LanguageChanged;

    /// <summary>Original: <c>IdiomaSeleccionado</c>.</summary>
    public SupportedLanguage SelectedLanguage { get; private set; } = new();

    /// <summary>
    /// Fills the list and selects the language the program is in, or the first one when that
    /// language is not offered. Original: <c>OnLoad</c>.
    /// </summary>
    public void Load()
    {
        double screenWidth = TopLevel.GetTopLevel(this)?.Screens?.Primary?.Bounds.Width ?? 0.0;
        var list = new SupportedLanguages(List);
        Languages.ItemsSource = list.Languages
            .Select(language => new LanguageItem(language, LoadFlag(language, screenWidth)))
            .ToArray();
        Languages.SelectedIndex = Math.Max(list.IndexOf(AppLanguage.Current), 0);
    }

    /// <summary>A flag that is missing or cannot be read leaves the language without picture.</summary>
    private static Bitmap? LoadFlag(SupportedLanguage language, double screenWidth)
    {
        string path = Path.Combine(AppPaths.FlagsFolder, language.FlagFileName(screenWidth));
        try
        {
            return File.Exists(path) ? new Bitmap(path) : null;
        }
        catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Languages.SelectedItem is LanguageItem item)
        {
            SelectedLanguage = item.Language;
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
