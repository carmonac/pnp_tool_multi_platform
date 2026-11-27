using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PnpTool.App.Services;

/// <summary>
/// Gives the buttons that show only a picture a name for assistive technology: the text of
/// their tooltip. A screen reader otherwise announces them as "button" and nothing else.
/// Buttons with a text of their own, or with a name already, are left alone.
/// </summary>
public static class AccessibleNames
{
    private static bool _registered;

    /// <summary>Starts naming buttons as they appear, in every window. Calling it again does nothing.</summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        Control.LoadedEvent.AddClassHandler<Button>((button, _) => Name(button), RoutingStrategies.Direct);
    }

    /// <summary>Names one button after its tooltip, when it has no name and its tooltip is a text.</summary>
    public static void Name(Button button)
    {
        if (!string.IsNullOrEmpty(AutomationProperties.GetName(button)) || button.Content is string or TextBlock)
        {
            return;
        }

        if (ToolTip.GetTip(button) is string tip && tip.Length != 0)
        {
            AutomationProperties.SetName(button, tip);
        }
    }
}
