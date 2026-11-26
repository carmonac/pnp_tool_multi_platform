using System.Globalization;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;
using PnpTool.Core.Online;

namespace PnpTool.App.ViewModels;

/// <summary>A heading with the lines under it; <see cref="FlagPath"/> is the picture beside the heading.</summary>
public sealed record AboutSection(string? Heading, IReadOnlyList<string> Lines, string? FlagPath = null);

/// <summary>A choice of the list of update notifications.</summary>
public sealed record UpdateChannelOption(UpdateChannel Channel, string Text)
{
    public override string ToString() => Text;
}

/// <summary>One page of the window: what the buttons of the left bar show.</summary>
public sealed record AboutPage(string Key, string Title, IReadOnlyList<AboutSection> Sections, string? Closing = null)
{
    public override string ToString() => Title;
}

/// <summary>
/// Who made the program, its terms and the links to support it. Original: the left bar of the
/// program settings (<c>Barra_Izquierda</c>) with the pages of <c>config/webs</c>, and
/// <c>DonarModelo</c>.
/// </summary>
/// <remarks>
/// Left out: the news, which the original reads from the web site of a printing company, and
/// the changelog of the Windows versions. The setting of update notifications and the button
/// to look for a newer version are those of <c>Control_Actualizaciones</c>, which the original
/// shows in its program settings.
/// </remarks>
public sealed class AboutViewModel : ViewModelBase
{
    public AboutViewModel()
    {
        string language = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
        bool spanish = language is "es" or "ca";

        Pages =
        [
            new AboutPage(
                "about",
                AppInfo.Name,
                [
                    new AboutSection(AppInfo.Name + " " + AppInfo.Version + (spanish ? " para macOS y Linux" : " for macOS and Linux"), Credits.About(spanish)),
                    new AboutSection(null, [Credits.SourceCode(spanish)]),
                ]),
            new AboutPage(
                "terms",
                spanish ? "Términos y condiciones" : "Terms and conditions",
                [
                    new AboutSection(null, Credits.Terms(spanish)),
                    new AboutSection(spanish ? "Perfiles de color ICC de Adobe" : "Adobe ICC colour profiles", Credits.AdobeProfiles(spanish)),
                ]),
            new AboutPage(
                "team",
                "PNP Tool Team",
                [.. Credits.Team(spanish).Select(group => new AboutSection(group.Title, group.Names))],
                Credits.Thanks(spanish)),
            new AboutPage(
                "translators",
                spanish ? "Traductores" : "Translators",
                [.. Credits.Translators.Select(group => new AboutSection(group.Title, [string.Join(" / ", group.Names)], Flag(group.Flag)))],
                Credits.Thanks(spanish)),
            new AboutPage(
                "patrons",
                spanish ? "Mecenas" : "Patrons",
                [new AboutSection(null, Credits.Patrons)],
                Credits.Thanks(spanish)),
        ];
        Selected = Pages[0];
        _spanish = spanish;
        UpdateChannels =
        [
            new UpdateChannelOption(UpdateChannel.None, StringResources.Get("ComboBox_Actualizacion_Ninguna")),
            new UpdateChannelOption(UpdateChannel.Major, StringResources.Get("ComboBox_Actualizacion_Mayor")),
            new UpdateChannelOption(UpdateChannel.Minor, StringResources.Get("ComboBox_Actualizacion_Menor")),
            new UpdateChannelOption(UpdateChannel.Tester, StringResources.Get("ComboBox_Actualizacion_Tester")),
        ];
        _updateChannel = UpdateChannels.First(option => option.Channel == AppSettings.UpdateNotifications);
        UpdateLabel = StringResources.Get("Form_Setup_PnpTool_Label_Actualizacion");
        CheckText = StringResources.Get("ShellMenuBarItem_Ayuda_ComprobarActualizaciones");
        DonateText = StringResources.Get("ShellMenuBarItem_Ayuda_PayPal");
        TelegramText = StringResources.Get("ShellMenuBarItem_Ayuda_Telegram");
    }

    public IReadOnlyList<AboutPage> Pages { get; }

    public AboutPage Selected
    {
        get;
        set => SetProperty(ref field, value ?? field);
    }

    private readonly bool _spanish;
    private UpdateChannelOption _updateChannel;

    /// <summary>Makes what looks for newer versions; replaced in tests, which never use the network.</summary>
    public Func<UpdateChecker> NewUpdateChecker { get; set; } = () => new UpdateChecker();

    /// <summary>Original: <c>ComboboxItemActualizacion.CargarTiposActualizacion</c>.</summary>
    public IReadOnlyList<UpdateChannelOption> UpdateChannels { get; }

    /// <summary>
    /// Which newer versions the program tells about when it starts; "none" stops it from
    /// looking. Kept at once. Original: <c>ConfiguracionPrograma.AvisarActualizaciones</c>.
    /// </summary>
    public UpdateChannelOption Notifications
    {
        get => _updateChannel;
        set
        {
            if (value != null && SetProperty(ref _updateChannel, value))
            {
                AppSettings.UpdateNotifications = value.Channel;
                AppSettings.Save();
            }
        }
    }

    public string UpdateLabel { get; }

    public string CheckText { get; }

    /// <summary>What the last look for a newer version found.</summary>
    public string UpdateStatus
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>The page of the newer version found, or null.</summary>
    public string? UpdateUrl
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasUpdate));
            }
        }
    }

    public bool HasUpdate => UpdateUrl != null;

    /// <summary>
    /// Looks for a newer version now, whatever the setting. Original:
    /// <c>ComprobarActualizacionesToolStripMenuItem_Click</c>.
    /// </summary>
    public async Task CheckForUpdatesAsync()
    {
        UpdateUrl = null;
        UpdateStatus = "…";
        using UpdateChecker checker = NewUpdateChecker();
        UpdateCheckResult result = await checker.CheckAsync(Notifications.Channel, AppInfo.Version, asked: true);
        switch (result.Status)
        {
            case UpdateCheckStatus.Newer:
                UpdateStatus = AppInfo.Version + " → " + result.Release!.Version;
                UpdateUrl = result.Release.Url;
                break;
            case UpdateCheckStatus.Failed:
                UpdateStatus = (_spanish ? "No se pudo comprobar: " : "Could not check: ") + result.Message;
                break;
            default:
                UpdateStatus = StringResources.Get("Actualizacion_NoHay");
                break;
        }
    }

    /// <summary>Opens the page of the newer version found.</summary>
    public void OpenUpdate()
    {
        if (UpdateUrl != null)
        {
            Platform.Services.OpenUrl(UpdateUrl);
        }
    }

    public string DonateText { get; }

    public string TelegramText { get; }

    /// <summary>Opens the donations page of the author. Original: <c>Boton_Donar_Click</c>.</summary>
    public void Donate() => Platform.Services.OpenUrl(AppLinks.Donate);

    /// <summary>Opens the official channel. Original: <c>AbrirTelegram</c>.</summary>
    public void OpenTelegram() => Platform.Services.OpenUrl(AppLinks.Telegram);

    /// <summary>Opens a web address written in a line of a page.</summary>
    public void OpenLink(string url) => Platform.Services.OpenUrl(url);

    /// <summary>The web address a line ends with, if it has one.</summary>
    public static string? LinkIn(string line)
    {
        int start = line.IndexOf("https://", StringComparison.Ordinal);
        return start < 0 ? null : line[start..].Trim();
    }

    private static string? Flag(string? name)
    {
        if (name == null || AppPaths.AssetsFolder.Length == 0)
        {
            return null;
        }

        string path = Path.Combine(AppPaths.FlagsFolder, name + ".png");
        return File.Exists(path) ? path : null;
    }
}
