using Avalonia.Controls;
using Avalonia.Interactivity;
using PnpTool.App.ViewModels;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Localization;

namespace PnpTool.App.Views;

/// <summary>
/// What the program asks the first time it starts, when it has no settings yet: the
/// language, the units, and to accept the terms of use. Accepting saves the settings, so the
/// window is not shown again; cancelling ends the program.
/// Original: <c>WindowSetupPnpToolReducida</c>, shown by <c>MuestraVentana_ConfiguracionReducida</c>.
/// </summary>
public partial class FirstRunWindow : Window
{
    public FirstRunWindow()
    {
        InitializeComponent();
        Title = AppInfo.Name;
        Buttons.OkButton.IsEnabled = false;
        Translate();
        Opened += (_, _) =>
        {
            LanguageBox.Load();
            LanguageBox.LanguageChanged += (_, _) => UseLanguage(LanguageBox.SelectedLanguage);
        };
    }

    /// <summary>Whether the program has never been set up: it has no settings file. Original: the test of <c>MuestraVentana_ConfiguracionReducida</c>.</summary>
    public static bool IsNeeded => !File.Exists(AppPaths.SettingsFile);

    /// <summary>
    /// The language the window starts in: the system's when the program has it, British
    /// English otherwise. Original: <c>DevuelveIdiomaCodigo</c> of the system's language.
    /// </summary>
    public static SupportedLanguage LanguageFor(string? systemLanguageCode) =>
        new SupportedLanguages(LanguageList.Program).FindByCode(systemLanguageCode);

    /// <summary>Whether the settings were accepted and saved.</summary>
    public bool Accepted { get; private set; }

    /// <summary>Raised after the settings were saved, before the window closes.</summary>
    public event EventHandler? Completed;

    /// <summary>Shows a message; the test of the window replaces it.</summary>
    public Func<string, Task>? ShowMessage { get; set; }

    /// <summary>Opens the terms of use; the test of the window replaces it.</summary>
    public Func<Task>? ShowTerms { get; set; }

    /// <summary>Whether the terms are ticked, which is what lets the settings be accepted.</summary>
    public bool TermsAccepted
    {
        get => TermsCheck.IsChecked == true;
        set => TermsCheck.IsChecked = value;
    }

    /// <summary>
    /// Changes the language of the program and of this window, and says so when its
    /// translation is behind. Original: <c>ComboBox_Idiomas_SelectedIndexChanged</c>.
    /// </summary>
    public void UseLanguage(SupportedLanguage language)
    {
        bool changed = language.Code != AppLanguage.Current.Code;
        AppLanguage.Change(language);
        Translate();
        if (changed && !language.IsUpToDate)
        {
            string message = StringResources.Get("Idioma_Actualizar");
            _ = ShowMessage != null ? ShowMessage(message) : new MessageWindow(message).ShowDialog(this);
        }
    }

    /// <summary>Saves the language and the units and closes, as the accept button does. Original: <c>Boton_Aceptar_Click</c>.</summary>
    public void Accept()
    {
        if (!TermsAccepted)
        {
            return;
        }

        AppSettings.MeasurementSystem = UnitBox.SelectedUnit;
        AppSettings.Save();
        Accepted = true;
        Completed?.Invoke(this, EventArgs.Empty);
        Close();
    }

    /// <summary>Opens the terms of use, as the link does. Original: <c>Click_Link</c>, which opened a page of the program in the browser.</summary>
    public Task OpenTermsAsync()
    {
        if (ShowTerms != null)
        {
            return ShowTerms();
        }

        var about = new AboutViewModel();
        about.Selected = about.Pages.First(page => page.Key == "terms");
        return AboutWindow.ShowOnce(this, about);
    }

    private void Translate()
    {
        TermsText.Text = StringResources.Get("Acepto_Terminos_Uso");
        UnitBox.Translate();
    }

    private void OnTermsChanged(object? sender, RoutedEventArgs e) => Buttons.OkButton.IsEnabled = TermsAccepted;

    private async void OnTermsLink(object? sender, RoutedEventArgs e) => await OpenTermsAsync();

    private void OnAccepted(object? sender, EventArgs e) => Accept();

    private void OnCancelled(object? sender, EventArgs e) => Close();
}
