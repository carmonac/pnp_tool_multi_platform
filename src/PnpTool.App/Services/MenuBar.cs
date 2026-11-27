using Avalonia.Controls;
using Avalonia.Input;
using PnpTool.App.ViewModels;
using PnpTool.App.ViewModels.Ribbon;

namespace PnpTool.App.Services;

/// <summary>
/// The menu bar macOS shows at the top of the screen, made from the tabs of the command bar:
/// one menu for each tab, with its buttons as items and a line between its groups.
/// </summary>
/// <remarks>
/// Not in the original, whose commands are only in its ribbon. The application menu, the one
/// named after the program, is set by <see cref="App"/>.
/// </remarks>
public static class MenuBar
{
    /// <summary>The usual shortcuts of the commands that have one on macOS, by the key of their button.</summary>
    private static readonly Dictionary<string, KeyGesture> Shortcuts = new()
    {
        ["Form_Principal_Boton_Nuevo"] = new KeyGesture(Key.N, KeyModifiers.Meta),
        ["Form_Principal_Boton_Abrir"] = new KeyGesture(Key.O, KeyModifiers.Meta),
        ["Form_Principal_Boton_Guardar"] = new KeyGesture(Key.S, KeyModifiers.Meta),
        ["ShellMenuItem_File_GuardarComo"] = new KeyGesture(Key.S, KeyModifiers.Meta | KeyModifiers.Shift),
    };

    public static NativeMenu Build(MainWindowViewModel model)
    {
        var bar = new NativeMenu();

        // The group that makes the PDF is in several tabs; one menu is enough.
        var shown = new HashSet<RibbonGroupViewModel>();
        foreach (RibbonTabViewModel tab in model.Tabs)
        {
            var menu = new NativeMenu();
            foreach (RibbonGroupViewModel group in tab.Groups)
            {
                if (!shown.Add(group))
                {
                    continue;
                }

                if (menu.Items.Count > 0)
                {
                    menu.Add(new NativeMenuItemSeparator());
                }

                foreach (RibbonButtonViewModel button in group.Buttons)
                {
                    menu.Add(new NativeMenuItem(OneLine(button.ToolTipTitle))
                    {
                        Command = button.Command,
                        Gesture = Shortcuts.GetValueOrDefault(button.Key),
                    });
                }
            }

            bar.Add(new NativeMenuItem(OneLine(tab.Header)) { Menu = menu });
        }

        return bar;
    }

    /// <summary>Texts of buttons may be broken in lines to fit under an icon; a menu wants one line.</summary>
    private static string OneLine(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
