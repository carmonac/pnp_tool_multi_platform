using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace PnpTool.App.Views;

/// <summary>
/// The tabs of the command bar. It is a <see cref="TabControl"/> whose tabs also answer to
/// being "pressed" by assistive technology: Avalonia's tab only offers "select", which
/// VoiceOver's press (AXPress) does not use, so a screen reader could not change tab.
/// </summary>
public class RibbonTabControl : TabControl
{
    protected override Type StyleKeyOverride => typeof(TabControl);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new RibbonTabItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<RibbonTabItem>(item, out recycleKey);
}

/// <summary>A tab that can be pressed through the accessibility interface.</summary>
public class RibbonTabItem : TabItem
{
    protected override Type StyleKeyOverride => typeof(TabItem);

    protected override AutomationPeer OnCreateAutomationPeer() => new RibbonTabItemPeer(this);
}

/// <summary>Pressing the tab selects it.</summary>
public class RibbonTabItemPeer(RibbonTabItem owner) : ListItemAutomationPeer(owner), IInvokeProvider
{
    public void Invoke() => owner.IsSelected = true;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;
}
