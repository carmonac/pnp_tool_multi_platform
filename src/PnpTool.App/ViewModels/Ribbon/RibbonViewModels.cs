using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.Input;
using PnpTool.Core;
using PnpTool.Core.Infrastructure;

namespace PnpTool.App.ViewModels.Ribbon;

/// <summary>
/// A button of the command bar. Texts come from the translated resources under the button's
/// key, which is the name the control had in the Windows program: <c>key_Small</c> for the
/// label, <c>key</c> for the title of the tooltip and <c>key_ToolTipDescription</c> for its
/// text. Original: <c>FuncionesGeneralesPrograma.LocalizaControles_BotonMenu_Sencillo</c>.
/// </summary>
public sealed class RibbonButtonViewModel : ViewModelBase
{
    /// <param name="key">Name of the button in the Windows program; identifies it.</param>
    /// <param name="icon">Name of its picture in Assets/Icons, without extension.</param>
    /// <param name="command">What pressing it does.</param>
    /// <param name="textKey">Key of its texts when they are filed under another name than the button's, as a few are.</param>
    public RibbonButtonViewModel(string key, string icon, ICommand command, string? textKey = null)
    {
        Key = key;
        Command = command;
        Icon = LoadIcon(icon);
        textKey ??= key;
        string title = Text(textKey);
        Label = Text(textKey + "_Small", fallback: title.Length > 0 ? title : AppInfo.Name);
        ToolTipTitle = title.Length > 0 ? title : Label;
        ToolTipDescription = Text(textKey + "_ToolTipDescription");
    }

    public string Key { get; }

    public string Label { get; }

    public string ToolTipTitle { get; }

    public string ToolTipDescription { get; }

    public bool HasDescription => ToolTipDescription.Length > 0;

    public Bitmap? Icon { get; }

    public ICommand Command { get; }

    /// <summary>
    /// A second, narrow button beside this one that shows a colour and opens the colour
    /// dialog, as the "blank image" button has. Null for the buttons without one.
    /// </summary>
    public ICommand? SwatchCommand { get; init; }

    /// <summary>The colour that second button shows.</summary>
    public Avalonia.Media.IBrush? Swatch
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool HasSwatch => SwatchCommand != null;

    /// <summary>The translated text, or the fallback when the key has none.</summary>
    private static string Text(string key, string fallback = "") =>
        StringResources.Has(key) ? StringResources.Get(key) : fallback;

    private static Bitmap? LoadIcon(string name)
    {
        var uri = new Uri("avares://PnpTool/Assets/Icons/" + name + ".png");
        return AssetLoader.Exists(uri) ? new Bitmap(AssetLoader.Open(uri)) : null;
    }
}

/// <summary>Buttons that belong together, separated from the next group by a line.</summary>
public sealed class RibbonGroupViewModel(params RibbonButtonViewModel[] buttons)
{
    public IReadOnlyList<RibbonButtonViewModel> Buttons { get; } = buttons;
}

/// <summary>A tab of the command bar.</summary>
public sealed class RibbonTabViewModel(string headerKey, params RibbonGroupViewModel[] groups)
{
    /// <summary>What the tab shows before its groups of buttons, such as the lists that choose a style; null for nothing.</summary>
    public object? Leading { get; init; }

    public string Header { get; } = StringResources.Get(headerKey);

    public IReadOnlyList<RibbonGroupViewModel> Groups { get; } = groups;
}
